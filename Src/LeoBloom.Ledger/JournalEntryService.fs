namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates validation and persistence for posting journal entries.
module JournalEntryService =

    // --- DB-dependent validation queries ---

    type FiscalPeriodCheck =
        { id: int; startDate: DateOnly; endDate: DateOnly; isOpen: bool }

    let lookupFiscalPeriod (txn: NpgsqlTransaction) (periodId: int) : FiscalPeriodCheck option =
        use sql = new NpgsqlCommand(
            "SELECT id, start_date, end_date, is_open FROM ledger.fiscal_period WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", periodId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result =
                Some { id = reader.GetInt32(0)
                       startDate = reader.GetFieldValue<DateOnly>(1)
                       endDate = reader.GetFieldValue<DateOnly>(2)
                       isOpen = reader.GetBoolean(3) }
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
                match JournalEntryRepository.voidEntry txn cmd.journalEntryId cmd.voidReason with
                | Some entry ->
                    Log.info "Voided journal entry {EntryId} successfully" [| entry.id :> obj |]
                    Ok entry
                | None ->
                    Log.warn "Journal entry {EntryId} not found for voiding" [| cmd.journalEntryId :> obj |]
                    Error [ sprintf "Journal entry with id %d does not exist" cmd.journalEntryId ]
            with ex ->
                Log.errorExn ex "Failed to void journal entry {EntryId}" [| cmd.journalEntryId :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]
