namespace LeoBloom.Utilities

open Npgsql
open LeoBloom.Domain.Ops

/// Orchestrates validation and persistence for obligation agreement CRUD.
/// Opens its own connection + transaction for atomicity.
module ObligationAgreementService =

    let private lookupAccount (txn: NpgsqlTransaction) (accountId: int) : (int * bool) option =
        use sql = new NpgsqlCommand(
            "SELECT id, is_active FROM ledger.account WHERE id = @id",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@id", accountId) |> ignore
        use reader = sql.ExecuteReader()
        if reader.Read() then
            let result = Some (reader.GetInt32(0), reader.GetBoolean(1))
            reader.Close()
            result
        else
            reader.Close()
            None

    let private validateAccountReferences
        (txn: NpgsqlTransaction)
        (sourceAccountId: int option)
        (destAccountId: int option) : Result<unit, string list> =
        let mutable errors = []

        match sourceAccountId with
        | Some srcId ->
            match lookupAccount txn srcId with
            | None ->
                errors <- errors @ [ sprintf "source account with id %d does not exist" srcId ]
            | Some (_, isActive) ->
                if not isActive then
                    errors <- errors @ [ sprintf "source account with id %d is inactive" srcId ]
        | None -> ()

        match destAccountId with
        | Some destId ->
            match lookupAccount txn destId with
            | None ->
                errors <- errors @ [ sprintf "dest account with id %d does not exist" destId ]
            | Some (_, isActive) ->
                if not isActive then
                    errors <- errors @ [ sprintf "dest account with id %d is inactive" destId ]
        | None -> ()

        if errors.IsEmpty then Ok () else Error errors

    let create (cmd: CreateObligationAgreementCommand) : Result<ObligationAgreement, string list> =
        Log.info "Creating obligation agreement {Name}" [| cmd.name :> obj |]
        match ObligationAgreementValidation.validateCreateCommand cmd with
        | Error errs ->
            Log.warn "Obligation agreement validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                match validateAccountReferences txn cmd.sourceAccountId cmd.destAccountId with
                | Error errs ->
                    Log.warn "Obligation agreement account validation failed: {Errors}" [| errs :> obj |]
                    txn.Rollback()
                    Error errs
                | Ok () ->
                    let agreement = ObligationAgreementRepository.insert txn cmd
                    txn.Commit()
                    Log.info "Created obligation agreement {Id} successfully" [| agreement.id :> obj |]
                    Ok agreement
            with ex ->
                Log.errorExn ex "Failed to create obligation agreement" [||]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]

    let getById (id: int) : ObligationAgreement option =
        Log.info "Getting obligation agreement {Id}" [| id :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = ObligationAgreementRepository.findById txn id
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to get obligation agreement {Id}" [| id :> obj |]
            try txn.Rollback() with _ -> ()
            None

    let list (filter: ListAgreementsFilter) : ObligationAgreement list =
        Log.info "Listing obligation agreements" [||]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = ObligationAgreementRepository.list txn filter
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to list obligation agreements" [||]
            try txn.Rollback() with _ -> ()
            []

    let update (cmd: UpdateObligationAgreementCommand) : Result<ObligationAgreement, string list> =
        Log.info "Updating obligation agreement {Id}" [| cmd.id :> obj |]
        match ObligationAgreementValidation.validateUpdateCommand cmd with
        | Error errs ->
            Log.warn "Obligation agreement update validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                match ObligationAgreementRepository.findById txn cmd.id with
                | None ->
                    txn.Rollback()
                    Log.warn "Obligation agreement {Id} does not exist for update" [| cmd.id :> obj |]
                    Error [ sprintf "Obligation agreement with id %d does not exist" cmd.id ]
                | Some _ ->
                    match validateAccountReferences txn cmd.sourceAccountId cmd.destAccountId with
                    | Error errs ->
                        Log.warn "Obligation agreement account validation failed: {Errors}" [| errs :> obj |]
                        txn.Rollback()
                        Error errs
                    | Ok () ->
                        match ObligationAgreementRepository.update txn cmd with
                        | Some updated ->
                            txn.Commit()
                            Log.info "Updated obligation agreement {Id} successfully" [| updated.id :> obj |]
                            Ok updated
                        | None ->
                            txn.Rollback()
                            Error [ sprintf "Obligation agreement with id %d could not be updated" cmd.id ]
            with ex ->
                Log.errorExn ex "Failed to update obligation agreement {Id}" [| cmd.id :> obj |]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]

    let deactivate (id: int) : Result<ObligationAgreement, string list> =
        Log.info "Deactivating obligation agreement {Id}" [| id :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            match ObligationAgreementRepository.findById txn id with
            | None ->
                txn.Rollback()
                Log.warn "Obligation agreement {Id} does not exist for deactivation" [| id :> obj |]
                Error [ sprintf "Obligation agreement with id %d does not exist" id ]
            | Some _ ->
                if ObligationAgreementRepository.hasActiveInstances txn id then
                    txn.Rollback()
                    Log.warn "Cannot deactivate agreement {Id} — has active obligation instances" [| id :> obj |]
                    Error [ "Cannot deactivate agreement with active obligation instances" ]
                else
                    match ObligationAgreementRepository.deactivate txn id with
                    | Some deactivated ->
                        txn.Commit()
                        Log.info "Deactivated obligation agreement {Id} successfully" [| deactivated.id :> obj |]
                        Ok deactivated
                    | None ->
                        txn.Rollback()
                        Error [ sprintf "Obligation agreement with id %d could not be deactivated" id ]
        with ex ->
            Log.errorExn ex "Failed to deactivate obligation agreement {Id}" [| id :> obj |]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]
