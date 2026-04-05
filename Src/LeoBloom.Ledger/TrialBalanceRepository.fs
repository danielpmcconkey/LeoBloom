namespace LeoBloom.Ledger

open Npgsql
open LeoBloom.Domain.Ledger

/// Read-only queries for trial balance calculation.
module TrialBalanceRepository =

    let getActivityByPeriod (txn: NpgsqlTransaction) (fiscalPeriodId: int) : TrialBalanceAccountLine list =
        use sql = new NpgsqlCommand(
            "SELECT
                a.id,
                a.code,
                a.name,
                at.name AS account_type_name,
                at.normal_balance,
                COALESCE(SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END), 0) AS debit_total,
                COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0) AS credit_total
            FROM ledger.journal_entry je
            JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
            JOIN ledger.account a ON jel.account_id = a.id
            JOIN ledger.account_type at ON a.account_type_id = at.id
            WHERE je.fiscal_period_id = @fiscal_period_id
              AND je.voided_at IS NULL
            GROUP BY a.id, a.code, a.name, at.name, at.normal_balance
            ORDER BY at.name, a.code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@fiscal_period_id", fiscalPeriodId) |> ignore
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            let nb = match reader.GetString(4) with "debit" -> NormalBalance.Debit | _ -> NormalBalance.Credit
            let debitTotal = reader.GetDecimal(5)
            let creditTotal = reader.GetDecimal(6)
            let netBalance =
                match nb with
                | NormalBalance.Debit -> debitTotal - creditTotal
                | NormalBalance.Credit -> creditTotal - debitTotal
            results <-
                { accountId = reader.GetInt32(0)
                  accountCode = reader.GetString(1)
                  accountName = reader.GetString(2)
                  accountTypeName = reader.GetString(3)
                  normalBalance = nb
                  debitTotal = debitTotal
                  creditTotal = creditTotal
                  netBalance = netBalance } :: results
        reader.Close()
        results |> List.rev

    let resolvePeriodId (txn: NpgsqlTransaction) (periodKey: string) : int option =
        use sql = new NpgsqlCommand(
            "SELECT id FROM ledger.fiscal_period WHERE period_key = @period_key",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@period_key", periodKey) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let id = reader.GetInt32(0)
            reader.Close()
            Some id
        else
            reader.Close()
            None

    let periodExists (txn: NpgsqlTransaction) (fiscalPeriodId: int) : (int * string) option =
        use sql = new NpgsqlCommand(
            "SELECT id, period_key FROM ledger.fiscal_period WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", fiscalPeriodId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = (reader.GetInt32(0), reader.GetString(1))
            reader.Close()
            Some result
        else
            reader.Close()
            None
