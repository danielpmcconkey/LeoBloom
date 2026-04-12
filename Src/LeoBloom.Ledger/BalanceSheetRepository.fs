namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Read-only queries for balance sheet calculation.
module BalanceSheetRepository =

    let getCumulativeBalances (txn: NpgsqlTransaction) (asOfDate: DateOnly) : (string * BalanceSheetLine) list =
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
            WHERE je.entry_date <= @as_of_date
              AND je.voided_at IS NULL
              AND at.name IN ('asset', 'liability', 'equity')
            GROUP BY a.id, a.code, a.name, at.name, at.normal_balance
            ORDER BY at.name, a.code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@as_of_date", asOfDate) |> ignore
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            let accountTypeName = reader.GetString(3)
            let nb = match reader.GetString(4) with "credit" -> NormalBalance.Credit | _ -> NormalBalance.Debit
            let debitTotal = reader.GetDecimal(5)
            let creditTotal = reader.GetDecimal(6)
            let balance = resolveBalance nb debitTotal creditTotal
            let line =
                { accountId = reader.GetInt32(0)
                  accountCode = reader.GetString(1)
                  accountName = reader.GetString(2)
                  balance = balance }
            results <- (accountTypeName, line) :: results
        reader.Close()
        results |> List.rev

    let getCumulativeBalancesAsOfClose (txn: NpgsqlTransaction) (asOfDate: DateOnly) (fiscalPeriodId: int) (closedAt: DateTimeOffset) : (string * BalanceSheetLine) list =
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
            WHERE je.entry_date <= @as_of_date
              AND je.voided_at IS NULL
              AND at.name IN ('asset', 'liability', 'equity')
              AND (
                je.fiscal_period_id != @fiscal_period_id
                OR (je.fiscal_period_id = @fiscal_period_id AND je.created_at <= @closed_at)
                OR (je.adjustment_for_period_id = @fiscal_period_id AND je.created_at <= @closed_at)
              )
            GROUP BY a.id, a.code, a.name, at.name, at.normal_balance
            ORDER BY at.name, a.code",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@as_of_date", asOfDate) |> ignore
        sql.Parameters.AddWithValue("@fiscal_period_id", fiscalPeriodId) |> ignore
        sql.Parameters.AddWithValue("@closed_at", closedAt) |> ignore
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            let accountTypeName = reader.GetString(3)
            let nb = match reader.GetString(4) with "credit" -> NormalBalance.Credit | _ -> NormalBalance.Debit
            let debitTotal = reader.GetDecimal(5)
            let creditTotal = reader.GetDecimal(6)
            let balance = resolveBalance nb debitTotal creditTotal
            let line =
                { accountId = reader.GetInt32(0)
                  accountCode = reader.GetString(1)
                  accountName = reader.GetString(2)
                  balance = balance }
            results <- (accountTypeName, line) :: results
        reader.Close()
        results |> List.rev

    let getRetainedEarnings (txn: NpgsqlTransaction) (asOfDate: DateOnly) : decimal =
        use sql = new NpgsqlCommand(
            "SELECT
                at.name AS account_type_name,
                COALESCE(SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END), 0) AS debit_total,
                COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0) AS credit_total
            FROM ledger.journal_entry je
            JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
            JOIN ledger.account a ON jel.account_id = a.id
            JOIN ledger.account_type at ON a.account_type_id = at.id
            WHERE je.entry_date <= @as_of_date
              AND je.voided_at IS NULL
              AND at.name IN ('revenue', 'expense')
            GROUP BY at.name",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@as_of_date", asOfDate) |> ignore
        use reader = sql.ExecuteReader()
        let mutable revenueNet = 0m
        let mutable expenseNet = 0m
        while reader.Read() do
            let typeName = reader.GetString(0)
            let debitTotal = reader.GetDecimal(1)
            let creditTotal = reader.GetDecimal(2)
            match typeName with
            | "revenue" -> revenueNet <- resolveBalance NormalBalance.Credit debitTotal creditTotal
            | "expense" -> expenseNet <- resolveBalance NormalBalance.Debit debitTotal creditTotal
            | _ -> ()
        reader.Close()
        revenueNet - expenseNet

    let getRetainedEarningsAsOfClose (txn: NpgsqlTransaction) (asOfDate: DateOnly) (fiscalPeriodId: int) (closedAt: DateTimeOffset) : decimal =
        use sql = new NpgsqlCommand(
            "SELECT
                at.name AS account_type_name,
                COALESCE(SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END), 0) AS debit_total,
                COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0) AS credit_total
            FROM ledger.journal_entry je
            JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id
            JOIN ledger.account a ON jel.account_id = a.id
            JOIN ledger.account_type at ON a.account_type_id = at.id
            WHERE je.entry_date <= @as_of_date
              AND je.voided_at IS NULL
              AND at.name IN ('revenue', 'expense')
              AND (
                je.fiscal_period_id != @fiscal_period_id
                OR (je.fiscal_period_id = @fiscal_period_id AND je.created_at <= @closed_at)
                OR (je.adjustment_for_period_id = @fiscal_period_id AND je.created_at <= @closed_at)
              )
            GROUP BY at.name",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@as_of_date", asOfDate) |> ignore
        sql.Parameters.AddWithValue("@fiscal_period_id", fiscalPeriodId) |> ignore
        sql.Parameters.AddWithValue("@closed_at", closedAt) |> ignore
        use reader = sql.ExecuteReader()
        let mutable revenueNet = 0m
        let mutable expenseNet = 0m
        while reader.Read() do
            let typeName = reader.GetString(0)
            let debitTotal = reader.GetDecimal(1)
            let creditTotal = reader.GetDecimal(2)
            match typeName with
            | "revenue" -> revenueNet <- resolveBalance NormalBalance.Credit debitTotal creditTotal
            | "expense" -> expenseNet <- resolveBalance NormalBalance.Debit debitTotal creditTotal
            | _ -> ()
        reader.Close()
        revenueNet - expenseNet
