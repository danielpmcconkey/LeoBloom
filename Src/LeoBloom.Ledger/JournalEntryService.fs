namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates validation and persistence for posting journal entries.
module JournalEntryService =

    // --- DB-dependent validation queries ---

    type FiscalPeriodCheck =
        { id: int; startDate: DateOnly; endDate: DateOnly; isOpen: bool
          periodKey: string; closedAt: DateTimeOffset option }

    let lookupFiscalPeriod (txn: NpgsqlTransaction) (periodId: int) : FiscalPeriodCheck option =
        use sql = new NpgsqlCommand(
            "SELECT id, start_date, end_date, is_open, period_key, closed_at FROM ledger.fiscal_period WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", periodId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result =
                Some { id = reader.GetInt32(0)
                       startDate = reader.GetFieldValue<DateOnly>(1)
                       endDate = reader.GetFieldValue<DateOnly>(2)
                       isOpen = reader.GetBoolean(3)
                       periodKey = reader.GetString(4)
                       closedAt = if reader.IsDBNull(5) then None else Some (reader.GetFieldValue<DateTimeOffset>(5)) }
            reader.Close()
            result
        else
            reader.Close()
            None

    let lookupAccountActivity (txn: NpgsqlTransaction) (accountIds: int list) : (int * bool) list =
        if accountIds.IsEmpty then []
        else
            let paramNames =
                accountIds
                |> List.mapi (fun i _ -> sprintf "@a%d" i)
                |> String.concat ", "
            let query = sprintf "SELECT id, is_active FROM ledger.account WHERE id IN (%s)" paramNames
            use sql = new NpgsqlCommand(query, txn.Connection, txn)
            accountIds
            |> List.iteri (fun i aid ->
                sql.Parameters.AddWithValue(sprintf "@a%d" i, aid) |> ignore)
            use reader = sql.ExecuteReader()
            let mutable results = []
            while reader.Read() do
                results <- (reader.GetInt32(0), reader.GetBoolean(1)) :: results
            reader.Close()
            results

    let validateDbDependencies (txn: NpgsqlTransaction) (cmd: PostJournalEntryCommand) : Result<unit, string list> =
        let mutable errors = []

        // Fiscal period existence + open + date range
        match lookupFiscalPeriod txn cmd.fiscalPeriodId with
        | None ->
            errors <- errors @ [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
        | Some fp ->
            if not fp.isOpen then
                errors <- errors @ [ sprintf "Fiscal period '%d' is not open" fp.id ]
            if cmd.entryDate < fp.startDate || cmd.entryDate > fp.endDate then
                errors <- errors @ [ sprintf "Entry date %O falls outside fiscal period date range %O to %O" cmd.entryDate fp.startDate fp.endDate ]

        // Account existence + active check
        let accountIds = cmd.lines |> List.map (fun l -> l.accountId) |> List.distinct
        let found = lookupAccountActivity txn accountIds
        let foundIds = found |> List.map fst |> Set.ofList

        for aid in accountIds do
            if not (Set.contains aid foundIds) then
                errors <- errors @ [ sprintf "Account with id %d does not exist" aid ]

        for (aid, isActive) in found do
            if not isActive then
                errors <- errors @ [ sprintf "Account with id %d is inactive" aid ]

        // Adjustment period existence check (no open check — closed is fine)
        match cmd.adjustmentForPeriodId with
        | Some adjId ->
            match lookupFiscalPeriod txn adjId with
            | None ->
                errors <- errors @ [ sprintf "Adjustment fiscal period with id %d does not exist" adjId ]
            | Some _ -> ()
        | None -> ()

        if errors.IsEmpty then Ok () else Error errors

    /// Post a journal entry: validate, persist, return result.
    let post (txn: NpgsqlTransaction) (cmd: PostJournalEntryCommand) : Result<PostedJournalEntry, string list> =
        Log.info "Posting journal entry for date {EntryDate}, fiscal period {FiscalPeriodId}" [| cmd.entryDate :> obj; cmd.fiscalPeriodId :> obj |]
        // Phase 1: Pure validation (no DB)
        match validateCommand cmd with
        | Error errs ->
            Log.warn "Journal entry validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            // Phase 2: DB validation + persistence
            try
                match validateDbDependencies txn cmd with
                | Error errs ->
                    Log.warn "Journal entry DB validation failed: {Errors}" [| errs :> obj |]
                    Error errs
                | Ok () ->
                    let entry = JournalEntryRepository.insertEntry txn cmd
                    let lines = JournalEntryRepository.insertLines txn entry.id cmd.lines
                    let refs = JournalEntryRepository.insertReferences txn entry.id cmd.references
                    Log.info "Posted journal entry {EntryId} successfully" [| entry.id :> obj |]
                    Ok { entry = entry; lines = lines; references = refs }
            with ex ->
                Log.errorExn ex "Failed to persist journal entry" [||]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Retrieve a journal entry with its lines and references by ID.
    let getEntry (txn: NpgsqlTransaction) (entryId: int) : Result<PostedJournalEntry, string list> =
        Log.info "Retrieving journal entry {EntryId}" [| entryId :> obj |]
        try
            match JournalEntryRepository.getEntryById txn entryId with
            | Some posted ->
                Ok posted
            | None ->
                Error [ sprintf "Journal entry with id %d does not exist" entryId ]
        with ex ->
            Log.errorExn ex "Failed to retrieve journal entry {EntryId}" [| entryId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    // --- Void operations ---

    let private validateVoidCommand (cmd: VoidJournalEntryCommand) : Result<unit, string list> =
        if String.IsNullOrWhiteSpace cmd.voidReason then
            Error [ "Void reason is required and cannot be empty" ]
        else Ok ()

    /// Void a journal entry: validate, update, return result.
    let voidEntry (txn: NpgsqlTransaction) (cmd: VoidJournalEntryCommand) : Result<JournalEntry, string list> =
        Log.info "Voiding journal entry {EntryId}" [| cmd.journalEntryId :> obj |]
        match validateVoidCommand cmd with
        | Error errs ->
            Log.warn "Void validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            try
                // Pre-flight: load the entry to check voided status and fiscal period
                match JournalEntryRepository.getEntryById txn cmd.journalEntryId with
                | None ->
                    Log.warn "Journal entry {EntryId} not found for voiding" [| cmd.journalEntryId :> obj |]
                    Error [ sprintf "Journal entry with id %d does not exist" cmd.journalEntryId ]
                | Some posted ->
                    if posted.entry.voidedAt.IsSome then
                        Error [ sprintf "Journal entry %d is already voided" cmd.journalEntryId ]
                    else
                        // Check fiscal period status
                        match lookupFiscalPeriod txn posted.entry.fiscalPeriodId with
                        | None ->
                            Error [ sprintf "Fiscal period with id %d does not exist" posted.entry.fiscalPeriodId ]
                        | Some fp when not fp.isOpen ->
                            let closedAtStr =
                                match fp.closedAt with
                                | Some dt -> dt.ToString("yyyy-MM-dd")
                                | None -> "unknown"
                            Error [
                                sprintf "Cannot void JE %d — it belongs to closed period '%s' (closed %s).\n       Post a reversing entry in the current open period instead:\n       leobloom ledger reverse --journal-entry-id %d"
                                    cmd.journalEntryId fp.periodKey closedAtStr cmd.journalEntryId ]
                        | Some _ ->
                            // Proceed with void
                            match JournalEntryRepository.voidEntry txn cmd.journalEntryId cmd.voidReason with
                            | Some entry ->
                                Log.info "Voided journal entry {EntryId} successfully" [| entry.id :> obj |]
                                Ok entry
                            | None ->
                                Error [ sprintf "Journal entry with id %d does not exist" cmd.journalEntryId ]
            with ex ->
                Log.errorExn ex "Failed to void journal entry {EntryId}" [| cmd.journalEntryId :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    // --- Reversing entries ---

    /// Create a reversing entry for an existing journal entry.
    /// The reversal mirrors the original with debits and credits swapped.
    let reverseEntry (txn: NpgsqlTransaction) (entryId: int) (dateOverride: DateOnly option) : Result<PostedJournalEntry, string list> =
        Log.info "Reversing journal entry {EntryId}" [| entryId :> obj |]
        try
            // 1. Load original entry
            match JournalEntryRepository.getEntryById txn entryId with
            | None ->
                Error [ sprintf "Journal entry with id %d does not exist" entryId ]
            | Some original ->
                // 2. Reject if already voided
                if original.entry.voidedAt.IsSome then
                    Error [ sprintf "Journal entry %d is voided and cannot be reversed" entryId ]
                else
                    // 3. Idempotency guard — reject if reversal already exists
                    match JournalEntryRepository.findNonVoidedByReference txn "reversal" (string entryId) with
                    | Some existingId ->
                        Error [ sprintf "Journal entry %d already has a reversal (JE %d)" entryId existingId ]
                    | None ->
                        // 4. Determine entry date
                        let entryDate = dateOverride |> Option.defaultValue (DateOnly.FromDateTime(DateTime.Today))
                        // 5. Derive fiscal period from date
                        match FiscalPeriodRepository.findOpenPeriodForDate txn entryDate with
                        | None ->
                            Error [ sprintf "No open fiscal period covers date %O" entryDate ]
                        | Some period ->
                            // 6. Build reversed lines (swap Debit ↔ Credit)
                            let reversedLines =
                                original.lines
                                |> List.map (fun l ->
                                    { accountId = l.accountId
                                      amount = l.amount
                                      entryType =
                                          match l.entryType with
                                          | EntryType.Debit -> EntryType.Credit
                                          | EntryType.Credit -> EntryType.Debit
                                      memo = l.memo })
                            // 7. Build command
                            let cmd : PostJournalEntryCommand =
                                { entryDate = entryDate
                                  description = sprintf "Reversal of JE %d: %s" entryId original.entry.description
                                  source = Some "reversal"
                                  fiscalPeriodId = period.id
                                  lines = reversedLines
                                  references = [ { referenceType = "reversal"; referenceValue = string entryId } ]
                                  adjustmentForPeriodId = None }
                            // 8. Delegate to existing post
                            let result = post txn cmd
                            match result with
                            | Ok posted ->
                                Log.info "Reversed journal entry {EntryId} as new JE {NewId}" [| entryId :> obj; posted.entry.id :> obj |]
                            | Error errs ->
                                Log.warn "Reversal of {EntryId} failed: {Errors}" [| entryId :> obj; errs :> obj |]
                            result
        with ex ->
            Log.errorExn ex "Failed to reverse journal entry {EntryId}" [| entryId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]
