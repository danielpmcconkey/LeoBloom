namespace LeoBloom.Portfolio

open System
open Npgsql
open LeoBloom.Domain.Portfolio
open LeoBloom.Utilities

/// Orchestrates validation and persistence for investment account operations.
/// Caller is responsible for connection and transaction lifecycle.
module InvestmentAccountService =

    /// Create a new investment account. Validates name is non-blank.
    let createAccount
        (txn: NpgsqlTransaction)
        (name: string)
        (taxBucketId: int)
        (accountGroupId: int)
        : Result<InvestmentAccount, string list> =
        let errors = ResizeArray<string>()
        if String.IsNullOrWhiteSpace name then
            errors.Add("Account name is required and cannot be blank")
        if errors.Count > 0 then
            Error (errors |> Seq.toList)
        else
            Log.info "Creating investment account {Name}" [| name :> obj |]
            try
                let acct = InvestmentAccountRepository.create txn name taxBucketId accountGroupId
                Log.info "Created investment account {Id}" [| acct.id :> obj |]
                Ok acct
            with ex ->
                Log.errorExn ex "Failed to create investment account {Name}" [| name :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// List all investment accounts.
    let listAccounts (txn: NpgsqlTransaction) : Result<InvestmentAccount list, string list> =
        Log.info "Listing all investment accounts" [||]
        try
            let result = InvestmentAccountRepository.listAll txn
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list investment accounts" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Get a single investment account by ID. Returns None if not found.
    let getAccount (txn: NpgsqlTransaction) (accountId: int) : Result<InvestmentAccount option, string list> =
        Log.info "Getting investment account {Id}" [| accountId :> obj |]
        try
            let result = InvestmentAccountRepository.findById txn accountId
            Ok result
        with ex ->
            Log.errorExn ex "Failed to get investment account {Id}" [| accountId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// List investment accounts filtered by account group name.
    let listAccountsByGroup (txn: NpgsqlTransaction) (groupName: string) : Result<InvestmentAccount list, string list> =
        Log.info "Listing investment accounts by group {Name}" [| groupName :> obj |]
        try
            let result = InvestmentAccountRepository.listByGroup txn groupName
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list investment accounts by group {Name}" [| groupName :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// List investment accounts filtered by tax bucket name.
    let listAccountsByTaxBucket (txn: NpgsqlTransaction) (bucketName: string) : Result<InvestmentAccount list, string list> =
        Log.info "Listing investment accounts by tax bucket {Name}" [| bucketName :> obj |]
        try
            let result = InvestmentAccountRepository.listByTaxBucket txn bucketName
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list investment accounts by tax bucket {Name}" [| bucketName :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]
