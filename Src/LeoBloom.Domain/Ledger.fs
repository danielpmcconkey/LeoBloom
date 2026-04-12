namespace LeoBloom.Domain

/// Ledger domain types — double-entry bookkeeping.
/// This file must appear before Ops.fs in the project file.
module Ledger =

    open System

    type EntryType = Debit | Credit

    type NormalBalance = Debit | Credit

    let resolveBalance (normalBalance: NormalBalance) (debits: decimal) (credits: decimal) : decimal =
        match normalBalance with
        | NormalBalance.Debit  -> debits - credits
        | NormalBalance.Credit -> credits - debits

    type AccountType =
        { id: int
          name: string
          normalBalance: NormalBalance }

    type AccountSubType =
        | Cash
        | FixedAsset
        | Investment
        | CurrentLiability
        | LongTermLiability
        | OperatingRevenue
        | OtherRevenue
        | OperatingExpense
        | OtherExpense

    module AccountSubType =
        let toDbString (st: AccountSubType) : string =
            match st with
            | Cash -> "Cash"
            | FixedAsset -> "FixedAsset"
            | Investment -> "Investment"
            | CurrentLiability -> "CurrentLiability"
            | LongTermLiability -> "LongTermLiability"
            | OperatingRevenue -> "OperatingRevenue"
            | OtherRevenue -> "OtherRevenue"
            | OperatingExpense -> "OperatingExpense"
            | OtherExpense -> "OtherExpense"

        let fromDbString (s: string) : Result<AccountSubType, string> =
            match s with
            | "Cash" -> Ok Cash
            | "FixedAsset" -> Ok FixedAsset
            | "Investment" -> Ok Investment
            | "CurrentLiability" -> Ok CurrentLiability
            | "LongTermLiability" -> Ok LongTermLiability
            | "OperatingRevenue" -> Ok OperatingRevenue
            | "OtherRevenue" -> Ok OtherRevenue
            | "OperatingExpense" -> Ok OperatingExpense
            | "OtherExpense" -> Ok OtherExpense
            | _ -> Error (sprintf "Invalid account_subtype: '%s'" s)

        /// Returns the list of valid subtypes for a given account type name.
        let validSubTypesForAccountType (accountTypeName: string) : AccountSubType list =
            match accountTypeName.ToLowerInvariant() with
            | "asset" -> [ Cash; FixedAsset; Investment ]
            | "liability" -> [ CurrentLiability; LongTermLiability ]
            | "equity" -> []
            | "revenue" -> [ OperatingRevenue; OtherRevenue ]
            | "expense" -> [ OperatingExpense; OtherExpense ]
            | _ -> []

        /// Validates that a subtype is valid for the given account type name.
        /// None (null subtype) is always valid for any account type.
        let isValidSubType (accountTypeName: string) (subType: AccountSubType option) : bool =
            match subType with
            | None -> true
            | Some st -> validSubTypesForAccountType accountTypeName |> List.contains st

    type Account =
        { id: int
          code: string
          name: string
          accountTypeId: int
          parentId: int option
          subType: AccountSubType option
          externalRef: string option
          isActive: bool
          createdAt: DateTimeOffset
          modifiedAt: DateTimeOffset }

    type FiscalPeriod =
        { id: int
          periodKey: string
          startDate: DateOnly
          endDate: DateOnly
          isOpen: bool
          closedAt: DateTimeOffset option
          closedBy: string option
          reopenedCount: int
          createdAt: DateTimeOffset }

    type FiscalPeriodAuditEntry =
        { id: int
          fiscalPeriodId: int
          action: string
          actor: string
          occurredAt: DateTimeOffset
          note: string option }

    type JournalEntry =
        { id: int
          entryDate: DateOnly
          description: string
          source: string option
          fiscalPeriodId: int
          voidedAt: DateTimeOffset option
          voidReason: string option
          createdAt: DateTimeOffset
          modifiedAt: DateTimeOffset }

    type JournalEntryReference =
        { id: int
          journalEntryId: int
          referenceType: string
          referenceValue: string
          createdAt: DateTimeOffset }

    type JournalEntryLine =
        { id: int
          journalEntryId: int
          accountId: int
          amount: decimal
          entryType: EntryType
          memo: string option }

    let validateBalanced (lines: JournalEntryLine list) : Result<JournalEntryLine list, string list> =
        let debits =
            lines
            |> List.filter (fun l -> l.entryType = EntryType.Debit)
            |> List.sumBy (fun l -> l.amount)
        let credits =
            lines
            |> List.filter (fun l -> l.entryType = EntryType.Credit)
            |> List.sumBy (fun l -> l.amount)
        if debits = credits then Ok lines
        else Error [ sprintf "Debits (%M) do not equal credits (%M)" debits credits ]

    let validateAmountsPositive (lines: JournalEntryLine list) : Result<JournalEntryLine list, string list> =
        let errors =
            lines
            |> List.filter (fun l -> l.amount <= 0m)
            |> List.map (fun l -> sprintf "Line for account %d has non-positive amount: %M" l.accountId l.amount)
        if errors.IsEmpty then Ok lines
        else Error errors

    let validateMinimumLineCount (lines: JournalEntryLine list) : Result<JournalEntryLine list, string list> =
        if List.length lines >= 2 then Ok lines
        else Error [ sprintf "Journal entry must have at least 2 lines, got %d" (List.length lines) ]

    let validateVoidReason (voidedAt: DateTimeOffset option) (voidReason: string option) : Result<unit, string list> =
        match voidedAt, voidReason with
        | None, _ -> Ok ()
        | Some _, Some reason when not (String.IsNullOrWhiteSpace reason) -> Ok ()
        | Some _, None -> Error [ "Void reason is required when entry is voided" ]
        | Some _, Some _ -> Error [ "Void reason cannot be empty when entry is voided" ]

    // --- Command DTOs for the write path ---

    type PostLineCommand =
        { accountId: int
          amount: decimal
          entryType: EntryType
          memo: string option }

    type PostReferenceCommand =
        { referenceType: string
          referenceValue: string }

    type PostJournalEntryCommand =
        { entryDate: DateOnly
          description: string
          source: string option
          fiscalPeriodId: int
          lines: PostLineCommand list
          references: PostReferenceCommand list }

    type PostedJournalEntry =
        { entry: JournalEntry
          lines: JournalEntryLine list
          references: JournalEntryReference list }

    type VoidJournalEntryCommand =
        { journalEntryId: int
          voidReason: string }

    // --- Opening Balances types ---

    type OpeningBalanceEntry =
        { accountId: int
          balance: decimal }

    type PostOpeningBalancesCommand =
        { entryDate: DateOnly
          fiscalPeriodId: int
          balancingAccountId: int
          entries: OpeningBalanceEntry list
          description: string option }

    type CreateAccountCommand =
        { code: string; name: string; accountTypeId: int; parentId: int option; subType: AccountSubType option; externalRef: string option }

    /// Update mutable fields on an existing account.
    /// None on each field means "don't change this field."
    type UpdateAccountCommand =
        { accountId: int
          name: string option
          subType: AccountSubType option
          externalRef: string option }

    type UpdateAccountNameCommand =
        { accountId: int; name: string }

    type DeactivateAccountCommand =
        { accountId: int }

    type CloseFiscalPeriodCommand =
        { fiscalPeriodId: int
          actor: string
          note: string option }

    type ReopenFiscalPeriodCommand =
        { fiscalPeriodId: int
          reason: string
          actor: string }

    let validateReopenReason (reason: string) : Result<unit, string list> =
        if String.IsNullOrWhiteSpace reason then
            Error [ "Reopen reason is required and cannot be empty" ]
        else Ok ()

    type AccountBalance =
        { accountId: int
          accountCode: string
          accountName: string
          normalBalance: NormalBalance
          balance: decimal
          asOfDate: DateOnly }

    type TrialBalanceAccountLine =
        { accountId: int
          accountCode: string
          accountName: string
          accountTypeName: string
          normalBalance: NormalBalance
          debitTotal: decimal
          creditTotal: decimal
          netBalance: decimal }

    type TrialBalanceGroup =
        { accountTypeName: string
          lines: TrialBalanceAccountLine list
          groupDebitTotal: decimal
          groupCreditTotal: decimal }

    type TrialBalanceReport =
        { fiscalPeriodId: int
          periodKey: string
          groups: TrialBalanceGroup list
          grandTotalDebits: decimal
          grandTotalCredits: decimal
          isBalanced: bool }

    // --- Income Statement types ---

    type IncomeStatementLine =
        { accountId: int
          accountCode: string
          accountName: string
          balance: decimal }

    type IncomeStatementSection =
        { sectionName: string
          lines: IncomeStatementLine list
          sectionTotal: decimal }

    type IncomeStatementReport =
        { fiscalPeriodId: int
          periodKey: string
          revenue: IncomeStatementSection
          expenses: IncomeStatementSection
          netIncome: decimal }

    // --- Balance Sheet types ---

    type BalanceSheetLine =
        { accountId: int
          accountCode: string
          accountName: string
          balance: decimal }

    type BalanceSheetSection =
        { sectionName: string
          lines: BalanceSheetLine list
          sectionTotal: decimal }

    type BalanceSheetReport =
        { asOfDate: DateOnly
          assets: BalanceSheetSection
          liabilities: BalanceSheetSection
          equity: BalanceSheetSection
          retainedEarnings: decimal
          totalEquity: decimal
          isBalanced: bool }

    // --- Subtree P&L types ---

    type SubtreePLReport =
        { rootAccountCode: string
          rootAccountName: string
          fiscalPeriodId: int
          periodKey: string
          revenue: IncomeStatementSection
          expenses: IncomeStatementSection
          netIncome: decimal }

    // --- Additional pure validators for the write path ---

    module EntryType =
        let fromString (s: string) : Result<EntryType, string> =
            match s.ToLowerInvariant() with
            | "debit" -> Ok EntryType.Debit
            | "credit" -> Ok EntryType.Credit
            | _ -> Error (sprintf "Invalid entry_type: '%s'. Must be 'debit' or 'credit'" s)

        let toDbString (et: EntryType) =
            match et with
            | EntryType.Debit -> "debit"
            | EntryType.Credit -> "credit"

    let validateDescription (desc: string) : Result<unit, string list> =
        if String.IsNullOrWhiteSpace desc then
            Error [ "Description is required and cannot be empty" ]
        else Ok ()

    let validateSource (source: string option) : Result<unit, string list> =
        match source with
        | None -> Ok ()
        | Some s when String.IsNullOrWhiteSpace s ->
            Error [ "Source cannot be empty when provided" ]
        | Some _ -> Ok ()

    let validateReferences (refs: PostReferenceCommand list) : Result<unit, string list> =
        let errors =
            refs
            |> List.collect (fun r ->
                [ if String.IsNullOrWhiteSpace r.referenceType then
                      "reference_type cannot be empty"
                  if String.IsNullOrWhiteSpace r.referenceValue then
                      "reference_value cannot be empty" ])
        if errors.IsEmpty then Ok () else Error errors

    /// Runs all pure (no-DB) validations. Collects all errors.
    let validateCommand (cmd: PostJournalEntryCommand) : Result<unit, string list> =
        let toLines =
            cmd.lines
            |> List.map (fun l ->
                { id = 0; journalEntryId = 0; accountId = l.accountId
                  amount = l.amount; entryType = l.entryType; memo = l.memo })

        let lineErrors =
            [ validateMinimumLineCount toLines
              validateAmountsPositive toLines
              validateBalanced toLines ]
            |> List.collect (function Error errs -> errs | Ok _ -> [])

        let headerErrors =
            [ validateDescription cmd.description
              validateSource cmd.source
              validateReferences cmd.references ]
            |> List.collect (function Error errs -> errs | Ok _ -> [])

        let allErrors = headerErrors @ lineErrors
        if allErrors.IsEmpty then Ok () else Error allErrors
