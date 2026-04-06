namespace LeoBloom.Ledger

open System
open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Orchestrates account balance queries.
module AccountBalanceService =

    /// List accounts. No business logic -- straight pass-through to repo.
    let listAccounts
        (accountTypeName: string option)
        (includeInactive: bool)
        : Result<Account list, string list> =
        Log.info "Listing accounts (type={AccountType}, includeInactive={IncludeInactive})" [| accountTypeName :> obj; includeInactive :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = AccountBalanceRepository.listAccounts txn accountTypeName includeInactive
            txn.Commit()
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list accounts" [||]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Show a single account by ID.
    let showAccountById (accountId: int) : Result<Account, string> =
        Log.info "Showing account by ID {AccountId}" [| accountId :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result =
                match AccountBalanceRepository.findAccountById txn accountId with
                | Some account -> Ok account
                | None -> Error (sprintf "Account with id %d does not exist" accountId)
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to show account {AccountId}" [| accountId :> obj |]
            try txn.Rollback() with _ -> ()
            Error (sprintf "Query error: %s" ex.Message)

    /// Show a single account by code.
    let showAccountByCode (code: string) : Result<Account, string> =
        Log.info "Showing account by code {AccountCode}" [| code :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result =
                match AccountBalanceRepository.findAccountByCode txn code with
                | Some account -> Ok account
                | None -> Error (sprintf "Account with code '%s' does not exist" code)
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to show account code {AccountCode}" [| code :> obj |]
            try txn.Rollback() with _ -> ()
            Error (sprintf "Query error: %s" ex.Message)

    let getBalanceById (accountId: int) (asOfDate: DateOnly) : Result<AccountBalance, string> =
        Log.info "Getting balance for account {AccountId} as of {AsOfDate}" [| accountId :> obj; asOfDate :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result =
                match AccountBalanceRepository.getBalance txn accountId asOfDate with
                | Some balance -> Ok balance
                | None -> Error (sprintf "Account with id %d does not exist" accountId)
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to get balance for account {AccountId}" [| accountId :> obj |]
            try txn.Rollback() with _ -> ()
            Error (sprintf "Query error: %s" ex.Message)

    let getBalanceByCode (code: string) (asOfDate: DateOnly) : Result<AccountBalance, string> =
        Log.info "Getting balance for account code {AccountCode} as of {AsOfDate}" [| code :> obj; asOfDate :> obj |]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result =
                match AccountBalanceRepository.resolveAccountId txn code with
                | None -> Error (sprintf "Account with code '%s' does not exist" code)
                | Some accountId ->
                    match AccountBalanceRepository.getBalance txn accountId asOfDate with
                    | Some balance -> Ok balance
                    | None -> Error (sprintf "Account with id %d does not exist" accountId)
            txn.Commit()
            result
        with ex ->
            Log.errorExn ex "Failed to get balance for account code {AccountCode}" [| code :> obj |]
            try txn.Rollback() with _ -> ()
            Error (sprintf "Query error: %s" ex.Message)
