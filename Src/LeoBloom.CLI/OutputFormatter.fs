module LeoBloom.CLI.OutputFormatter

open System
open System.Text.Json
open System.Text.Json.Serialization
open LeoBloom.Domain.Ledger
open LeoBloom.Domain.Ops
open LeoBloom.Domain.Portfolio
open LeoBloom.Reporting.ReportingTypes

// --- JSON serialization options ---

let private jsonOptions =
    let opts = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    opts.Converters.Add(JsonFSharpConverter())
    opts

// --- Human-readable formatting ---

let private formatEntryHeader (e: JournalEntry) =
    let status =
        match e.voidedAt with
        | Some dt -> sprintf "VOIDED at %s (%s)" (dt.ToString("yyyy-MM-dd HH:mm:ss")) (e.voidReason |> Option.defaultValue "")
        | None -> "POSTED"
    [ sprintf "Journal Entry #%d  [%s]" e.id status
      sprintf "  Date:           %s" (e.entryDate.ToString("yyyy-MM-dd"))
      sprintf "  Description:    %s" e.description
      sprintf "  Source:         %s" (e.source |> Option.defaultValue "(none)")
      sprintf "  Fiscal Period:  %d" e.fiscalPeriodId
      sprintf "  Created:        %s" (e.createdAt.ToString("yyyy-MM-dd HH:mm:ss"))
      sprintf "  Modified:       %s" (e.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")) ]

let private formatLines (lines: JournalEntryLine list) =
    if lines.IsEmpty then []
    else
        let header = sprintf "  %-12s  %-10s  %-10s  %s" "Account" "Debit" "Credit" "Memo"
        let separator = sprintf "  %s  %s  %s  %s" (String.replicate 12 "-") (String.replicate 10 "-") (String.replicate 10 "-") (String.replicate 20 "-")
        let rows =
            lines
            |> List.map (fun l ->
                let debit = if l.entryType = EntryType.Debit then sprintf "%M" l.amount else ""
                let credit = if l.entryType = EntryType.Credit then sprintf "%M" l.amount else ""
                let memo = l.memo |> Option.defaultValue ""
                sprintf "  %-12d  %-10s  %-10s  %s" l.accountId debit credit memo)
        [ ""; "  Lines:" ] @ [ header; separator ] @ rows

let private formatReferences (refs: JournalEntryReference list) =
    if refs.IsEmpty then []
    else
        let rows =
            refs
            |> List.map (fun r -> sprintf "    %s: %s" r.referenceType r.referenceValue)
        [ ""; "  References:" ] @ rows

let formatPostedEntry (posted: PostedJournalEntry) : string =
    let parts =
        formatEntryHeader posted.entry
        @ formatLines posted.lines
        @ formatReferences posted.references
    String.Join(Environment.NewLine, parts)

let formatJournalEntry (entry: JournalEntry) : string =
    String.Join(Environment.NewLine, formatEntryHeader entry)

// --- Schedule E formatting ---

let private formatScheduleE (report: ScheduleEReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Schedule E — Supplemental Income and Loss (%d)" report.year)
    lines.Add(sprintf "  %-6s  %-45s  %12s" "Line" "Description" "Amount")
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 45 "-") (String.replicate 12 "-"))
    for item in report.lineItems do
        lines.Add(sprintf "  %-6d  %-45s  %12s" item.lineNumber item.description (sprintf "%M" item.amount))
        for (subDesc, subAmt) in item.subDetail do
            lines.Add(sprintf "  %6s    %-43s  %12s" "" subDesc (sprintf "%M" subAmt))
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 45 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-6s  %-45s  %12s" "" "Total Expenses" (sprintf "%M" report.totalExpenses))
    lines.Add(sprintf "  %-6s  %-45s  %12s" "" "Net Rental Income (Loss)" (sprintf "%M" report.netRentalIncome))
    String.Join(Environment.NewLine, lines)

// --- General Ledger formatting ---

let private formatGeneralLedger (report: GeneralLedgerReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "General Ledger — %s %s" report.accountCode report.accountName)
    lines.Add(sprintf "  Period: %s to %s" (report.fromDate.ToString("yyyy-MM-dd")) (report.toDate.ToString("yyyy-MM-dd")))
    lines.Add("")
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %12s  %12s  %12s" "Date" "Entry" "Description" "Debit" "Credit" "Balance")
    lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
    for entry in report.entries do
        let debit = if entry.debitAmount <> 0m then sprintf "%M" entry.debitAmount else ""
        let credit = if entry.creditAmount <> 0m then sprintf "%M" entry.creditAmount else ""
        let desc = if entry.description.Length > 30 then entry.description.Substring(0, 27) + "..." else entry.description
        lines.Add(sprintf "  %-12s  %-8d  %-30s  %12s  %12s  %12s"
            (entry.date.ToString("yyyy-MM-dd"))
            entry.journalEntryId
            desc
            debit credit
            (sprintf "%M" entry.runningBalance))
    lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %12s  %12s  %12s" "" "" "Ending Balance" "" "" (sprintf "%M" report.endingBalance))
    String.Join(Environment.NewLine, lines)

// --- Cash Receipts formatting ---

let private formatCashReceipts (report: CashReceiptsReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Cash Receipts — %s to %s" (report.fromDate.ToString("yyyy-MM-dd")) (report.toDate.ToString("yyyy-MM-dd")))
    lines.Add("")
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %-25s  %12s" "Date" "Entry" "Description" "Counterparty" "Amount")
    lines.Add(sprintf "  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 25 "-") (String.replicate 12 "-"))
    for entry in report.entries do
        let desc = if entry.description.Length > 30 then entry.description.Substring(0, 27) + "..." else entry.description
        let counter = if entry.counterpartyAccount.Length > 25 then entry.counterpartyAccount.Substring(0, 22) + "..." else entry.counterpartyAccount
        lines.Add(sprintf "  %-12s  %-8d  %-30s  %-25s  %12s"
            (entry.date.ToString("yyyy-MM-dd"))
            entry.journalEntryId
            desc counter
            (sprintf "%M" entry.amount))
    lines.Add(sprintf "  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 25 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %-25s  %12s" "" "" "Total Receipts" "" (sprintf "%M" report.totalReceipts))
    String.Join(Environment.NewLine, lines)

// --- Cash Disbursements formatting ---

let private formatCashDisbursements (report: CashDisbursementsReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Cash Disbursements — %s to %s" (report.fromDate.ToString("yyyy-MM-dd")) (report.toDate.ToString("yyyy-MM-dd")))
    lines.Add("")
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %-25s  %12s" "Date" "Entry" "Description" "Counterparty" "Amount")
    lines.Add(sprintf "  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 25 "-") (String.replicate 12 "-"))
    for entry in report.entries do
        let desc = if entry.description.Length > 30 then entry.description.Substring(0, 27) + "..." else entry.description
        let counter = if entry.counterpartyAccount.Length > 25 then entry.counterpartyAccount.Substring(0, 22) + "..." else entry.counterpartyAccount
        lines.Add(sprintf "  %-12s  %-8d  %-30s  %-25s  %12s"
            (entry.date.ToString("yyyy-MM-dd"))
            entry.journalEntryId
            desc counter
            (sprintf "%M" entry.amount))
    lines.Add(sprintf "  %s  %s  %s  %s  %s"
        (String.replicate 12 "-") (String.replicate 8 "-") (String.replicate 30 "-")
        (String.replicate 25 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-12s  %-8s  %-30s  %-25s  %12s" "" "" "Total Disbursements" "" (sprintf "%M" report.totalDisbursements))
    String.Join(Environment.NewLine, lines)

// --- Trial Balance formatting ---

let private formatTrialBalance (report: TrialBalanceReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Trial Balance -- Period %s (ID: %d)" report.periodKey report.fiscalPeriodId)
    lines.Add("")
    for group in report.groups do
        lines.Add(sprintf "  %s" (group.accountTypeName.ToUpper()))
        lines.Add(sprintf "  %-6s  %-32s  %12s  %12s" "Code" "Account Name" "Debit" "Credit")
        lines.Add(sprintf "  %s  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
        for line in group.lines do
            let debit = if line.debitTotal <> 0m then sprintf "%M" line.debitTotal else ""
            let credit = if line.creditTotal <> 0m then sprintf "%M" line.creditTotal else ""
            let name = if line.accountName.Length > 32 then line.accountName.Substring(0, 29) + "..." else line.accountName
            lines.Add(sprintf "  %-6s  %-32s  %12s  %12s" line.accountCode name debit credit)
        lines.Add(sprintf "  %s  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
        lines.Add(sprintf "  %-6s  %-32s  %12s  %12s" "" "Subtotal" (sprintf "%M" group.groupDebitTotal) (sprintf "%M" group.groupCreditTotal))
        lines.Add("")
    lines.Add(sprintf "  %-6s  %-32s  %12s  %12s" "" "Grand Total" (sprintf "%M" report.grandTotalDebits) (sprintf "%M" report.grandTotalCredits))
    let status = if report.isBalanced then "BALANCED" else "UNBALANCED"
    lines.Add(sprintf "  Status: %s" status)
    String.Join(Environment.NewLine, lines)

// --- Balance Sheet formatting ---

let private formatBalanceSheet (report: BalanceSheetReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Balance Sheet -- As of %s" (report.asOfDate.ToString("yyyy-MM-dd")))
    lines.Add("")
    // Assets
    lines.Add(sprintf "  ASSETS")
    lines.Add(sprintf "  %-6s  %-32s  %12s" "Code" "Account Name" "Balance")
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-"))
    for line in report.assets.lines do
        let name = if line.accountName.Length > 32 then line.accountName.Substring(0, 29) + "..." else line.accountName
        lines.Add(sprintf "  %-6s  %-32s  %12s" line.accountCode name (sprintf "%M" line.balance))
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" "Total Assets" (sprintf "%M" report.assets.sectionTotal))
    lines.Add("")
    // Liabilities
    lines.Add(sprintf "  LIABILITIES")
    lines.Add(sprintf "  %-6s  %-32s  %12s" "Code" "Account Name" "Balance")
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-"))
    for line in report.liabilities.lines do
        let name = if line.accountName.Length > 32 then line.accountName.Substring(0, 29) + "..." else line.accountName
        lines.Add(sprintf "  %-6s  %-32s  %12s" line.accountCode name (sprintf "%M" line.balance))
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" "Total Liabilities" (sprintf "%M" report.liabilities.sectionTotal))
    lines.Add("")
    // Equity
    lines.Add(sprintf "  EQUITY")
    lines.Add(sprintf "  %-6s  %-32s  %12s" "Code" "Account Name" "Balance")
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-"))
    for line in report.equity.lines do
        let name = if line.accountName.Length > 32 then line.accountName.Substring(0, 29) + "..." else line.accountName
        lines.Add(sprintf "  %-6s  %-32s  %12s" line.accountCode name (sprintf "%M" line.balance))
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" "Subtotal Equity Accounts" (sprintf "%M" report.equity.sectionTotal))
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" "Retained Earnings" (sprintf "%M" report.retainedEarnings))
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" "Total Equity" (sprintf "%M" report.totalEquity))
    lines.Add("")
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" "Total Liabilities + Equity" (sprintf "%M" (report.liabilities.sectionTotal + report.totalEquity)))
    let status = if report.isBalanced then "BALANCED" else "UNBALANCED"
    lines.Add(sprintf "  Status: %s" status)
    String.Join(Environment.NewLine, lines)

// --- Income Statement formatting ---

let private formatIncomeStatementSection (lines: ResizeArray<string>) (section: IncomeStatementSection) =
    lines.Add(sprintf "  %s" (section.sectionName.ToUpper()))
    lines.Add(sprintf "  %-6s  %-32s  %12s" "Code" "Account Name" "Amount")
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-"))
    for line in section.lines do
        let name = if line.accountName.Length > 32 then line.accountName.Substring(0, 29) + "..." else line.accountName
        lines.Add(sprintf "  %-6s  %-32s  %12s" line.accountCode name (sprintf "%M" line.balance))
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 6 "-") (String.replicate 32 "-") (String.replicate 12 "-"))
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" (sprintf "Total %s" section.sectionName) (sprintf "%M" section.sectionTotal))

let private formatIncomeStatement (report: IncomeStatementReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Income Statement -- Period %s (ID: %d)" report.periodKey report.fiscalPeriodId)
    lines.Add("")
    formatIncomeStatementSection lines report.revenue
    lines.Add("")
    formatIncomeStatementSection lines report.expenses
    lines.Add("")
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" "Net Income" (sprintf "%M" report.netIncome))
    String.Join(Environment.NewLine, lines)

// --- Subtree P&L formatting ---

let private formatSubtreePL (report: SubtreePLReport) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "P&L Subtree -- %s %s -- Period %s (ID: %d)" report.rootAccountCode report.rootAccountName report.periodKey report.fiscalPeriodId)
    lines.Add("")
    formatIncomeStatementSection lines report.revenue
    lines.Add("")
    formatIncomeStatementSection lines report.expenses
    lines.Add("")
    lines.Add(sprintf "  %-6s  %-32s  %12s" "" "Net Income" (sprintf "%M" report.netIncome))
    String.Join(Environment.NewLine, lines)

// --- Account detail formatting ---

let private formatAccount (a: Account) : string =
    let subTypeStr =
        match a.subType with
        | Some st -> AccountSubType.toDbString st
        | None -> "(none)"
    let activeStr = if a.isActive then "Active" else "Inactive"
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Account %s -- %s" a.code a.name)
    lines.Add(sprintf "  ID:            %d" a.id)
    lines.Add(sprintf "  Type ID:       %d" a.accountTypeId)
    lines.Add(sprintf "  Sub-Type:      %s" subTypeStr)
    lines.Add(sprintf "  Parent ID:     %s" (a.parentId |> Option.map string |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Status:        %s" activeStr)
    lines.Add(sprintf "  Created:       %s" (a.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:      %s" (a.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)

let private formatAccountList (accounts: Account list) : string =
    if accounts.IsEmpty then "(no accounts found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-10s  %-35s  %-8s  %-8s" "ID" "Code" "Name" "Type ID" "Active")
        lines.Add(sprintf "  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 10 "-")
            (String.replicate 35 "-") (String.replicate 8 "-") (String.replicate 8 "-"))
        for a in accounts do
            let name = if a.name.Length > 35 then a.name.Substring(0, 32) + "..." else a.name
            let active = if a.isActive then "Yes" else "No"
            lines.Add(sprintf "  %-6d  %-10s  %-35s  %-8d  %-8s"
                a.id a.code name a.accountTypeId active)
        String.Join(Environment.NewLine, lines)

// --- Fiscal Period detail formatting ---

let private formatFiscalPeriod (fp: FiscalPeriod) : string =
    let statusStr = if fp.isOpen then "Open" else "Closed"
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Fiscal Period %s (ID: %d)" fp.periodKey fp.id)
    lines.Add(sprintf "  Start Date:    %s" (fp.startDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  End Date:      %s" (fp.endDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  Status:        %s" statusStr)
    lines.Add(sprintf "  Created:       %s" (fp.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)

let private formatFiscalPeriodList (periods: FiscalPeriod list) : string =
    if periods.IsEmpty then "(no fiscal periods found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-10s  %-12s  %-12s  %-8s" "ID" "Key" "Start" "End" "Status")
        lines.Add(sprintf "  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 10 "-")
            (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 8 "-"))
        for fp in periods do
            let status = if fp.isOpen then "Open" else "Closed"
            lines.Add(sprintf "  %-6d  %-10s  %-12s  %-12s  %-8s"
                fp.id fp.periodKey
                (fp.startDate.ToString("yyyy-MM-dd"))
                (fp.endDate.ToString("yyyy-MM-dd"))
                status)
        String.Join(Environment.NewLine, lines)

// --- Account Balance formatting ---

let private formatAccountBalance (bal: AccountBalance) : string =
    let normalBalStr = match bal.normalBalance with | NormalBalance.Debit -> "Debit" | NormalBalance.Credit -> "Credit"
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Account Balance -- %s %s" bal.accountCode bal.accountName)
    lines.Add(sprintf "  As of:          %s" (bal.asOfDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  Normal Balance: %s" normalBalStr)
    lines.Add(sprintf "  Balance:        %M" bal.balance)
    String.Join(Environment.NewLine, lines)

// --- Invoice formatting ---

let private formatInvoice (inv: Invoice) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Invoice #%d" inv.id)
    lines.Add(sprintf "  Tenant:         %s" inv.tenant)
    lines.Add(sprintf "  Fiscal Period:  %d" inv.fiscalPeriodId)
    lines.Add(sprintf "  Rent Amount:    %M" inv.rentAmount)
    lines.Add(sprintf "  Utility Share:  %M" inv.utilityShare)
    lines.Add(sprintf "  Total Amount:   %M" inv.totalAmount)
    lines.Add(sprintf "  Generated At:   %s" (inv.generatedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Document Path:  %s" (inv.documentPath |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Notes:          %s" (inv.notes |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Created:        %s" (inv.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:       %s" (inv.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)

let private formatInvoiceList (invoices: Invoice list) : string =
    if invoices.IsEmpty then ""
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-15s  %-8s  %12s  %12s  %12s" "ID" "Tenant" "Period" "Rent" "Utility" "Total")
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 15 "-") (String.replicate 8 "-")
            (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
        for inv in invoices do
            lines.Add(sprintf "  %-6d  %-15s  %-8d  %12s  %12s  %12s"
                inv.id inv.tenant inv.fiscalPeriodId
                (sprintf "%M" inv.rentAmount) (sprintf "%M" inv.utilityShare) (sprintf "%M" inv.totalAmount))
        String.Join(Environment.NewLine, lines)

// --- Transfer formatting ---

let private formatTransfer (t: Transfer) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Transfer #%d" t.id)
    lines.Add(sprintf "  From Account:   %d" t.fromAccountId)
    lines.Add(sprintf "  To Account:     %d" t.toAccountId)
    lines.Add(sprintf "  Amount:         %M" t.amount)
    lines.Add(sprintf "  Status:         %s" (TransferStatus.toString t.status))
    lines.Add(sprintf "  Initiated:      %s" (t.initiatedDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  Exp. Settle:    %s" (t.expectedSettlement |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Confirmed:      %s" (t.confirmedDate |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Journal Entry:  %s" (t.journalEntryId |> Option.map string |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Description:    %s" (t.description |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Created:        %s" (t.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:       %s" (t.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)

let private formatTransferList (transfers: Transfer list) : string =
    if transfers.IsEmpty then ""
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-10s  %-8s  %-8s  %12s  %-12s" "ID" "Status" "From" "To" "Amount" "Initiated")
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 10 "-") (String.replicate 8 "-")
            (String.replicate 8 "-") (String.replicate 12 "-") (String.replicate 12 "-"))
        for t in transfers do
            lines.Add(sprintf "  %-6d  %-10s  %-8d  %-8d  %12s  %-12s"
                t.id (TransferStatus.toString t.status) t.fromAccountId t.toAccountId
                (sprintf "%M" t.amount) (t.initiatedDate.ToString("yyyy-MM-dd")))
        String.Join(Environment.NewLine, lines)

// --- Obligation Agreement formatting ---

let private formatObligationAgreement (a: ObligationAgreement) : string =
    let activeStr = if a.isActive then "Yes" else "No"
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Obligation Agreement #%d — %s" a.id a.name)
    lines.Add(sprintf "  Type:           %s" (ObligationDirection.toString a.obligationType))
    lines.Add(sprintf "  Cadence:        %s" (RecurrenceCadence.toString a.cadence))
    lines.Add(sprintf "  Counterparty:   %s" (a.counterparty |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Amount:         %s" (a.amount |> Option.map (sprintf "%M") |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Expected Day:   %s" (a.expectedDay |> Option.map string |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Payment Method: %s" (a.paymentMethod |> Option.map PaymentMethodType.toString |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Source Account: %s" (a.sourceAccountId |> Option.map string |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Dest Account:   %s" (a.destAccountId |> Option.map string |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Active:         %s" activeStr)
    lines.Add(sprintf "  Notes:          %s" (a.notes |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Created:        %s" (a.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:       %s" (a.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)

let private formatObligationAgreementList (agreements: ObligationAgreement list) : string =
    if agreements.IsEmpty then "(no agreements found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-35s  %-12s  %-10s  %-6s" "ID" "Name" "Type" "Cadence" "Active")
        lines.Add(sprintf "  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 35 "-")
            (String.replicate 12 "-") (String.replicate 10 "-") (String.replicate 6 "-"))
        for a in agreements do
            let name = if a.name.Length > 35 then a.name.Substring(0, 32) + "..." else a.name
            let active = if a.isActive then "Yes" else "No"
            lines.Add(sprintf "  %-6d  %-35s  %-12s  %-10s  %-6s"
                a.id name
                (ObligationDirection.toString a.obligationType)
                (RecurrenceCadence.toString a.cadence)
                active)
        String.Join(Environment.NewLine, lines)

// --- Obligation Instance formatting ---

let private formatObligationInstance (i: ObligationInstance) : string =
    let activeStr = if i.isActive then "Yes" else "No"
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Obligation Instance #%d — %s" i.id i.name)
    lines.Add(sprintf "  Agreement ID:   %d" i.obligationAgreementId)
    lines.Add(sprintf "  Status:         %s" (InstanceStatus.toString i.status))
    lines.Add(sprintf "  Amount:         %s" (i.amount |> Option.map (sprintf "%M") |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Expected Date:  %s" (i.expectedDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  Confirmed Date: %s" (i.confirmedDate |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Due Date:       %s" (i.dueDate |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Journal Entry:  %s" (i.journalEntryId |> Option.map string |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Document Path:  %s" (i.documentPath |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Notes:          %s" (i.notes |> Option.defaultValue "(none)"))
    lines.Add(sprintf "  Active:         %s" activeStr)
    lines.Add(sprintf "  Created:        %s" (i.createdAt.ToString("yyyy-MM-dd HH:mm:ss")))
    lines.Add(sprintf "  Modified:       %s" (i.modifiedAt.ToString("yyyy-MM-dd HH:mm:ss")))
    String.Join(Environment.NewLine, lines)

let private formatObligationInstanceList (instances: ObligationInstance list) : string =
    if instances.IsEmpty then "(no instances found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-6s  %-25s  %-10s  %-12s  %-10s" "ID" "AgrID" "Name" "Status" "Expected" "Amount")
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 6 "-") (String.replicate 25 "-")
            (String.replicate 10 "-") (String.replicate 12 "-") (String.replicate 10 "-"))
        for i in instances do
            let name = if i.name.Length > 25 then i.name.Substring(0, 22) + "..." else i.name
            let amount = i.amount |> Option.map (sprintf "%M") |> Option.defaultValue "(none)"
            lines.Add(sprintf "  %-6d  %-6d  %-25s  %-10s  %-12s  %-10s"
                i.id i.obligationAgreementId name
                (InstanceStatus.toString i.status)
                (i.expectedDate.ToString("yyyy-MM-dd"))
                amount)
        String.Join(Environment.NewLine, lines)

// --- Obligation result formatting ---

let private formatOverdueResult (r: OverdueDetectionResult) : string =
    let lines = ResizeArray<string>()
    if r.queryFailed then
        lines.Add("Overdue detection failed (query error).")
    else
        lines.Add(sprintf "Overdue detection complete.")
        lines.Add(sprintf "  Transitioned: %d" r.transitioned)
        lines.Add(sprintf "  Errors:       %d" r.errors.Length)
        if not r.errors.IsEmpty then
            lines.Add("  Error details:")
            for (instanceId, msg) in r.errors do
                lines.Add(sprintf "    Instance %d: %s" instanceId msg)
    String.Join(Environment.NewLine, lines)

let private formatSpawnResult (r: SpawnResult) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Spawned %d instance(s), %d skipped." r.created.Length r.skippedCount)
    if not r.created.IsEmpty then
        lines.Add("")
        lines.Add(formatObligationInstanceList r.created)
    String.Join(Environment.NewLine, lines)

let private formatPostToLedgerResult (r: PostToLedgerResult) : string =
    sprintf "Posted instance %d to journal entry %d." r.instanceId r.journalEntryId

// --- Balance Projection formatting ---

let private formatBalanceProjection (p: BalanceProjection) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Balance Projection: Account %s (%s)" p.accountCode p.accountName)
    lines.Add(sprintf "  Current balance as of %s: %M" (p.asOfDate.ToString("yyyy-MM-dd")) p.currentBalance)
    lines.Add(sprintf "  Projection through: %s" (p.projectionEndDate.ToString("yyyy-MM-dd")))
    lines.Add("")
    lines.Add(sprintf "  %-12s  %15s  %15s" "Date" "Balance" "Change")
    lines.Add(sprintf "  %s  %s  %s" (String.replicate 12 "-") (String.replicate 15 "-") (String.replicate 15 "-"))
    for day in p.days do
        if day.items.IsEmpty then
            lines.Add(sprintf "  %-12s  %15M  %15s"
                (day.date.ToString("yyyy-MM-dd")) day.closingBalance "")
        else
            let changeStr =
                if day.hasUnknownAmounts && day.knownNetChange = 0m then "[unknown]"
                elif day.hasUnknownAmounts then
                    sprintf "%+M + [unknown]" day.knownNetChange
                else
                    sprintf "%+M" day.knownNetChange
            lines.Add(sprintf "  %-12s  %15M  %15s"
                (day.date.ToString("yyyy-MM-dd")) day.closingBalance changeStr)
            for item in day.items do
                let itemStr =
                    match item.amount with
                    | None ->
                        let marker =
                            if item.direction = Inflow then "unknown inflow"
                            else "unknown outflow"
                        sprintf "    %s  %s: %s" (String.replicate 12 " ") item.description marker
                    | Some a ->
                        let sign = if item.direction = Inflow then "+" else "-"
                        sprintf "    %s  %s%M  %s" (String.replicate 12 " ") sign a item.description
                lines.Add(itemStr)
    String.Join(Environment.NewLine, lines)

// --- Portfolio formatting ---

let formatInvestmentAccount (a: InvestmentAccount) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Investment Account #%d — %s" a.id a.name)
    lines.Add(sprintf "  Tax Bucket ID:    %d" a.taxBucketId)
    lines.Add(sprintf "  Account Group ID: %d" a.accountGroupId)
    String.Join(Environment.NewLine, lines)

let private formatInvestmentAccountList (accounts: InvestmentAccount list) : string =
    if accounts.IsEmpty then "(no investment accounts found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-35s  %-12s  %-15s" "ID" "Name" "TaxBucketId" "AccountGroupId")
        lines.Add(sprintf "  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 35 "-")
            (String.replicate 12 "-") (String.replicate 15 "-"))
        for a in accounts do
            let name = if a.name.Length > 35 then a.name.Substring(0, 32) + "..." else a.name
            lines.Add(sprintf "  %-6d  %-35s  %-12d  %-15d" a.id name a.taxBucketId a.accountGroupId)
        String.Join(Environment.NewLine, lines)

let formatFund (f: Fund) : string =
    let optStr o = o |> Option.map string |> Option.defaultValue "(none)"
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Fund %s — %s" f.symbol f.name)
    lines.Add(sprintf "  Investment Type ID: %s" (optStr f.investmentTypeId))
    lines.Add(sprintf "  Market Cap ID:      %s" (optStr f.marketCapId))
    lines.Add(sprintf "  Index Type ID:      %s" (optStr f.indexTypeId))
    lines.Add(sprintf "  Sector ID:          %s" (optStr f.sectorId))
    lines.Add(sprintf "  Region ID:          %s" (optStr f.regionId))
    lines.Add(sprintf "  Objective ID:       %s" (optStr f.objectiveId))
    String.Join(Environment.NewLine, lines)

let private formatFundList (funds: Fund list) : string =
    if funds.IsEmpty then "(no funds found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-10s  %-40s  %-15s  %-12s" "Symbol" "Name" "InvTypeId" "MarketCapId")
        lines.Add(sprintf "  %s  %s  %s  %s"
            (String.replicate 10 "-") (String.replicate 40 "-")
            (String.replicate 15 "-") (String.replicate 12 "-"))
        for f in funds do
            let name = if f.name.Length > 40 then f.name.Substring(0, 37) + "..." else f.name
            let invType = f.investmentTypeId |> Option.map string |> Option.defaultValue ""
            let mktCap  = f.marketCapId      |> Option.map string |> Option.defaultValue ""
            lines.Add(sprintf "  %-10s  %-40s  %-15s  %-12s" f.symbol name invType mktCap)
        String.Join(Environment.NewLine, lines)

let formatPosition (p: Position) : string =
    let lines = ResizeArray<string>()
    lines.Add(sprintf "Position #%d" p.id)
    lines.Add(sprintf "  Account ID:   %d" p.investmentAccountId)
    lines.Add(sprintf "  Symbol:       %s" p.symbol)
    lines.Add(sprintf "  Date:         %s" (p.positionDate.ToString("yyyy-MM-dd")))
    lines.Add(sprintf "  Price:        %M" p.price)
    lines.Add(sprintf "  Quantity:     %M" p.quantity)
    lines.Add(sprintf "  Value:        %M" p.currentValue)
    lines.Add(sprintf "  Cost Basis:   %M" p.costBasis)
    String.Join(Environment.NewLine, lines)

let private formatPositionList (positions: Position list) : string =
    if positions.IsEmpty then "(no positions found)"
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "  %-6s  %-9s  %-10s  %-12s  %12s  %12s  %12s  %12s"
            "ID" "AccountId" "Symbol" "Date" "Price" "Qty" "Value" "CostBasis")
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s  %s  %s"
            (String.replicate 6 "-") (String.replicate 9 "-") (String.replicate 10 "-")
            (String.replicate 12 "-") (String.replicate 12 "-") (String.replicate 12 "-")
            (String.replicate 12 "-") (String.replicate 12 "-"))
        for p in positions do
            lines.Add(sprintf "  %-6d  %-9d  %-10s  %-12s  %12M  %12M  %12M  %12M"
                p.id p.investmentAccountId p.symbol
                (p.positionDate.ToString("yyyy-MM-dd"))
                p.price p.quantity p.currentValue p.costBasis)
        String.Join(Environment.NewLine, lines)

// --- Dispatch formatting based on type ---

let formatHuman (value: obj) : string =
    match value with
    | :? PostedJournalEntry as p -> formatPostedEntry p
    | :? JournalEntry as e -> formatJournalEntry e
    | :? ScheduleEReport as r -> formatScheduleE r
    | :? GeneralLedgerReport as r -> formatGeneralLedger r
    | :? CashReceiptsReport as r -> formatCashReceipts r
    | :? CashDisbursementsReport as r -> formatCashDisbursements r
    | :? Invoice as inv -> formatInvoice inv
    | :? Transfer as t -> formatTransfer t
    | :? TrialBalanceReport as r -> formatTrialBalance r
    | :? BalanceSheetReport as r -> formatBalanceSheet r
    | :? IncomeStatementReport as r -> formatIncomeStatement r
    | :? SubtreePLReport as r -> formatSubtreePL r
    | :? AccountBalance as b -> formatAccountBalance b
    | :? Account as a -> formatAccount a
    | :? FiscalPeriod as fp -> formatFiscalPeriod fp
    | :? ObligationAgreement as a -> formatObligationAgreement a
    | :? ObligationInstance as i -> formatObligationInstance i
    | :? InvestmentAccount as a -> formatInvestmentAccount a
    | :? Fund as f -> formatFund f
    | :? Position as p -> formatPosition p
    | _ -> sprintf "%A" value

let formatJson (value: obj) : string =
    JsonSerializer.Serialize(value, jsonOptions)

// --- Write Result to stdout/stderr ---

let write (isJson: bool) (result: Result<obj, string list>) : int =
    match result with
    | Ok value ->
        let output = if isJson then formatJson value else formatHuman value
        Console.Out.WriteLine(output)
        ExitCodes.success
    | Error errors ->
        if isJson then
            let json = JsonSerializer.Serialize({| errors = errors |}, jsonOptions)
            Console.Error.WriteLine(json)
        else
            for err in errors do
                Console.Error.WriteLine(sprintf "Error: %s" err)
        ExitCodes.businessError

// --- Write human-only Result to stdout/stderr (no --json support) ---

let writeHuman (result: Result<'a, string list>) : int =
    match result with
    | Ok value ->
        let output = formatHuman (value :> obj)
        Console.Out.WriteLine(output)
        ExitCodes.success
    | Error errors ->
        for err in errors do
            Console.Error.WriteLine(sprintf "Error: %s" err)
        ExitCodes.businessError

/// Dedicated write function for Invoice list to avoid F# type erasure
/// issues with generic list pattern matching in formatHuman.
let writeInvoiceList (isJson: bool) (invoices: Invoice list) : int =
    if isJson then
        let output = formatJson invoices
        Console.Out.WriteLine(output)
    else
        let output = formatInvoiceList invoices
        if not (String.IsNullOrEmpty output) then
            Console.Out.WriteLine(output)
    ExitCodes.success

/// Dedicated write function for Transfer list to avoid F# type erasure
/// issues with generic list pattern matching in formatHuman.
let writeTransferList (isJson: bool) (transfers: Transfer list) : int =
    if isJson then
        let output = formatJson transfers
        Console.Out.WriteLine(output)
    else
        let output = formatTransferList transfers
        if not (String.IsNullOrEmpty output) then
            Console.Out.WriteLine(output)
    ExitCodes.success

/// Dedicated write function for Account list to avoid F# type erasure
/// issues with generic list pattern matching in formatHuman.
let writeAccountList (isJson: bool) (accounts: Account list) : int =
    if isJson then
        let output = formatJson accounts
        Console.Out.WriteLine(output)
    else
        let output = formatAccountList accounts
        Console.Out.WriteLine(output)
    ExitCodes.success

/// Dedicated write function for FiscalPeriod list.
let writePeriodList (isJson: bool) (periods: FiscalPeriod list) : int =
    if isJson then
        let output = formatJson periods
        Console.Out.WriteLine(output)
    else
        let output = formatFiscalPeriodList periods
        Console.Out.WriteLine(output)
    ExitCodes.success

let writeHumanErrors (errors: string list) : int =
    for err in errors do
        Console.Error.WriteLine(sprintf "Error: %s" err)
    ExitCodes.businessError

/// Dedicated write function for ObligationAgreement list to avoid F# type erasure issues.
let writeAgreementList (isJson: bool) (agreements: ObligationAgreement list) : int =
    if isJson then
        Console.Out.WriteLine(formatJson agreements)
    else
        Console.Out.WriteLine(formatObligationAgreementList agreements)
    ExitCodes.success

/// Dedicated write function for ObligationInstance list to avoid F# type erasure issues.
let writeInstanceList (isJson: bool) (instances: ObligationInstance list) : int =
    if isJson then
        Console.Out.WriteLine(formatJson instances)
    else
        Console.Out.WriteLine(formatObligationInstanceList instances)
    ExitCodes.success

/// Dedicated write function for OverdueDetectionResult.
let writeOverdueResult (isJson: bool) (result: OverdueDetectionResult) : int =
    if isJson then
        Console.Out.WriteLine(formatJson result)
    else
        Console.Out.WriteLine(formatOverdueResult result)
    ExitCodes.success

/// Dedicated write function for SpawnResult.
let writeSpawnResult (isJson: bool) (result: SpawnResult) : int =
    if isJson then
        Console.Out.WriteLine(formatJson result)
    else
        Console.Out.WriteLine(formatSpawnResult result)
    ExitCodes.success

/// Dedicated write function for PostToLedgerResult.
let writePostResult (isJson: bool) (result: PostToLedgerResult) : int =
    if isJson then
        Console.Out.WriteLine(formatJson result)
    else
        Console.Out.WriteLine(formatPostToLedgerResult result)
    ExitCodes.success

// --- Orphaned Posting formatting ---

let private formatOrphanCondition (c: OrphanCondition) : string =
    match c with
    | OrphanCondition.DanglingStatus     -> "Dangling status"
    | OrphanCondition.MissingSource      -> "Missing source"
    | OrphanCondition.VoidedBackingEntry -> "Voided backing JE"
    | OrphanCondition.InvalidReference   -> "Invalid reference"

let private formatOrphanedPostings (result: OrphanedPostingResult) : string =
    if result.orphans.IsEmpty then
        "No orphaned postings found."
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "%d orphaned posting(s) found." result.orphans.Length)
        lines.Add("")
        lines.Add(sprintf "  %-12s  %-10s  %-8s  %-20s  %s"
            "Source Type" "Source ID" "JE ID" "Condition" "Reference")
        lines.Add(sprintf "  %s  %s  %s  %s  %s"
            (String.replicate 12 "-") (String.replicate 10 "-")
            (String.replicate 8 "-") (String.replicate 20 "-") (String.replicate 20 "-"))
        for o in result.orphans do
            let sourceIdStr =
                match o.sourceRecordId with
                | Some id -> string id
                | None    -> "\u2014"  // em dash
            lines.Add(sprintf "  %-12s  %-10s  %-8d  %-20s  %s"
                o.sourceType sourceIdStr o.journalEntryId
                (formatOrphanCondition o.condition) o.referenceValue)
        String.Join(Environment.NewLine, lines)

/// Dedicated write function for OrphanedPostingResult.
let writeOrphanedPostings (isJson: bool) (result: OrphanedPostingResult) : int =
    if isJson then
        Console.Out.WriteLine(formatJson result)
    else
        Console.Out.WriteLine(formatOrphanedPostings result)
    ExitCodes.success

/// Dedicated write function for BalanceProjection (human-only — no --json support).
let writeBalanceProjection (projection: BalanceProjection) : int =
    Console.Out.WriteLine(formatBalanceProjection projection)
    ExitCodes.success

/// Dedicated write function for InvestmentAccount list to avoid F# type erasure issues.
let writeInvestmentAccountList (isJson: bool) (accounts: InvestmentAccount list) : int =
    if isJson then
        Console.Out.WriteLine(formatJson accounts)
    else
        Console.Out.WriteLine(formatInvestmentAccountList accounts)
    ExitCodes.success

/// Dedicated write function for Fund list to avoid F# type erasure issues.
let writeFundList (isJson: bool) (funds: Fund list) : int =
    if isJson then
        Console.Out.WriteLine(formatJson funds)
    else
        Console.Out.WriteLine(formatFundList funds)
    ExitCodes.success

/// Dedicated write function for Position list to avoid F# type erasure issues.
let writePositionList (isJson: bool) (positions: Position list) : int =
    if isJson then
        Console.Out.WriteLine(formatJson positions)
    else
        Console.Out.WriteLine(formatPositionList positions)
    ExitCodes.success

// --- Portfolio Report formatting ---

let private formatAllocationReport (report: AllocationReport) : string =
    if report.rows.IsEmpty then
        sprintf "Allocation by %s\n  (no positions found)" report.dimension
    else
        let lines = ResizeArray<string>()
        lines.Add(sprintf "Allocation by %s" report.dimension)
        lines.Add("")
        lines.Add(sprintf "  %-35s  %14s  %8s" "Category" "Value" "Pct")
        lines.Add(sprintf "  %s  %s  %s" (String.replicate 35 "-") (String.replicate 14 "-") (String.replicate 8 "-"))
        for r in report.rows do
            let cat = if r.category.Length > 35 then r.category.Substring(0, 32) + "..." else r.category
            lines.Add(sprintf "  %-35s  %14M  %7.1f%%" cat r.currentValue r.percentage)
        lines.Add(sprintf "  %s  %s  %s" (String.replicate 35 "-") (String.replicate 14 "-") (String.replicate 8 "-"))
        lines.Add(sprintf "  %-35s  %14M  %7.1f%%" "Total" report.total 100.0)
        String.Join(Environment.NewLine, lines)

let private formatPortfolioSummary (summary: PortfolioSummary) : string =
    if summary.totalValue = 0m && summary.taxBucketBreakdown.IsEmpty then
        "Portfolio Summary\n  (no positions found)"
    else
        let lines = ResizeArray<string>()
        let glSign = if summary.unrealizedGainLoss >= 0m then "+" else ""
        lines.Add("Portfolio Summary")
        lines.Add("")
        lines.Add(sprintf "  Total Value:          %14M" summary.totalValue)
        lines.Add(sprintf "  Total Cost Basis:     %14M" summary.totalCostBasis)
        lines.Add(sprintf "  Unrealized Gain/Loss: %s%M  (%s%.2f%%)"
            glSign summary.unrealizedGainLoss glSign summary.unrealizedGainLossPct)
        lines.Add("")
        lines.Add("  Tax Bucket Breakdown")
        if summary.taxBucketBreakdown.IsEmpty then
            lines.Add("    (none)")
        else
            lines.Add(sprintf "    %-30s  %14s  %8s" "Bucket" "Value" "Pct")
            lines.Add(sprintf "    %s  %s  %s" (String.replicate 30 "-") (String.replicate 14 "-") (String.replicate 8 "-"))
            for r in summary.taxBucketBreakdown do
                let cat = if r.category.Length > 30 then r.category.Substring(0, 27) + "..." else r.category
                lines.Add(sprintf "    %-30s  %14M  %7.1f%%" cat r.currentValue r.percentage)
        lines.Add("")
        lines.Add("  Top 5 Holdings")
        if summary.topHoldings.IsEmpty then
            lines.Add("    (none)")
        else
            lines.Add(sprintf "    %-40s  %14s  %8s" "Fund" "Value" "Pct")
            lines.Add(sprintf "    %s  %s  %s" (String.replicate 40 "-") (String.replicate 14 "-") (String.replicate 8 "-"))
            for r in summary.topHoldings do
                let cat = if r.category.Length > 40 then r.category.Substring(0, 37) + "..." else r.category
                lines.Add(sprintf "    %-40s  %14M  %7.1f%%" cat r.currentValue r.percentage)
        String.Join(Environment.NewLine, lines)

let private formatPortfolioHistoryReport (report: PortfolioHistoryReport) : string =
    if report.rows.IsEmpty then
        sprintf "Portfolio History by %s\n  (no positions found)" report.dimension
    else
        // Collect all distinct categories in order of first appearance
        let allCats =
            report.rows
            |> List.collect (fun r -> r.categories |> List.map fst)
            |> List.distinct
        let colWidth = 14
        let lines = ResizeArray<string>()
        lines.Add(sprintf "Portfolio History by %s" report.dimension)
        lines.Add("")
        let headerCats = allCats |> List.map (fun c -> if c.Length > colWidth then c.Substring(0, colWidth - 3) + "..." else c)
        let header = sprintf "  %-12s  %s  %s" "Date" (headerCats |> List.map (sprintf "%*s" colWidth) |> String.concat "  ") (sprintf "%*s" colWidth "Total")
        lines.Add(header)
        let sep = sprintf "  %s  %s  %s" (String.replicate 12 "-") (allCats |> List.map (fun _ -> String.replicate colWidth "-") |> String.concat "  ") (String.replicate colWidth "-")
        lines.Add(sep)
        for row in report.rows do
            let catMap = row.categories |> Map.ofList
            let catCols = allCats |> List.map (fun cat ->
                let v = catMap |> Map.tryFind cat |> Option.defaultValue 0m
                sprintf "%*M" colWidth v)
            lines.Add(sprintf "  %-12s  %s  %*M"
                (row.positionDate.ToString("yyyy-MM-dd"))
                (catCols |> String.concat "  ")
                colWidth row.total)
        String.Join(Environment.NewLine, lines)

let private formatGainsReport (report: GainsReport) : string =
    if report.rows.IsEmpty then
        "Gains Report\n  (no positions found)"
    else
        let lines = ResizeArray<string>()
        lines.Add("Gains Report")
        lines.Add("")
        lines.Add(sprintf "  %-10s  %-35s  %14s  %14s  %14s  %8s"
            "Symbol" "Fund" "Cost Basis" "Value" "Gain/Loss" "Pct")
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
            (String.replicate 10 "-") (String.replicate 35 "-")
            (String.replicate 14 "-") (String.replicate 14 "-")
            (String.replicate 14 "-") (String.replicate 8 "-"))
        for r in report.rows do
            let name = if r.fundName.Length > 35 then r.fundName.Substring(0, 32) + "..." else r.fundName
            let glSign = if r.gainLoss >= 0m then "+" else ""
            lines.Add(sprintf "  %-10s  %-35s  %14M  %14M  %s%13M  %+7.2f%%"
                r.symbol name r.costBasis r.currentValue glSign r.gainLoss r.gainLossPct)
        lines.Add(sprintf "  %s  %s  %s  %s  %s  %s"
            (String.replicate 10 "-") (String.replicate 35 "-")
            (String.replicate 14 "-") (String.replicate 14 "-")
            (String.replicate 14 "-") (String.replicate 8 "-"))
        let totalGlSign = if report.totalGainLoss >= 0m then "+" else ""
        lines.Add(sprintf "  %-10s  %-35s  %14M  %14M  %s%13M  %+7.2f%%"
            "" "Total" report.totalCostBasis report.totalCurrentValue
            totalGlSign report.totalGainLoss report.totalGainLossPct)
        String.Join(Environment.NewLine, lines)

/// Dedicated write function for AllocationReport.
let writeAllocationReport (isJson: bool) (result: Result<AllocationReport, string list>) : int =
    match result with
    | Ok report ->
        if isJson then Console.Out.WriteLine(formatJson report)
        else Console.Out.WriteLine(formatAllocationReport report)
        ExitCodes.success
    | Error errors ->
        writeHumanErrors errors

/// Dedicated write function for PortfolioSummary.
let writePortfolioSummary (isJson: bool) (result: Result<PortfolioSummary, string list>) : int =
    match result with
    | Ok summary ->
        if isJson then Console.Out.WriteLine(formatJson summary)
        else Console.Out.WriteLine(formatPortfolioSummary summary)
        ExitCodes.success
    | Error errors ->
        writeHumanErrors errors

/// Dedicated write function for PortfolioHistoryReport.
let writePortfolioHistoryReport (isJson: bool) (result: Result<PortfolioHistoryReport, string list>) : int =
    match result with
    | Ok report ->
        if isJson then Console.Out.WriteLine(formatJson report)
        else Console.Out.WriteLine(formatPortfolioHistoryReport report)
        ExitCodes.success
    | Error errors ->
        writeHumanErrors errors

/// Dedicated write function for GainsReport.
let writeGainsReport (isJson: bool) (result: Result<GainsReport, string list>) : int =
    match result with
    | Ok report ->
        if isJson then Console.Out.WriteLine(formatJson report)
        else Console.Out.WriteLine(formatGainsReport report)
        ExitCodes.success
    | Error errors ->
        writeHumanErrors errors
