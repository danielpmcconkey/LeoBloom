namespace LeoBloom.Reporting

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Queries account balances for property accounts within a calendar year.
/// All operations run within a caller-provided NpgsqlTransaction.
module ScheduleERepository =

    /// Account balance row: code, name, net balance (positive = normal direction).
    type AccountBalanceRow =
        { accountCode: string
          accountName: string
          netBalance: decimal }

    /// Query net balances for the given account codes within a calendar year.
    /// Revenue accounts: credit - debit (normal balance = credit).
    /// Expense accounts: debit - credit (normal balance = debit).
    /// Filters to non-voided entries only.
    let getBalancesForYear
        (txn: NpgsqlTransaction)
        (accountCodes: string list)
        (year: int)
        : AccountBalanceRow list =

        if accountCodes.IsEmpty then []
        else
            let startDate = DateOnly(year, 1, 1)
            let endDate = DateOnly(year, 12, 31)

            let paramNames =
                accountCodes
                |> List.mapi (fun i _ -> sprintf "@code%d" i)
                |> String.concat ", "

            let query =
                sprintf
                    "SELECT
                        a.code,
                        a.name,
                        at.normal_balance,
                        COALESCE(SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END), 0) AS debit_total,
                        COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0) AS credit_total
                    FROM ledger.account a
                    JOIN ledger.account_type at ON a.account_type_id = at.id
                    LEFT JOIN (
                        ledger.journal_entry_line jel
                        JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
                            AND je.voided_at IS NULL
                            AND je.entry_date >= @start_date
                            AND je.entry_date <= @end_date
                    ) ON jel.account_id = a.id
                    WHERE a.code IN (%s)
                    GROUP BY a.code, a.name, at.normal_balance
                    ORDER BY a.code"
                    paramNames

            use sql = new NpgsqlCommand(query, txn.Connection, txn)
            sql.Parameters.AddWithValue("@start_date", startDate) |> ignore
            sql.Parameters.AddWithValue("@end_date", endDate) |> ignore
            accountCodes
            |> List.iteri (fun i code ->
                sql.Parameters.AddWithValue(sprintf "@code%d" i, code) |> ignore)

            use reader = sql.ExecuteReader()
            let mutable results = []
            while reader.Read() do
                let nb = match reader.GetString(2) with "credit" -> NormalBalance.Credit | _ -> NormalBalance.Debit
                let debitTotal = reader.GetDecimal(3)
                let creditTotal = reader.GetDecimal(4)
                let netBalance = resolveBalance nb debitTotal creditTotal
                results <-
                    { accountCode = reader.GetString(0)
                      accountName = reader.GetString(1)
                      netBalance = netBalance } :: results
            reader.Close()
            List.rev results
