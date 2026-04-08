namespace LeoBloom.Portfolio

open Npgsql
open LeoBloom.Domain.Portfolio
open LeoBloom.Utilities

/// Aggregates all portfolio dimension tables.
module DimensionService =

    /// List all 8 dimension tables and their id/name values.
    let listAllDimensions (txn: NpgsqlTransaction) : Result<AllDimensions, string list> =
        Log.info "Listing all portfolio dimensions" [||]
        try
            let tables =
                [ { tableName = "tax_bucket";         values = DimensionRepository.listTaxBuckets      txn }
                  { tableName = "account_group";      values = DimensionRepository.listAccountGroups   txn }
                  { tableName = "dim_investment_type"; values = DimensionRepository.listInvestmentTypes txn }
                  { tableName = "dim_market_cap";     values = DimensionRepository.listMarketCaps      txn }
                  { tableName = "dim_index_type";     values = DimensionRepository.listIndexTypes      txn }
                  { tableName = "dim_sector";         values = DimensionRepository.listSectors         txn }
                  { tableName = "dim_region";         values = DimensionRepository.listRegions         txn }
                  { tableName = "dim_objective";      values = DimensionRepository.listObjectives      txn } ]
            Ok { tables = tables }
        with ex ->
            Log.errorExn ex "Failed to list portfolio dimensions" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]
