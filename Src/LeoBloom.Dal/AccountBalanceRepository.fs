namespace LeoBloom.Dal

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Read-only query for account balance calculation.
module AccountBalanceRepository =

    let getBalance (txn: NpgsqlTransaction) (accountId: int) (asOfDate: DateOnly) : AccountBalance option =
        use sql = new NpgsqlCommand(
            "SELECT a.id, a.code, a.name, at.normal_balance,
                    COALESCE(SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END), 0)
                        - COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0)
                        AS raw_balance
             FROM ledger.account a
             JOIN ledger.account_type at ON a.account_type_id = at.id
             LEFT JOIN (ledger.journal_entry_line jel
                 JOIN ledger.journal_entry je ON jel.journal_entry_id = je.id
                     AND je.voided_at IS NULL
                     AND je.entry_date <= @as_of_date)
                 ON jel.account_id = a.id
             WHERE a.id = @account_id
             GROUP BY a.id, a.code, a.name, at.normal_balance",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@account_id", accountId) |> ignore
        sql.Parameters.AddWithValue("@as_of_date", asOfDate) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let nb = match reader.GetString(3) with "debit" -> NormalBalance.Debit | _ -> NormalBalance.Credit
            let rawBalance = reader.GetDecimal(4)
            let balance = match nb with NormalBalance.Debit -> rawBalance | NormalBalance.Credit -> -rawBalance
            let result =
                Some { accountId = reader.GetInt32(0)
                       accountCode = reader.GetString(1)
                       accountName = reader.GetString(2)
                       normalBalance = nb
                       balance = balance
                       asOfDate = asOfDate }
            reader.Close()
            result
        else
            reader.Close()
            None

    let resolveAccountId (txn: NpgsqlTransaction) (code: string) : int option =
        use sql = new NpgsqlCommand(
            "SELECT id FROM ledger.account WHERE code = @code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@code", code) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let id = reader.GetInt32(0)
            reader.Close()
            Some id
        else
            reader.Close()
            None
