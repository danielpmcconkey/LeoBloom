namespace LeoBloom.Ops

open System
open Npgsql
open LeoBloom.Domain.Ops

/// Read-only SQL queries for orphaned posting detection.
/// Two query directions:
///   A: journal_entry_reference → source table (DanglingStatus, MissingSource, InvalidReference)
///   B: source table → journal_entry (VoidedBackingEntry)
/// All queries are SELECT-only; no mutations.
module OrphanedPostingRepository =

    /// Query A, part 1: find references with non-numeric values for obligation/transfer types.
    /// These cannot be safely cast to integer and are reported as InvalidReference.
    let private queryInvalidReferences (txn: NpgsqlTransaction) : OrphanedPosting list =
        use sql = new NpgsqlCommand(
            "SELECT jer.journal_entry_id, jer.reference_type, jer.reference_value \
             FROM ledger.journal_entry_reference jer \
             WHERE jer.reference_type IN ('obligation', 'transfer') \
               AND jer.reference_value !~ '^\\d+$'",
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            results <-
                { sourceType = reader.GetString(1)
                  sourceRecordId = None
                  journalEntryId = reader.GetInt32(0)
                  condition = InvalidReference
                  referenceValue = reader.GetString(2) } :: results
        reader.Close()
        results

    /// Query A, part 2: numeric references — left-join to source tables to detect
    /// DanglingStatus (source exists but journal_entry_id is NULL) and
    /// MissingSource (source row does not exist).
    let private queryReferenceSourceOrphans (txn: NpgsqlTransaction) : OrphanedPosting list =
        use sql = new NpgsqlCommand(
            "SELECT jer.journal_entry_id, jer.reference_type, jer.reference_value, \
                    oi.id AS obligation_id, \
                    t.id AS transfer_id \
             FROM ledger.journal_entry_reference jer \
             LEFT JOIN ops.obligation_instance oi \
               ON jer.reference_type = 'obligation' \
              AND oi.id = CAST(jer.reference_value AS integer) \
             LEFT JOIN ops.transfer t \
               ON jer.reference_type = 'transfer' \
              AND t.id = CAST(jer.reference_value AS integer) \
             WHERE jer.reference_type IN ('obligation', 'transfer') \
               AND jer.reference_value ~ '^\\d+$' \
               AND ( \
                 (jer.reference_type = 'obligation' AND oi.id IS NULL) \
                 OR (jer.reference_type = 'transfer' AND t.id IS NULL) \
                 OR (jer.reference_type = 'obligation' AND oi.id IS NOT NULL AND oi.journal_entry_id IS NULL) \
                 OR (jer.reference_type = 'transfer' AND t.id IS NOT NULL AND t.journal_entry_id IS NULL) \
               )",
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            let jeId = reader.GetInt32(0)
            let refType = reader.GetString(1)
            let refValue = reader.GetString(2)
            let obligationId = if reader.IsDBNull(3) then None else Some (reader.GetInt32(3))
            let transferId   = if reader.IsDBNull(4) then None else Some (reader.GetInt32(4))
            let condition, sourceId =
                match refType with
                | "obligation" ->
                    match obligationId with
                    | None    -> MissingSource, None
                    | Some id -> DanglingStatus, Some id
                | "transfer" ->
                    match transferId with
                    | None    -> MissingSource, None
                    | Some id -> DanglingStatus, Some id
                | _ -> MissingSource, None
            results <-
                { sourceType = refType
                  sourceRecordId = sourceId
                  journalEntryId = jeId
                  condition = condition
                  referenceValue = refValue } :: results
        reader.Close()
        results

    /// Query B: source records (obligation posted / transfer confirmed) whose backing
    /// journal entry has been voided.
    let private queryVoidedBackingEntries (txn: NpgsqlTransaction) : OrphanedPosting list =
        use sql = new NpgsqlCommand(
            "SELECT oi.id, oi.journal_entry_id, 'obligation' AS source_type \
             FROM ops.obligation_instance oi \
             JOIN ledger.journal_entry je ON je.id = oi.journal_entry_id \
             WHERE oi.status = 'posted' \
               AND oi.journal_entry_id IS NOT NULL \
               AND je.voided_at IS NOT NULL \
             UNION ALL \
             SELECT t.id, t.journal_entry_id, 'transfer' AS source_type \
             FROM ops.transfer t \
             JOIN ledger.journal_entry je ON je.id = t.journal_entry_id \
             WHERE t.status = 'confirmed' \
               AND t.journal_entry_id IS NOT NULL \
               AND je.voided_at IS NOT NULL",
            txn.Connection, txn)
        use reader = sql.ExecuteReader()
        let mutable results = []
        while reader.Read() do
            let sourceId = reader.GetInt32(0)
            let jeId     = reader.GetInt32(1)
            let srcType  = reader.GetString(2)
            results <-
                { sourceType = srcType
                  sourceRecordId = Some sourceId
                  journalEntryId = jeId
                  condition = VoidedBackingEntry
                  referenceValue = string sourceId } :: results
        reader.Close()
        results

    /// Run all three detection queries and return the merged result list.
    let findOrphanedPostings (txn: NpgsqlTransaction) : OrphanedPosting list =
        let invalidRefs   = queryInvalidReferences txn
        let refOrphans    = queryReferenceSourceOrphans txn
        let voidedBacking = queryVoidedBackingEntries txn
        invalidRefs @ refOrphans @ voidedBacking
