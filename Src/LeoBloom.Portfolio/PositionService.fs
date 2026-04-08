namespace LeoBloom.Portfolio

open System
open Npgsql
open LeoBloom.Domain.Portfolio
open LeoBloom.Utilities

/// Orchestrates validation and persistence for position operations.
/// Caller is responsible for connection and transaction lifecycle.
module PositionService =

    /// Record a position. Validates:
    ///   AC-B9: price, quantity, current_value must be >= 0
    ///   AC-B10: fund symbol must exist
    ///   AC-B4: duplicate (account, symbol, date) returns friendly error
    let recordPosition
        (txn: NpgsqlTransaction)
        (investmentAccountId: int)
        (symbol: string)
        (positionDate: DateOnly)
        (price: decimal)
        (quantity: decimal)
        (currentValue: decimal)
        (costBasis: decimal)
        : Result<Position, string list> =
        // AC-B9: non-negative value checks (pure validation, no DB)
        let errors = ResizeArray<string>()
        if price < 0m then
            errors.Add("Price must not be negative")
        if quantity < 0m then
            errors.Add("Quantity must not be negative")
        if currentValue < 0m then
            errors.Add("Current value must not be negative")
        if errors.Count > 0 then
            Error (errors |> Seq.toList)
        else
            Log.info "Recording position for account {AccountId} symbol {Symbol}" [| investmentAccountId :> obj; symbol :> obj |]
            try
                // AC-B10: fund must exist
                match FundRepository.findBySymbol txn symbol with
                | None ->
                    Error [ sprintf "Fund with symbol '%s' does not exist" symbol ]
                | Some _ ->
                    let position =
                        PositionRepository.create txn investmentAccountId symbol positionDate
                            price quantity currentValue costBasis
                    Log.info "Recorded position {Id}" [| position.id :> obj |]
                    Ok position
            with
            | :? PostgresException as ex when ex.SqlState = "23505" ->
                // AC-B4: duplicate (account, symbol, date)
                Error [ sprintf "A position for account %d, symbol '%s', date %O already exists"
                            investmentAccountId symbol positionDate ]
            | ex ->
                Log.errorExn ex "Failed to record position for account {AccountId} symbol {Symbol}"
                    [| investmentAccountId :> obj; symbol :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// List positions matching the given filter.
    let listPositions (txn: NpgsqlTransaction) (filter: PositionFilter) : Result<Position list, string list> =
        Log.info "Listing positions" [||]
        try
            let result = PositionRepository.listByFilter txn filter
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list positions" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Get latest positions (most recent per symbol) for a given account.
    let latestPositionsByAccount (txn: NpgsqlTransaction) (accountId: int) : Result<Position list, string list> =
        Log.info "Getting latest positions for account {AccountId}" [| accountId :> obj |]
        try
            let result = PositionRepository.latestByAccount txn accountId
            Ok result
        with ex ->
            Log.errorExn ex "Failed to get latest positions for account {AccountId}" [| accountId :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Get latest positions (most recent per account+symbol) across all accounts.
    let latestPositionsAll (txn: NpgsqlTransaction) : Result<Position list, string list> =
        Log.info "Getting latest positions for all accounts" [||]
        try
            let result = PositionRepository.latestAll txn
            Ok result
        with ex ->
            Log.errorExn ex "Failed to get latest positions for all accounts" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]
