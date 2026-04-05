namespace LeoBloom.Reporting

open System
open Npgsql

/// Shared repository for cash receipts and cash disbursements.
/// Queries journal entry lines where one side hits a cash/bank account.
/// All operations run within a caller-provided NpgsqlTransaction.
module CashFlowRepository =

    /// Raw cash flow row from the query.
    type CashFlowRow =
        { date: DateOnly
          journalEntryId: int
          description: string
          counterpartyAccount: string
          amount: decimal }

    /// Query cash receipts (debits to cash accounts = money in).
    /// Returns all debit-side entries on cash accounts within the date range,
    /// with the counterparty being the credit-side account on the same entry.
    let getReceipts
        (txn: NpgsqlTransaction)
        (fromDate: DateOnly)
        (toDate: DateOnly)
        : CashFlowRow list =

        use sql = new NpgsqlCommand(
            "SELECT
                je.entry_date,
                je.id,
                je.description,
                COALESCE(counter_acct.name, '(unknown)') AS counterparty,
                jel.amount
            FROM ledger.journal_entry_line jel
            JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
            JOIN ledger.account cash_acct ON jel.account_id = cash_acct.id
            LEFT JOIN LATERAL (
                SELECT a.name
                FROM ledger.journal_entry_line jel2
                JOIN ledger.account a ON jel2.account_id = a.id
                WHERE jel2.journal_entry_id = je.id
                  AND jel2.entry_type = 'credit'
                  AND a.account_subtype IS DISTINCT FROM 'Cash'
                LIMIT 1
            ) counter_acct ON true
            WHERE cash_acct.account_subtype = 'Cash'
              AND jel.entry_type = 'debit'
              AND je.voided_at IS NULL
              AND je.entry_date >= @from_date
              AND je.entry_date <= @to_date
            ORDER BY je.entry_date, je.id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@from_date", fromDate) |> ignore
        sql.Parameters.AddWithValue("@to_date", toDate) |> ignore

        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <-
                { date = reader.GetFieldValue<DateOnly>(0)
                  journalEntryId = reader.GetInt32(1)
                  description = reader.GetString(2)
                  counterpartyAccount = reader.GetString(3)
                  amount = reader.GetDecimal(4) } :: results
        reader.Close()
        List.rev results

    /// Query cash disbursements (credits to cash accounts = money out).
    /// Returns all credit-side entries on cash accounts within the date range,
    /// with the counterparty being the debit-side account on the same entry.
    let getDisbursements
        (txn: NpgsqlTransaction)
        (fromDate: DateOnly)
        (toDate: DateOnly)
        : CashFlowRow list =

        use sql = new NpgsqlCommand(
            "SELECT
                je.entry_date,
                je.id,
                je.description,
                COALESCE(counter_acct.name, '(unknown)') AS counterparty,
                jel.amount
            FROM ledger.journal_entry_line jel
            JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
            JOIN ledger.account cash_acct ON jel.account_id = cash_acct.id
            LEFT JOIN LATERAL (
                SELECT a.name
                FROM ledger.journal_entry_line jel2
                JOIN ledger.account a ON jel2.account_id = a.id
                WHERE jel2.journal_entry_id = je.id
                  AND jel2.entry_type = 'debit'
                  AND a.account_subtype IS DISTINCT FROM 'Cash'
                LIMIT 1
            ) counter_acct ON true
            WHERE cash_acct.account_subtype = 'Cash'
              AND jel.entry_type = 'credit'
              AND je.voided_at IS NULL
              AND je.entry_date >= @from_date
              AND je.entry_date <= @to_date
            ORDER BY je.entry_date, je.id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@from_date", fromDate) |> ignore
        sql.Parameters.AddWithValue("@to_date", toDate) |> ignore

        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <-
                { date = reader.GetFieldValue<DateOnly>(0)
                  journalEntryId = reader.GetInt32(1)
                  description = reader.GetString(2)
                  counterpartyAccount = reader.GetString(3)
                  amount = reader.GetDecimal(4) } :: results
        reader.Close()
        List.rev results
