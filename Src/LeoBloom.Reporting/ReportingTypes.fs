namespace LeoBloom.Reporting

open System

/// Domain types for tax reports: Schedule E, General Ledger detail,
/// Cash Receipts, and Cash Disbursements.
module ReportingTypes =

    // --- Schedule E types ---

    type ScheduleELineItem =
        { lineNumber: int
          description: string
          amount: decimal
          subDetail: (string * decimal) list }

    type ScheduleEReport =
        { year: int
          lineItems: ScheduleELineItem list
          totalExpenses: decimal
          netRentalIncome: decimal }

    // --- General Ledger detail types ---

    type GeneralLedgerEntry =
        { date: DateOnly
          journalEntryId: int
          description: string
          debitAmount: decimal
          creditAmount: decimal
          runningBalance: decimal }

    type GeneralLedgerReport =
        { accountCode: string
          accountName: string
          fromDate: DateOnly
          toDate: DateOnly
          entries: GeneralLedgerEntry list
          endingBalance: decimal }

    // --- Cash Receipts types ---

    type CashReceiptEntry =
        { date: DateOnly
          journalEntryId: int
          description: string
          counterpartyAccount: string
          amount: decimal }

    type CashReceiptsReport =
        { fromDate: DateOnly
          toDate: DateOnly
          entries: CashReceiptEntry list
          totalReceipts: decimal }

    // --- Cash Disbursements types ---

    type CashDisbursementEntry =
        { date: DateOnly
          journalEntryId: int
          description: string
          counterpartyAccount: string
          amount: decimal }

    type CashDisbursementsReport =
        { fromDate: DateOnly
          toDate: DateOnly
          entries: CashDisbursementEntry list
          totalDisbursements: decimal }
