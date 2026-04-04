namespace LeoBloom.Dal

open System
open Npgsql
open LeoBloom.Domain.Ledger

/// Orchestrates account balance queries.
module AccountBalanceService =

    let getBalanceByIdInTransaction (txn: NpgsqlTransaction) (accountId: int) (asOfDate: DateOnly) : Result<AccountBalance, string> =
        match AccountBalanceRepository.getBalance txn accountId asOfDate with
        | Some balance -> Ok balance
        | None -> Error (sprintf "Account with id %d does not exist" accountId)

    let getBalanceByCodeInTransaction (txn: NpgsqlTransaction) (code: string) (asOfDate: DateOnly) : Result<AccountBalance, string> =
        match AccountBalanceRepository.resolveAccountId txn code with
        | None -> Error (sprintf "Account with code '%s' does not exist" code)
        | Some accountId ->
            match AccountBalanceRepository.getBalance txn accountId asOfDate with
            | Some balance -> Ok balance
            | None -> Error (sprintf "Account with id %d does not exist" accountId)

    let getBalanceById (basePath: string) (accountId: int) (asOfDate: DateOnly) : Result<AccountBalance, string> =
        let connStr = ConnectionString.resolve basePath
        use conn = new NpgsqlConnection(connStr)
        conn.Open()
        use txn = conn.BeginTransaction()
        try
            let result = getBalanceByIdInTransaction txn accountId asOfDate
            txn.Commit()
            result
        with ex ->
            try txn.Rollback() with _ -> ()
            Error (sprintf "Query error: %s" ex.Message)

    let getBalanceByCode (basePath: string) (code: string) (asOfDate: DateOnly) : Result<AccountBalance, string> =
        let connStr = ConnectionString.resolve basePath
        use conn = new NpgsqlConnection(connStr)
        conn.Open()
        use txn = conn.BeginTransaction()
        try
            let result = getBalanceByCodeInTransaction txn code asOfDate
            txn.Commit()
            result
        with ex ->
            try txn.Rollback() with _ -> ()
            Error (sprintf "Query error: %s" ex.Message)
