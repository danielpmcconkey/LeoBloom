namespace LeoBloom.Utilities

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Orchestrates account balance queries.
module AccountBalanceService =

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
