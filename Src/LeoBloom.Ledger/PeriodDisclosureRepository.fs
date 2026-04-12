namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Fetches period disclosure data: metadata + adjustment summary + adjustment detail.
module PeriodDisclosureRepository =

    /// Fetch disclosure for a known period ID. Queries fiscal_period for metadata,
    /// then journal_entry for adjustment JEs (adjustment_for_period_id = period_id).
    let getDisclosure (txn: NpgsqlTransaction) (fiscalPeriodId: int) : PeriodDisclosure option =
        // First: get period metadata
        use metaSql = new NpgsqlCommand(
            "SELECT id, period_key, start_date, end_date, is_open,
                    closed_at, closed_by, reopened_count
             FROM ledger.fiscal_period WHERE id = @id",
            txn.Connection, txn)
        metaSql.Parameters.AddWithValue("@id", fiscalPeriodId) |> ignore
        use metaReader = metaSql.ExecuteReader()
        if not (metaReader.Read()) then
            metaReader.Close()
            None
        else
            let periodKey = metaReader.GetString(1)
            let startDate = metaReader.GetFieldValue<DateOnly>(2)
            let endDate = metaReader.GetFieldValue<DateOnly>(3)
            let isOpen = metaReader.GetBoolean(4)
            let closedAt = if metaReader.IsDBNull(5) then None else Some (metaReader.GetFieldValue<DateTimeOffset>(5))
            let closedBy = if metaReader.IsDBNull(6) then None else Some (metaReader.GetString(6))
            let reopenedCount = metaReader.GetInt32(7)
            metaReader.Close()

            // Second: get adjustment JEs for this period
            use adjSql = new NpgsqlCommand(
                "SELECT je.id, je.entry_date, je.description,
                        COALESCE(SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END), 0)
                      - COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0) AS net_amount
                 FROM ledger.journal_entry je
                 JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
                 WHERE je.adjustment_for_period_id = @period_id
                   AND je.voided_at IS NULL
                 GROUP BY je.id, je.entry_date, je.description
                 ORDER BY je.entry_date, je.id",
                txn.Connection, txn)
            adjSql.Parameters.AddWithValue("@period_id", fiscalPeriodId) |> ignore
            use adjReader = adjSql.ExecuteReader()
            let mutable adjustments = []
            while adjReader.Read() do
                adjustments <-
                    { journalEntryId = adjReader.GetInt32(0)
                      entryDate = adjReader.GetFieldValue<DateOnly>(1)
                      description = adjReader.GetString(2)
                      netAmount = adjReader.GetDecimal(3) } :: adjustments
            adjReader.Close()
            let adjustments = List.rev adjustments
            let adjustmentCount = List.length adjustments
            let adjustmentNetImpact = adjustments |> List.sumBy (fun a -> a.netAmount)

            Some { fiscalPeriodId = fiscalPeriodId
                   periodKey = periodKey
                   startDate = startDate
                   endDate = endDate
                   isOpen = isOpen
                   closedAt = closedAt
                   closedBy = closedBy
                   reopenedCount = reopenedCount
                   adjustmentCount = adjustmentCount
                   adjustmentNetImpact = adjustmentNetImpact
                   adjustments = adjustments
                   asOriginallyClosed = false }

    /// Resolve a period key to a period ID, then delegate to getDisclosure.
    let getDisclosureByKey (txn: NpgsqlTransaction) (periodKey: string) : PeriodDisclosure option =
        use sql = new NpgsqlCommand(
            "SELECT id FROM ledger.fiscal_period WHERE period_key = @key",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@key", periodKey) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let id = reader.GetInt32(0)
            reader.Close()
            getDisclosure txn id
        else
            reader.Close()
            None
