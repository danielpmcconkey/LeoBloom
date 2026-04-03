namespace LeoBloom.Domain

/// Ledger domain types — double-entry bookkeeping.
/// This file must appear before Ops.fs in the project file.
module Ledger =

    open System

    type EntryType = Debit | Credit

    type NormalBalance = Debit | Credit

    type AccountType =
        { id: int
          name: string
          normalBalance: NormalBalance }

    type Account =
        { id: int
          code: string
          name: string
          accountTypeId: int
          parentCode: string option
          isActive: bool
          createdAt: DateTimeOffset
          modifiedAt: DateTimeOffset }

    type FiscalPeriod =
        { id: int
          periodKey: string
          startDate: DateOnly
          endDate: DateOnly
          isOpen: bool
          createdAt: DateTimeOffset }

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
