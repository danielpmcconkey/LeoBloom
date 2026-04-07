namespace LeoBloom.Ops

open System
open Npgsql
open LeoBloom.Domain.Ops

/// Raw SQL queries for balance projection data. All functions accept a
/// caller-provided NpgsqlTransaction for atomicity.
module BalanceProjectionRepository =

    /// Return projected obligation line items for the given account and date range.
    /// Includes expected and in_flight instances where the account is either:
    ///   - the destination (receivable), or
    ///   - the source (payable).
    let getProjectedObligationItems
        (txn: NpgsqlTransaction)
        (accountId: int)
        (fromDate: DateOnly)
        (toDate: DateOnly)
        : ProjectionLineItem list =
        use sql = new NpgsqlCommand(
            "SELECT oi.expected_date, oi.amount, oa.obligation_type, oa.name, oi.name \
             FROM ops.obligation_instance oi \
             JOIN ops.obligation_agreement oa ON oa.id = oi.obligation_agreement_id \
             WHERE oi.is_active = true \
               AND oi.status IN ('expected', 'in_flight') \
               AND oi.expected_date >= @from_date \
               AND oi.expected_date <= @to_date \
               AND ( \
                 (oa.obligation_type = 'receivable' AND oa.dest_account_id = @account_id) \
                 OR \
                 (oa.obligation_type = 'payable' AND oa.source_account_id = @account_id) \
               ) \
             ORDER BY oi.expected_date, oa.name",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@account_id", accountId) |> ignore
        sql.Parameters.AddWithValue("@from_date", fromDate) |> ignore
        sql.Parameters.AddWithValue("@to_date", toDate) |> ignore
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            let date = reader.GetFieldValue<DateOnly>(0)
            let amount = if reader.IsDBNull(1) then None else Some (reader.GetDecimal(1))
            let obligationType = reader.GetString(2)
            let agreementName = reader.GetString(3)
            let instanceName = reader.GetString(4)
            let description = sprintf "%s — %s" agreementName instanceName
            let direction = if obligationType = "receivable" then Inflow else Outflow
            let sourceType = if obligationType = "receivable" then ObligationInflow else ObligationOutflow
            results <-
                { date = date
                  description = description
                  amount = amount
                  direction = direction
                  sourceType = sourceType } :: results
        reader.Close()
        results |> List.rev

    /// Return in-flight (initiated, not confirmed) transfer line items for the given
    /// account and date range. Projection date = expected_settlement if set,
    /// otherwise initiated_date. Items outside the date range are excluded.
    let getProjectedTransferItems
        (txn: NpgsqlTransaction)
        (accountId: int)
        (fromDate: DateOnly)
        (toDate: DateOnly)
        : ProjectionLineItem list =
        use sql = new NpgsqlCommand(
            "SELECT t.initiated_date, t.expected_settlement, t.amount, \
                    t.from_account_id, t.to_account_id, t.description \
             FROM ops.transfer t \
             WHERE t.is_active = true \
               AND t.status = 'initiated' \
               AND (t.from_account_id = @account_id OR t.to_account_id = @account_id) \
               AND COALESCE(t.expected_settlement, t.initiated_date) BETWEEN @from_date AND @to_date",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@account_id", accountId) |> ignore
        sql.Parameters.AddWithValue("@from_date", fromDate) |> ignore
        sql.Parameters.AddWithValue("@to_date", toDate) |> ignore
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            let initiatedDate = reader.GetFieldValue<DateOnly>(0)
            let expectedSettlement =
                if reader.IsDBNull(1) then None
                else Some (reader.GetFieldValue<DateOnly>(1))
            let amount = reader.GetDecimal(2)
            let fromAccountId = reader.GetInt32(3)
            let toAccountId = reader.GetInt32(4)
            let desc =
                if reader.IsDBNull(5) then "In-flight transfer"
                else reader.GetString(5)
            let projDate = expectedSettlement |> Option.defaultValue initiatedDate
            let isOutflow = fromAccountId = accountId
            let direction = if isOutflow then Outflow else Inflow
            let sourceType = if isOutflow then TransferOut else TransferIn
            results <-
                { date = projDate
                  description = desc
                  amount = Some amount
                  direction = direction
                  sourceType = sourceType } :: results
        reader.Close()
        results |> List.sortBy (fun i -> i.date)
