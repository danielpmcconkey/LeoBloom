namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates validation and persistence for account CRUD operations.
module AccountService =

    /// Create an account. Validates code/name non-blank, account_type_id exists,
    /// subtype is valid for the given type, parent (if given) exists and is active.
    /// Catches 23505 for duplicate code.
    let createAccount (txn: NpgsqlTransaction) (cmd: CreateAccountCommand) : Result<Account, string list> =
        let errors = ResizeArray<string>()
        if String.IsNullOrWhiteSpace cmd.code then
            errors.Add("Account code is required and cannot be blank")
        if String.IsNullOrWhiteSpace cmd.name then
            errors.Add("Account name is required and cannot be blank")
        if errors.Count > 0 then
            Error (errors |> Seq.toList)
        else
            Log.info "Creating account {Code}" [| cmd.code :> obj |]
            try
                match AccountRepository.findAccountTypeNameById txn cmd.accountTypeId with
                | None ->
                    Error [ sprintf "account type with id %d does not exist" cmd.accountTypeId ]
                | Some typeName ->
                    if not (AccountSubType.isValidSubType typeName cmd.subType) then
                        let subTypeName = cmd.subType |> Option.map AccountSubType.toDbString |> Option.defaultValue ""
                        Error [ sprintf "subtype '%s' is not valid for account type '%s'" subTypeName typeName ]
                    else
                        match cmd.parentId with
                        | Some pid ->
                            match AccountRepository.findById txn pid with
                            | None ->
                                Error [ sprintf "parent account with id %d does not exist" pid ]
                            | Some parent when not parent.isActive ->
                                Error [ sprintf "parent account with id %d is inactive" pid ]
                            | Some _ ->
                                try
                                    let acct = AccountRepository.create txn cmd.code cmd.name cmd.accountTypeId cmd.parentId cmd.subType cmd.externalRef
                                    Log.info "Created account {AccountId}" [| acct.id :> obj |]
                                    Ok acct
                                with
                                | :? PostgresException as ex when ex.SqlState = "23505" ->
                                    Error [ sprintf "account with code '%s' already exists" cmd.code ]
                        | None ->
                            try
                                let acct = AccountRepository.create txn cmd.code cmd.name cmd.accountTypeId cmd.parentId cmd.subType cmd.externalRef
                                Log.info "Created account {AccountId}" [| acct.id :> obj |]
                                Ok acct
                            with
                            | :? PostgresException as ex when ex.SqlState = "23505" ->
                                Error [ sprintf "account with code '%s' already exists" cmd.code ]
            with ex ->
                Log.errorExn ex "Failed to create account {Code}" [| cmd.code :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Update an account's name. Validates account exists and new name is non-blank.
    let updateAccountName (txn: NpgsqlTransaction) (cmd: UpdateAccountNameCommand) : Result<Account, string list> =
        if String.IsNullOrWhiteSpace cmd.name then
            Error [ "Account name is required and cannot be blank" ]
        else
            Log.info "Updating account name {AccountId}" [| cmd.accountId :> obj |]
            try
                match AccountRepository.findById txn cmd.accountId with
                | None ->
                    Error [ sprintf "account with id %d does not exist" cmd.accountId ]
                | Some _ ->
                    let acct = AccountRepository.updateName txn cmd.accountId cmd.name
                    Log.info "Updated account name {AccountId}" [| acct.id :> obj |]
                    Ok acct
            with ex ->
                Log.errorExn ex "Failed to update account name {AccountId}" [| cmd.accountId :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Update mutable fields on an account. Validates account exists, name (if given) is non-blank,
    /// and subtype (if given) is valid for the account's type.
    /// Returns Result<Account * Account, string list> — (before, after) for CLI diff display.
    let updateAccount (txn: NpgsqlTransaction) (cmd: UpdateAccountCommand) : Result<Account * Account, string list> =
        Log.info "Updating account {AccountId}" [| cmd.accountId :> obj |]
        try
            match AccountRepository.findById txn cmd.accountId with
            | None ->
                Error [ sprintf "account with id %d does not exist" cmd.accountId ]
            | Some before ->
                let errors = ResizeArray<string>()
                match cmd.name with
                | Some n when String.IsNullOrWhiteSpace n ->
                    errors.Add("Account name cannot be blank")
                | _ -> ()
                match cmd.subType with
                | Some st ->
                    match AccountRepository.findAccountTypeNameById txn before.accountTypeId with
                    | None ->
                        errors.Add(sprintf "account type with id %d does not exist" before.accountTypeId)
                    | Some typeName ->
                        if not (AccountSubType.isValidSubType typeName (Some st)) then
                            let subTypeName = AccountSubType.toDbString st
                            errors.Add(sprintf "subtype '%s' is not valid for account type '%s'" subTypeName typeName)
                | None -> ()
                if errors.Count > 0 then
                    Error (errors |> Seq.toList)
                else
                    let after = AccountRepository.update txn cmd.accountId cmd.name cmd.subType cmd.externalRef
                    Log.info "Updated account {AccountId}" [| after.id :> obj |]
                    Ok (before, after)
        with ex ->
            Log.errorExn ex "Failed to update account {AccountId}" [| cmd.accountId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Deactivate an account. Validates account exists and is currently active.
    /// Rejects if the account has active child accounts.
    /// Allows even if the account has posted journal entries.
    let deactivateAccount (txn: NpgsqlTransaction) (cmd: DeactivateAccountCommand) : Result<Account, string list> =
        Log.info "Deactivating account {AccountId}" [| cmd.accountId :> obj |]
        try
            match AccountRepository.findById txn cmd.accountId with
            | None ->
                Error [ sprintf "account with id %d does not exist" cmd.accountId ]
            | Some acct when not acct.isActive ->
                Error [ sprintf "account with id %d is already inactive" cmd.accountId ]
            | Some _ ->
                if AccountRepository.hasChildren txn cmd.accountId then
                    Error [ sprintf "account with id %d has active children and cannot be deactivated" cmd.accountId ]
                else
                    let acct = AccountRepository.deactivate txn cmd.accountId
                    Log.info "Deactivated account {AccountId}" [| acct.id :> obj |]
                    Ok acct
        with ex ->
            Log.errorExn ex "Failed to deactivate account {AccountId}" [| cmd.accountId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]
