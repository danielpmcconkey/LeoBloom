namespace LeoBloom.Ops

open LeoBloom.Domain.Ops
open LeoBloom.Utilities

/// Orchestrates the orphaned posting diagnostic.
/// Read-only — opens a connection, runs both repository queries, and returns results.
module OrphanedPostingService =

    let findOrphanedPostings () : Result<OrphanedPostingResult, string list> =
        Log.info "Running orphaned posting diagnostic" [||]
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let orphans = OrphanedPostingRepository.findOrphanedPostings txn
            txn.Commit()
            Log.info "Orphaned posting diagnostic complete: {Count} orphan(s) found"
                [| orphans.Length :> obj |]
            Ok { orphans = orphans }
        with ex ->
            Log.errorExn ex "Orphaned posting diagnostic query failed" [||]
            try txn.Rollback() with _ -> ()
            Error [ sprintf "Persistence error: %s" ex.Message ]
