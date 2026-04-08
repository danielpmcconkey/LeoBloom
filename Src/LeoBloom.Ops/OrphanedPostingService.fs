namespace LeoBloom.Ops

open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Utilities

/// Orchestrates the orphaned posting diagnostic.
/// Read-only — caller provides the transaction, service runs both repository queries and returns results.
module OrphanedPostingService =

    let findOrphanedPostings (txn: NpgsqlTransaction) : Result<OrphanedPostingResult, string list> =
        Log.info "Running orphaned posting diagnostic" [||]
        try
            let orphans = OrphanedPostingRepository.findOrphanedPostings txn
            Log.info "Orphaned posting diagnostic complete: {Count} orphan(s) found"
                [| orphans.Length :> obj |]
            Ok { orphans = orphans }
        with ex ->
            Log.errorExn ex "Orphaned posting diagnostic query failed" [||]
            Error [ sprintf "Persistence error: %s" ex.Message ]
