namespace LeoBloom.Reporting

open System
open Npgsql

/// Queries journal entry lines for a single account within a date range.
/// All operations run within a caller-provided NpgsqlTransaction.
module GeneralLedgerRepository =

    /// Resolve an account code to its ID, name, and normal balance direction.
    /// Returns None if not found.
    let resolveAccountByCode
        (txn: NpgsqlTransaction)
        (accountCode: string)
        : (int * string * string) option =

        use sql = new NpgsqlCommand(
            "SELECT a.id, a.name, at.normal_balance
             FROM ledger.account a
             JOIN ledger.account_type at ON a.account_type_id = at.id
             WHERE a.code = @code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@code", accountCode) |> ignore
        use reader = sql.ExecuteReader()
        let result =
            if reader.Read() then
                Some (reader.GetInt32(0), reader.GetString(1), reader.GetString(2))
            else
                None
        reader.Close()
        result

    /// Raw row from the general ledger query.
    type GLEntryRow =
        { date: DateOnly
          journalEntryId: int
          description: string
          debitAmount: decimal
          creditAmount: decimal }

    /// Query journal entry lines for a specific account within a date range.
    /// Excludes voided entries. Ordered by date, then entry ID.
    let getEntriesForAccount
        (txn: NpgsqlTransaction)
        (accountId: int)
        (fromDate: DateOnly)
        (toDate: DateOnly)
        : GLEntryRow list =

        use sql = new NpgsqlCommand(
            "SELECT
                je.entry_date,
                je.id,
                je.description,
                CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END AS debit_amount,
                CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END AS credit_amount
            FROM ledger.journal_entry_line jel
            JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
            WHERE jel.account_id = @account_id
              AND je.voided_at IS NULL
              AND je.entry_date >= @from_date
              AND je.entry_date <= @to_date
            ORDER BY je.entry_date, je.id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@account_id", accountId) |> ignore
        sql.Parameters.AddWithValue("@from_date", fromDate) |> ignore
        sql.Parameters.AddWithValue("@to_date", toDate) |> ignore

        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <-
                { date = reader.GetFieldValue<DateOnly>(0)
                  journalEntryId = reader.GetInt32(1)
                  description = reader.GetString(2)
                  debitAmount = reader.GetDecimal(3)
                  creditAmount = reader.GetDecimal(4) } :: results
        reader.Close()
        List.rev results
