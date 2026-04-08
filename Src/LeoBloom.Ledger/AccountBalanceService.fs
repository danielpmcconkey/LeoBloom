namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates account balance queries.
module AccountBalanceService =

    /// List accounts. No business logic -- straight pass-through to repo.
    let listAccounts
        (txn: NpgsqlTransaction)
        (accountTypeName: string option)
        (includeInactive: bool)
        : Result<Account list, string list> =
        Log.info "Listing accounts (type={AccountType}, includeInactive={IncludeInactive})" [| accountTypeName :> obj; includeInactive :> obj |]
        try
            let result = AccountBalanceRepository.listAccounts txn accountTypeName includeInactive
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list accounts" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Show a single account by ID.
    let showAccountById (txn: NpgsqlTransaction) (accountId: int) : Result<Account, string> =
        Log.info "Showing account by ID {AccountId}" [| accountId :> obj |]
        try
            match AccountBalanceRepository.findAccountById txn accountId with
            | Some account -> Ok account
            | None -> Error (sprintf "Account with id %d does not exist" accountId)
        with ex ->
            Log.errorExn ex "Failed to show account {AccountId}" [| accountId :> obj |]
            Error (sprintf "Persistence error: %s" ex.Message)

    /// Show a single account by code.
    let showAccountByCode (txn: NpgsqlTransaction) (code: string) : Result<Account, string> =
        Log.info "Showing account by code {AccountCode}" [| code :> obj |]
        try
            match AccountBalanceRepository.findAccountByCode txn code with
            | Some account -> Ok account
            | None -> Error (sprintf "Account with code '%s' does not exist" code)
        with ex ->
            Log.errorExn ex "Failed to show account code {AccountCode}" [| code :> obj |]
            Error (sprintf "Persistence error: %s" ex.Message)

    let getBalanceById (txn: NpgsqlTransaction) (accountId: int) (asOfDate: DateOnly) : Result<AccountBalance, string> =
        Log.info "Getting balance for account {AccountId} as of {AsOfDate}" [| accountId :> obj; asOfDate :> obj |]
        try
            match AccountBalanceRepository.getBalance txn accountId asOfDate with
            | Some balance -> Ok balance
            | None -> Error (sprintf "Account with id %d does not exist" accountId)
        with ex ->
            Log.errorExn ex "Failed to get balance for account {AccountId}" [| accountId :> obj |]
            Error (sprintf "Persistence error: %s" ex.Message)

    let getBalanceByCode (txn: NpgsqlTransaction) (code: string) (asOfDate: DateOnly) : Result<AccountBalance, string> =
        Log.info "Getting balance for account code {AccountCode} as of {AsOfDate}" [| code :> obj; asOfDate :> obj |]
        try
            match AccountBalanceRepository.resolveAccountId txn code with
            | None -> Error (sprintf "Account with code '%s' does not exist" code)
            | Some accountId ->
                match AccountBalanceRepository.getBalance txn accountId asOfDate with
                | Some balance -> Ok balance
                | None -> Error (sprintf "Account with id %d does not exist" accountId)
        with ex ->
            Log.errorExn ex "Failed to get balance for account code {AccountCode}" [| code :> obj |]
            Error (sprintf "Persistence error: %s" ex.Message)
