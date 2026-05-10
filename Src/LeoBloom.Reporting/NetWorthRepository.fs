namespace LeoBloom.Reporting

open System
open Npgsql

module NetWorthRepository =

    type InvestmentPositionRow =
        { investmentAccountName: string
          taxBucketName: string
          symbol: string
          fundName: string
          investmentTypeName: string
          currentValue: decimal }

    type AccountBalanceRow =
        { accountName: string
          balance: decimal }

    let getInvestmentPositions (txn: NpgsqlTransaction) (asOfDate: DateOnly) : InvestmentPositionRow list =
        let sql = "
            SELECT DISTINCT ON (p.investment_account_id, p.symbol)
                   ia.name AS investment_account_name,
                   tb.name AS tax_bucket_name,
                   p.symbol,
                   f.name AS fund_name,
                   it.name AS investment_type_name,
                   p.current_value
            FROM portfolio.position p
            JOIN portfolio.investment_account ia ON ia.id = p.investment_account_id
            JOIN portfolio.tax_bucket tb ON tb.id = ia.tax_bucket_id
            JOIN portfolio.fund f ON f.symbol = p.symbol
            LEFT JOIN portfolio.dim_investment_type it ON it.id = f.investment_type_id
            WHERE p.position_date <= @as_of
            ORDER BY p.investment_account_id, p.symbol, p.position_date DESC, p.id DESC"
        use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
        cmd.Parameters.AddWithValue("@as_of", asOfDate) |> ignore
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<InvestmentPositionRow>()
        while reader.Read() do
            if reader.GetDecimal(5) > 0m then
                results.Add({
                    investmentAccountName = reader.GetString(0)
                    taxBucketName = reader.GetString(1)
                    symbol = reader.GetString(2)
                    fundName = reader.GetString(3)
                    investmentTypeName = if reader.IsDBNull(4) then "" else reader.GetString(4)
                    currentValue = reader.GetDecimal(5)
                })
        reader.Close()
        results |> Seq.toList

    let getCashBalances (txn: NpgsqlTransaction) (asOfDate: DateOnly) : AccountBalanceRow list =
        let sql = "
            SELECT a.name,
                   SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE -jel.amount END) as balance
            FROM ledger.account a
            JOIN ledger.journal_entry_line jel ON jel.account_id = a.id
            JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
            WHERE a.account_subtype = 'Cash'
              AND a.is_active = true
              AND je.voided_at IS NULL
              AND je.entry_date <= @as_of
            GROUP BY a.name
            HAVING SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE -jel.amount END) > 0
            ORDER BY balance DESC"
        use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
        cmd.Parameters.AddWithValue("@as_of", asOfDate) |> ignore
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<AccountBalanceRow>()
        while reader.Read() do
            results.Add({
                accountName = reader.GetString(0)
                balance = reader.GetDecimal(1)
            })
        reader.Close()
        results |> Seq.toList

    let getLiabilityBalances (txn: NpgsqlTransaction) (asOfDate: DateOnly) : AccountBalanceRow list =
        let sql = "
            SELECT a.name,
                   SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE -jel.amount END) as balance
            FROM ledger.account a
            JOIN ledger.account_type at ON at.id = a.account_type_id
            JOIN ledger.journal_entry_line jel ON jel.account_id = a.id
            JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
            WHERE at.name = 'liability'
              AND a.is_active = true
              AND a.account_subtype IS NOT NULL
              AND je.voided_at IS NULL
              AND je.entry_date <= @as_of
            GROUP BY a.name
            HAVING SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE -jel.amount END) > 0
            ORDER BY balance DESC"
        use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
        cmd.Parameters.AddWithValue("@as_of", asOfDate) |> ignore
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<AccountBalanceRow>()
        while reader.Read() do
            results.Add({
                accountName = reader.GetString(0)
                balance = reader.GetDecimal(1)
            })
        reader.Close()
        results |> Seq.toList

    let getFrozenAssetBalances (txn: NpgsqlTransaction) (asOfDate: DateOnly) : AccountBalanceRow list =
        let sql = "
            SELECT a.name,
                   SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE -jel.amount END) as balance
            FROM ledger.account a
            JOIN ledger.journal_entry_line jel ON jel.account_id = a.id
            JOIN ledger.journal_entry je ON je.id = jel.journal_entry_id
            WHERE a.code = '1150'
              AND a.is_active = true
              AND je.voided_at IS NULL
              AND je.entry_date <= @as_of
            GROUP BY a.name
            HAVING SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE -jel.amount END) > 0
            ORDER BY balance DESC"
        use cmd = new NpgsqlCommand(sql, txn.Connection, txn)
        cmd.Parameters.AddWithValue("@as_of", asOfDate) |> ignore
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<AccountBalanceRow>()
        while reader.Read() do
            results.Add({
                accountName = reader.GetString(0)
                balance = reader.GetDecimal(1)
            })
        reader.Close()
        results |> Seq.toList
