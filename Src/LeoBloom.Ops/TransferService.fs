namespace LeoBloom.Ops

open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger

/// Orchestrates initiation and confirmation of transfers between asset accounts.
/// Each public function opens its own connection(s) + transaction(s).
module TransferService =

    let private lookupAccountInfo (txn: NpgsqlTransaction) (accountId: int) : (bool * string) option =
        use sql = new NpgsqlCommand(
            "SELECT a.is_active, at.name \
             FROM ledger.account a \
             JOIN ledger.account_type at ON a.account_type_id = at.id \
             WHERE a.id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = Some (reader.GetBoolean(0), reader.GetString(1))
            reader.Close()
            result
        else
            reader.Close()
            None

    let initiate (cmd: InitiateTransferCommand) : Result<Transfer, string list> =
        Log.info "Initiating transfer from account {FromId} to account {ToId} for {Amount}"
            [| cmd.fromAccountId :> obj; cmd.toAccountId :> obj; cmd.amount :> obj |]

        // Phase 1: Pure validation
        let pureErrors =
            [ if cmd.amount <= 0m then
                  "Transfer amount must be greater than zero"
              if cmd.fromAccountId = cmd.toAccountId then
                  "Cannot transfer to the same account" ]

        if not pureErrors.IsEmpty then
            Log.warn "Transfer initiation validation failed: {Errors}" [| pureErrors :> obj |]
            Error pureErrors
        else
            // Phase 2: DB validation + insert
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()

            try
                let fromInfo = lookupAccountInfo txn cmd.fromAccountId
                let toInfo = lookupAccountInfo txn cmd.toAccountId

                let dbErrors =
                    [ match fromInfo with
                      | None -> sprintf "Account with id %d does not exist" cmd.fromAccountId
                      | Some (false, _) -> sprintf "Account with id %d is not active" cmd.fromAccountId
                      | Some (_, typeName) when typeName <> "asset" ->
                          sprintf "Account with id %d is not an asset account (type: %s)" cmd.fromAccountId typeName
                      | _ -> ()

                      match toInfo with
                      | None -> sprintf "Account with id %d does not exist" cmd.toAccountId
                      | Some (false, _) -> sprintf "Account with id %d is not active" cmd.toAccountId
                      | Some (_, typeName) when typeName <> "asset" ->
                          sprintf "Account with id %d is not an asset account (type: %s)" cmd.toAccountId typeName
                      | _ -> () ]

                if not dbErrors.IsEmpty then
                    Log.warn "Transfer initiation DB validation failed: {Errors}" [| dbErrors :> obj |]
                    txn.Rollback()
                    Error dbErrors
                else
                    let transfer = TransferRepository.insert txn cmd
                    txn.Commit()
                    Log.info "Transfer {TransferId} initiated successfully" [| transfer.id :> obj |]
                    Ok transfer
            with ex ->
                Log.errorExn ex "Failed to initiate transfer" [||]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]

    let confirm (cmd: ConfirmTransferCommand) : Result<Transfer, string list> =
        Log.info "Confirming transfer {TransferId} on {ConfirmedDate}"
            [| cmd.transferId :> obj; cmd.confirmedDate :> obj |]

        // Phase 1: Read + validate
        let readResult =
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                let transfer = TransferRepository.findById txn cmd.transferId
                match transfer with
                | None ->
                    txn.Rollback()
                    Error [ sprintf "Transfer with id %d does not exist" cmd.transferId ]
                | Some t ->
                    if not t.isActive then
                        txn.Rollback()
                        Log.warn "Transfer {TransferId} is inactive" [| cmd.transferId :> obj |]
                        Error [ sprintf "Transfer with id %d is inactive" cmd.transferId ]
                    elif t.status <> TransferStatus.Initiated then
                        txn.Rollback()
                        Log.warn "Transfer {TransferId} is not in initiated status" [| cmd.transferId :> obj |]
                        Error [ sprintf "Transfer must be in initiated status to confirm (current: %s)" (TransferStatus.toString t.status) ]
                    else
                        let period = FiscalPeriodRepository.findByDate txn cmd.confirmedDate
                        match period with
                        | None ->
                            txn.Rollback()
                            Log.warn "No fiscal period found for date {Date}" [| cmd.confirmedDate :> obj |]
                            Error [ sprintf "No fiscal period covers the confirmed_date %O" cmd.confirmedDate ]
                        | Some fp ->
                            txn.Commit()
                            Ok (t, fp)
            with ex ->
                Log.errorExn ex "Failed during read phase for transfer {TransferId}" [| cmd.transferId :> obj |]
                Error [ sprintf "Read phase error: %s" ex.Message ]

        match readResult with
        | Error errs -> Error errs
        | Ok (transfer, fiscalPeriod) ->
            // Phase 2: Build and post journal entry
            let description =
                transfer.description
                |> Option.defaultValue (sprintf "Transfer %d" transfer.id)

            let jeCmd : PostJournalEntryCommand =
                { entryDate = cmd.confirmedDate
                  description = description
                  source = Some "transfer"
                  fiscalPeriodId = fiscalPeriod.id
                  lines =
                    [ { accountId = transfer.toAccountId
                        amount = transfer.amount
                        entryType = EntryType.Debit
                        memo = None }
                      { accountId = transfer.fromAccountId
                        amount = transfer.amount
                        entryType = EntryType.Credit
                        memo = None } ]
                  references =
                    [ { referenceType = "transfer"
                        referenceValue = string transfer.id } ] }

            // Idempotency guard: check for existing non-voided journal entry
            let existingJeId =
                use guardConn = DataSource.openConnection()
                use guardTxn = guardConn.BeginTransaction()
                try
                    let result =
                        JournalEntryRepository.findNonVoidedByReference
                            guardTxn "transfer" (string transfer.id)
                    guardTxn.Commit()
                    result
                with ex ->
                    try guardTxn.Rollback() with _ -> ()
                    raise ex

            match existingJeId with
            | Some jeId ->
                Log.info
                    "Idempotency guard: found existing journal entry {JournalEntryId} for transfer {TransferId}, skipping post"
                    [| jeId :> obj; transfer.id :> obj |]

                // Phase 3: Update transfer record (using existing JE)
                try
                    use conn = DataSource.openConnection()
                    use txn = conn.BeginTransaction()
                    let updated = TransferRepository.updateConfirm txn transfer.id cmd.confirmedDate jeId
                    txn.Commit()
                    Log.info "Transfer {TransferId} confirmed successfully (idempotent)"
                        [| updated.id :> obj |]
                    Ok updated
                with ex ->
                    Log.errorExn ex
                        "Failed to update transfer {TransferId} after finding existing journal entry {JournalEntryId}"
                        [| transfer.id :> obj; jeId :> obj |]
                    Error [ sprintf "Failed to update transfer after journal entry was posted: %s" ex.Message ]

            | None ->
                // No existing entry -- proceed with normal phase 2 + phase 3
                match JournalEntryService.post jeCmd with
                | Error errs ->
                    Log.warn "Journal entry posting failed for transfer {TransferId}: {Errors}"
                        [| transfer.id :> obj; errs :> obj |]
                    Error errs
                | Ok posted ->
                    Log.info "Created journal entry {JournalEntryId} for transfer {TransferId}"
                        [| posted.entry.id :> obj; transfer.id :> obj |]

                    // Phase 3: Update transfer record
                    try
                        use conn = DataSource.openConnection()
                        use txn = conn.BeginTransaction()
                        let updated = TransferRepository.updateConfirm txn transfer.id cmd.confirmedDate posted.entry.id
                        txn.Commit()
                        Log.info "Transfer {TransferId} confirmed successfully" [| updated.id :> obj |]
                        Ok updated
                    with ex ->
                        Log.errorExn ex "Failed to update transfer {TransferId} after journal entry {JournalEntryId} was created"
                            [| transfer.id :> obj; posted.entry.id :> obj |]
                        Log.warn "Transfer {TransferId} remains initiated but journal entry {JournalEntryId} exists — retry is safe"
                            [| transfer.id :> obj; posted.entry.id :> obj |]
                        Error [ sprintf "Failed to update transfer after journal entry was posted: %s" ex.Message ]

    let show (id: int) : Result<Transfer, string list> =
        Log.info "Showing transfer {TransferId}" [| id :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let transfer = TransferRepository.findById txn id
            txn.Commit()
            match transfer with
            | None ->
                Error [ sprintf "Transfer with id %d does not exist" id ]
            | Some t ->
                Ok t
        with ex ->
            Log.errorExn ex "Failed to show transfer {TransferId}" [| id :> obj |]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]

    let list (filter: ListTransfersFilter) : Transfer list =
        Log.info "Listing transfers" [||]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = TransferRepository.list txn filter
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to list transfers" [||]
            try txn.Rollback() with _ -> ()
            []
