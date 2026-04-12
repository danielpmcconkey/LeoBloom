namespace LeoBloom.Reporting

open System
open Npgsql
open LeoBloom.Reporting.ExtractTypes

/// Repository functions for the four reporting data extracts.
/// All queries run within a caller-provided NpgsqlTransaction.
/// Void filtering uses INNER JOIN + WHERE on voided_at IS NULL (never LEFT JOIN).
module ExtractRepository =

    /// Extract the full account tree ordered by code.
    let getAccountTree (txn: NpgsqlTransaction) : AccountTreeRow list =
        use sql = new NpgsqlCommand(
            "SELECT a.id, a.code, a.name, a.parent_id,
                    at.name AS account_type, at.normal_balance,
                    a.account_subtype, a.is_active
             FROM ledger.account a
             JOIN ledger.account_type at ON a.account_type_id = at.id
             ORDER BY a.code",
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <-
                { id = reader.GetInt32(0)
                  code = reader.GetString(1)
                  name = reader.GetString(2)
                  parentId = if reader.IsDBNull(3) then None else Some (reader.GetInt32(3))
                  accountType = reader.GetString(4)
                  normalBalance = reader.GetString(5)
                  subtype = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
                  isActive = reader.GetBoolean(7) } :: results
        reader.Close()
        List.rev results

    /// Extract non-zero account balances (raw debit - credit) as of a given date.
    /// Excludes voided entries. Accounts with net-zero balance are omitted.
    let getBalances (txn: NpgsqlTransaction) (asOfDate: DateOnly) : AccountBalanceRow list =
        use sql = new NpgsqlCommand(
            "SELECT a.id AS account_id, a.code, a.name,
                    SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END)
                  - SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END) AS balance
             FROM ledger.journal_entry_line jel
             JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
             JOIN ledger.account a ON a.id = jel.account_id
             WHERE je.voided_at IS NULL
               AND je.entry_date <= @as_of_date
             GROUP BY a.id, a.code, a.name
             HAVING SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END)
                  - SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END) <> 0
             ORDER BY a.code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@as_of_date", asOfDate) |> ignore
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <-
                { accountId = reader.GetInt32(0)
                  code = reader.GetString(1)
                  name = reader.GetString(2)
                  balance = reader.GetDecimal(3) } :: results
        reader.Close()
        List.rev results

    /// Extract the latest portfolio position per (account, symbol) as of a given date.
    /// Excludes positions with current_value = 0 (fully liquidated).
    let getPositions (txn: NpgsqlTransaction) (asOfDate: DateOnly) : PortfolioPositionRow list =
        use sql = new NpgsqlCommand(
            "SELECT latest.investment_account_id, ia.name AS investment_account_name,
                    tb.name AS tax_bucket, latest.symbol, f.name AS fund_name,
                    latest.position_date, latest.price, latest.quantity,
                    latest.current_value, latest.cost_basis
             FROM (
                 SELECT DISTINCT ON (p.investment_account_id, p.symbol)
                        p.investment_account_id, p.symbol, p.position_date,
                        p.price, p.quantity, p.current_value, p.cost_basis
                 FROM portfolio.position p
                 WHERE p.position_date <= @as_of_date
                 ORDER BY p.investment_account_id, p.symbol, p.position_date DESC
             ) latest
             JOIN portfolio.investment_account ia ON ia.id = latest.investment_account_id
             JOIN portfolio.tax_bucket tb ON tb.id = ia.tax_bucket_id
             JOIN portfolio.fund f ON f.symbol = latest.symbol
             WHERE latest.current_value <> 0",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@as_of_date", asOfDate) |> ignore
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <-
                { investmentAccountId = reader.GetInt32(0)
                  investmentAccountName = reader.GetString(1)
                  taxBucket = reader.GetString(2)
                  symbol = reader.GetString(3)
                  fundName = reader.GetString(4)
                  positionDate = reader.GetFieldValue<DateOnly>(5)
                  price = reader.GetDecimal(6)
                  quantity = reader.GetDecimal(7)
                  currentValue = reader.GetDecimal(8)
                  costBasis = reader.GetDecimal(9) } :: results
        reader.Close()
        List.rev results

    /// Extract non-voided journal entry lines for a fiscal period.
    /// includeAdjustments: when true, also includes JEs with adjustment_for_period_id = fiscalPeriodId.
    /// closedAtCutoff: when Some, restricts results to JEs created at or before that timestamp.
    /// Ordered by account_code ASC, entry_date ASC, journal_entry_id ASC.
    let getJournalEntryLines (txn: NpgsqlTransaction) (fiscalPeriodId: int) (includeAdjustments: bool) (closedAtCutoff: DateTimeOffset option) : JournalEntryLineRow list =
        let periodFilter =
            if includeAdjustments then
                "(je.fiscal_period_id = @fiscal_period_id OR je.adjustment_for_period_id = @fiscal_period_id)"
            else
                "je.fiscal_period_id = @fiscal_period_id"
        let cutoffFilter =
            match closedAtCutoff with
            | None -> ""
            | Some _ -> "\n               AND je.created_at <= @closed_at"
        use sql = new NpgsqlCommand(
            sprintf "SELECT je.id AS journal_entry_id, je.entry_date, je.description,
                    je.source, jel.account_id, a.code AS account_code,
                    a.name AS account_name, jel.amount,
                    jel.entry_type, jel.memo
             FROM ledger.journal_entry_line jel
             JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
             JOIN ledger.account a ON a.id = jel.account_id
             WHERE je.voided_at IS NULL
               AND %s%s
             ORDER BY a.code, je.entry_date, je.id" periodFilter cutoffFilter,
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@fiscal_period_id", fiscalPeriodId) |> ignore
        match closedAtCutoff with
        | Some dt -> sql.Parameters.AddWithValue("@closed_at", dt) |> ignore
        | None -> ()
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <-
                { journalEntryId = reader.GetInt32(0)
                  entryDate = reader.GetFieldValue<DateOnly>(1)
                  description = reader.GetString(2)
                  source = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                  accountId = reader.GetInt32(4)
                  accountCode = reader.GetString(5)
                  accountName = reader.GetString(6)
                  amount = reader.GetDecimal(7)
                  entryType = reader.GetString(8)
                  memo = if reader.IsDBNull(9) then None else Some (reader.GetString(9)) } :: results
        reader.Close()
        List.rev results
