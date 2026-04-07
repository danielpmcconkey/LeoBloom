namespace LeoBloom.Portfolio

open System
open Npgsql
open LeoBloom.Domain.Portfolio
open LeoBloom.Utilities

/// Orchestrates validation and persistence for investment account operations.
module InvestmentAccountService =

    /// Create a new investment account. Validates name is non-blank.
    let createAccount
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
            use conn = DataSource.openConnection()
            use txn = conn.BeginTransaction()
            try
                let acct = InvestmentAccountRepository.create txn name taxBucketId accountGroupId
                txn.Commit()
                Log.info "Created investment account {Id}" [| acct.id :> obj |]
                Ok acct
            with ex ->
                Log.errorExn ex "Failed to create investment account {Name}" [| name :> obj |]
                try txn.Rollback() with _ -> ()
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// List all investment accounts.
    let listAccounts () : Result<InvestmentAccount list, string list> =
        Log.info "Listing all investment accounts" [||]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = InvestmentAccountRepository.listAll txn
            txn.Commit()
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list investment accounts" [||]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]
