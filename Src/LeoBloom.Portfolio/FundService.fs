namespace LeoBloom.Portfolio

open System
open Npgsql
open LeoBloom.Domain.Portfolio
open LeoBloom.Utilities

/// Orchestrates validation and persistence for fund operations.
/// Caller is responsible for connection and transaction lifecycle.
module FundService =

    /// Create a new fund. Validates symbol and name are non-blank.
    let createFund (txn: NpgsqlTransaction) (fund: Fund) : Result<Fund, string list> =
        let errors = ResizeArray<string>()
        if String.IsNullOrWhiteSpace fund.symbol then
            errors.Add("Fund symbol is required and cannot be blank")
        if String.IsNullOrWhiteSpace fund.name then
            errors.Add("Fund name is required and cannot be blank")
        if errors.Count > 0 then
            Error (errors |> Seq.toList)
        else
            Log.info "Creating fund {Symbol}" [| fund.symbol :> obj |]
            try
                let created = FundRepository.create txn fund
                Log.info "Created fund {Symbol}" [| created.symbol :> obj |]
                Ok created
            with
            | :? PostgresException as ex when ex.SqlState = "23505" ->
                Error [ sprintf "A fund with symbol '%s' already exists" fund.symbol ]
            | ex ->
                Log.errorExn ex "Failed to create fund {Symbol}" [| fund.symbol :> obj |]
                Error [ sprintf "Persistence error: %s" ex.Message ]

    /// Find a fund by its ticker symbol. Returns None if not found.
    let findFundBySymbol (txn: NpgsqlTransaction) (symbol: string) : Result<Fund option, string list> =
        Log.info "Finding fund {Symbol}" [| symbol :> obj |]
        try
            let result = FundRepository.findBySymbol txn symbol
            Ok result
        with ex ->
            Log.errorExn ex "Failed to find fund {Symbol}" [| symbol :> obj |]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// List all funds.
    let listFunds (txn: NpgsqlTransaction) : Result<Fund list, string list> =
        Log.info "Listing all funds" [||]
        try
            let result = FundRepository.listAll txn
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list funds" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]

    /// List funds filtered by a dimension.
    let listFundsByDimension (txn: NpgsqlTransaction) (filter: FundDimensionFilter) : Result<Fund list, string list> =
        Log.info "Listing funds by dimension" [||]
        try
            let result = FundRepository.listByDimension txn filter
            Ok result
        with ex ->
            Log.errorExn ex "Failed to list funds by dimension" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]
