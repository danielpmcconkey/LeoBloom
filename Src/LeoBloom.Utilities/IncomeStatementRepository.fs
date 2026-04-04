namespace LeoBloom.Utilities

open Npgsql
open LeoBloom.Domain.Ledger

/// Read-only queries for income statement calculation.
module IncomeStatementRepository =

    let getActivityByPeriod (txn: NpgsqlTransaction) (fiscalPeriodId: int) : (string * IncomeStatementLine) list =
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
              AND at.name IN ('revenue', 'expense')
            GROUP BY a.id, a.code, a.name, at.name, at.normal_balance
            ORDER BY at.name, a.code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@fiscal_period_id", fiscalPeriodId) |> ignore
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            let accountTypeName = reader.GetString(3)
            let normalBalance = reader.GetString(4)
            let debitTotal = reader.GetDecimal(5)
            let creditTotal = reader.GetDecimal(6)
            let balance =
                match normalBalance with
                | "credit" -> creditTotal - debitTotal
                | _ -> debitTotal - creditTotal
            let line : IncomeStatementLine =
                { accountId = reader.GetInt32(0)
                  accountCode = reader.GetString(1)
                  accountName = reader.GetString(2)
                  balance = balance }
            results <- (accountTypeName, line) :: results
        reader.Close()
        results |> List.rev
