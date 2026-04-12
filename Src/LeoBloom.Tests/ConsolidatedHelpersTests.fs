module LeoBloom.Tests.ConsolidatedHelpersTests

open System
open System.IO
open Npgsql
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers

// =====================================================================
// @FT-CHL-001 -- Journal entry with null source persists via consolidated optParam
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-001")>]
let ``Journal entry with null source persists correctly via consolidated optParam`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" assetAt true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Null source test"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }

    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.True(posted.entry.id > 0)
        Assert.True(posted.entry.source.IsNone, "Expected source to be None")
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CHL-002 -- Journal entry with non-null source persists via consolidated optParam
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-002")>]
let ``Journal entry with non-null source persists correctly via consolidated optParam`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" assetAt true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Source test"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }

    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.True(posted.entry.id > 0)
        Assert.Equal(Some "manual", posted.entry.source)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CHL-003 -- Journal entry with null memo persists via consolidated optParam
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-003")>]
let ``Journal entry with null memo on lines persists correctly via consolidated optParam`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" assetAt true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Null memo test"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }

    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.True(posted.entry.id > 0)
        Assert.Equal(2, List.length posted.lines)
        Assert.True(posted.entry.createdAt > DateTimeOffset.MinValue)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CHL-004 -- Journal entry with non-null memo persists via consolidated optParam
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-004")>]
let ``Journal entry with non-null memo on lines persists correctly via consolidated optParam`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let assetAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let revAt = InsertHelpers.insertAccountType txn (prefix + "_rv") "credit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Asset" assetAt true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Revenue" revAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true

    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Memo test"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = Some "Cash received" }
              { accountId = acct2; amount = 1000m; entryType = EntryType.Credit; memo = Some "Rent income" } ]
          references = []
          adjustmentForPeriodId = None }

    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.True(posted.lines |> List.forall (fun l -> l.memo.IsSome),
                    "All lines should have non-null memo")
        let memos = posted.lines |> List.map (fun l -> l.memo.Value) |> Set.ofList
        Assert.Contains("Cash received", memos)
        Assert.Contains("Rent income", memos)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

// =====================================================================
// @FT-CHL-005 -- Repo root resolves correctly from test files
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-005")>]
let ``Repo root resolves correctly from test files`` () =
    let root = RepoPath.repoRoot
    let slnPath = Path.Combine(root, "LeoBloom.sln")
    Assert.True(File.Exists(slnPath),
        $"RepoPath.repoRoot should point to directory containing LeoBloom.sln. Got: {root}")

// =====================================================================
// @FT-CHL-006 -- Src directory resolves correctly from test files
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-006")>]
let ``Src directory resolves correctly from test files`` () =
    let src = RepoPath.srcDir
    Assert.True(src.EndsWith("Src"), $"RepoPath.srcDir should end with 'Src'. Got: {src}")
    Assert.True(Directory.Exists(src), $"RepoPath.srcDir should be a valid directory. Got: {src}")

// =====================================================================
// @FT-CHL-007 -- Shared constraint helpers detect violations (ledger)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-007")>]
let ``Shared assertNotNull detects NOT NULL violation in ledger schema`` () =
    use conn = DataSource.openConnection()
    let ex = ConstraintAssert.tryInsert conn
                "INSERT INTO ledger.account_type (name, normal_balance) VALUES (NULL, 'debit')"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation on ledger.account_type.name"

[<Fact>]
[<Trait("GherkinId", "FT-CHL-007")>]
let ``Shared assertUnique detects UNIQUE violation in ledger schema`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let name = TestData.accountTypeName prefix
    let mutable insertedId = 0
    try
        use cmd = new NpgsqlCommand(
            "INSERT INTO ledger.account_type (name, normal_balance) VALUES (@n, 'debit') RETURNING id", conn)
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        insertedId <- cmd.ExecuteScalar() :?> int
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ledger.account_type (name, normal_balance) VALUES (@n, 'debit')"
                    (fun cmd -> cmd.Parameters.AddWithValue("@n", name) |> ignore)
        ConstraintAssert.assertUnique ex "Expected UNIQUE violation on ledger.account_type.name"
    finally
        if insertedId > 0 then
            try
                use cmd = new NpgsqlCommand("DELETE FROM ledger.account_type WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", insertedId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "CHL-007 cleanup error: %s" ex.Message

[<Fact>]
[<Trait("GherkinId", "FT-CHL-007")>]
let ``Shared assertFk detects FK violation in ledger schema`` () =
    use conn = DataSource.openConnection()
    let ex = ConstraintAssert.tryExec conn
                "INSERT INTO ledger.account (code, name, account_type_id) VALUES ('ZZZZ', 'Test', @id)"
                (fun cmd -> cmd.Parameters.AddWithValue("@id", 9999) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation on ledger.account.account_type_id"

// =====================================================================
// @FT-CHL-007 -- Shared constraint helpers detect violations (ops)
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-007")>]
let ``Shared assertNotNull detects NOT NULL violation in ops schema`` () =
    use conn = DataSource.openConnection()
    let ex = ConstraintAssert.tryInsert conn
                "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES (NULL, 'receivable', 'monthly')"
    ConstraintAssert.assertNotNull ex "Expected NOT NULL violation on ops.obligation_agreement.name"

[<Fact>]
[<Trait("GherkinId", "FT-CHL-007")>]
let ``Shared assertUnique detects UNIQUE violation in ops schema`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let fpKey = TestData.periodKey prefix
    let mutable fpId = 0
    let mutable invoiceId = 0
    let tenant = $"{prefix}_tenant"
    try
        use fpCmd = new NpgsqlCommand(
            "INSERT INTO ledger.fiscal_period (period_key, start_date, end_date, is_open) VALUES (@k, '2099-06-01', '2099-06-30', true) RETURNING id", conn)
        fpCmd.Parameters.AddWithValue("@k", fpKey) |> ignore
        fpId <- fpCmd.ExecuteScalar() :?> int
        use cmd1 = new NpgsqlCommand(
            "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) \
             VALUES (@t, @fp, 1000.00, 200.00, 1200.00) RETURNING id", conn)
        cmd1.Parameters.AddWithValue("@t", tenant) |> ignore
        cmd1.Parameters.AddWithValue("@fp", fpId) |> ignore
        invoiceId <- cmd1.ExecuteScalar() :?> int
        // Try duplicate
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.invoice (tenant, fiscal_period_id, rent_amount, utility_share, total_amount) \
                     VALUES (@t, @fp, 500.00, 100.00, 600.00)"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@t", tenant) |> ignore
                        cmd.Parameters.AddWithValue("@fp", fpId) |> ignore)
        ConstraintAssert.assertUnique ex "Expected UNIQUE violation on ops.invoice tenant+fiscal_period_id"
    finally
        if invoiceId > 0 then
            try
                use cmd = new NpgsqlCommand("DELETE FROM ops.invoice WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", invoiceId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "CHL-007 invoice cleanup error: %s" ex.Message
        if fpId > 0 then
            try
                use cmd = new NpgsqlCommand("DELETE FROM ledger.fiscal_period WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", fpId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "CHL-007 fiscal_period cleanup error: %s" ex.Message

[<Fact>]
[<Trait("GherkinId", "FT-CHL-007")>]
let ``Shared assertFk detects FK violation in ops schema`` () =
    use conn = DataSource.openConnection()
    let ex = ConstraintAssert.tryExec conn
                "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence, source_account_id) VALUES ('Test', 'receivable', 'monthly', @sa)"
                (fun cmd -> cmd.Parameters.AddWithValue("@sa", 9999) |> ignore)
    ConstraintAssert.assertFk ex "Expected FK violation on ops.obligation_agreement.source_account_id"

// =====================================================================
// @FT-CHL-008 -- Shared assertSuccess confirms clean inserts
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-CHL-008")>]
let ``Shared assertSuccess helper confirms clean inserts`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let mutable oaId = 0
    let mutable instanceId = 0
    try
        use oaCmd = new NpgsqlCommand(
            "INSERT INTO ops.obligation_agreement (name, obligation_type, cadence) VALUES (@n, 'receivable', 'monthly') RETURNING id", conn)
        oaCmd.Parameters.AddWithValue("@n", prefix + "_agreement") |> ignore
        oaId <- oaCmd.ExecuteScalar() :?> int
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO ops.obligation_instance (obligation_agreement_id, name, status, expected_date) VALUES (@oa, @n, 'expected', '2026-04-01') RETURNING id"
                    (fun cmd ->
                        cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
                        cmd.Parameters.AddWithValue("@n", prefix + "_inst") |> ignore)
        ConstraintAssert.assertSuccess ex
        // capture instance id for cleanup if needed (ConstraintAssert.tryExec discards RETURNING result)
    finally
        // Delete instance first (FK), then agreement
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM ops.obligation_instance WHERE obligation_agreement_id = @oa", conn)
            cmd.Parameters.AddWithValue("@oa", oaId) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex -> eprintfn "CHL-008 instance cleanup error: %s" ex.Message
        if oaId > 0 then
            try
                use cmd = new NpgsqlCommand("DELETE FROM ops.obligation_agreement WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", oaId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "CHL-008 obligation_agreement cleanup error: %s" ex.Message

// =====================================================================
// Structural: optParam exists exactly once in Src/ tree (in DataHelpers.fs)
// =====================================================================

[<Fact>]
let ``optParam is defined exactly once in the Src tree and lives in DataHelpers.fs`` () =
    let srcDir = RepoPath.srcDir
    let fsFiles = Directory.GetFiles(srcDir, "*.fs", SearchOption.AllDirectories)
    let definitions =
        fsFiles
        // Exclude this test file -- it references optParam def patterns in string literals
        |> Array.filter (fun f -> not (f.EndsWith("ConsolidatedHelpersTests.fs")))
        |> Array.filter (fun f ->
            let content = File.ReadAllText(f)
            // Match "let optParam" or "let private optParam" -- any definition of optParam
            content.Contains("let optParam") || content.Contains("let private optParam"))
    Assert.Equal(1, definitions.Length)
    let defFile = definitions.[0]
    Assert.True(defFile.EndsWith("DataHelpers.fs"),
        $"optParam should be defined in DataHelpers.fs, found in: {defFile}")

// =====================================================================
// Structural: DataHelpers.fs is in LeoBloom.Utilities fsproj, after DataSource.fs
// =====================================================================

[<Fact>]
let ``DataHelpers.fs is in LeoBloom.Utilities fsproj after DataSource.fs`` () =
    let fsprojPath = Path.Combine(RepoPath.srcDir, "LeoBloom.Utilities", "LeoBloom.Utilities.fsproj")
    let content = File.ReadAllText(fsprojPath)
    let dsIndex = content.IndexOf("DataSource.fs")
    let dhIndex = content.IndexOf("DataHelpers.fs")
    Assert.True(dsIndex >= 0, "DataSource.fs should be in the fsproj")
    Assert.True(dhIndex >= 0, "DataHelpers.fs should be in the fsproj")
    Assert.True(dhIndex > dsIndex,
        $"DataHelpers.fs (at {dhIndex}) should appear after DataSource.fs (at {dsIndex}) in the fsproj")

// =====================================================================
// Structural: No let private optParam in any repository file
// =====================================================================

[<Fact>]
let ``No private optParam definitions remain in any repository file`` () =
    let repoFiles =
        [| "LeoBloom.Ledger/JournalEntryRepository.fs"
           "LeoBloom.Ops/ObligationAgreementRepository.fs"
           "LeoBloom.Ops/ObligationInstanceRepository.fs"
           "LeoBloom.Ops/TransferRepository.fs" |]
        |> Array.map (fun f -> Path.Combine(RepoPath.srcDir, f))

    let violations =
        repoFiles
        |> Array.filter (fun f ->
            File.Exists(f) && (File.ReadAllText(f).Contains("let private optParam")))
        |> Array.map (fun f -> f.Replace(RepoPath.repoRoot, ""))

    Assert.True(violations.Length = 0,
        sprintf "Found private optParam in: %s" (String.Join(", ", violations)))

// =====================================================================
// Structural: All 4 repository files call DataHelpers.optParam
// =====================================================================

[<Fact>]
let ``All repository files call DataHelpers.optParam`` () =
    let repoFiles =
        [| "LeoBloom.Ledger/JournalEntryRepository.fs"
           "LeoBloom.Ops/ObligationAgreementRepository.fs"
           "LeoBloom.Ops/ObligationInstanceRepository.fs"
           "LeoBloom.Ops/TransferRepository.fs" |]
        |> Array.map (fun f -> Path.Combine(RepoPath.srcDir, f))

    let missing =
        repoFiles
        |> Array.filter (fun f ->
            File.Exists(f) && not (File.ReadAllText(f).Contains("DataHelpers.optParam")))
        |> Array.map (fun f -> f.Replace(RepoPath.repoRoot, ""))

    Assert.True(missing.Length = 0,
        sprintf "Missing DataHelpers.optParam call in: %s" (String.Join(", ", missing)))

// =====================================================================
// Structural: repoRoot logic exists exactly once in TestHelpers.fs
// =====================================================================

[<Fact>]
let ``repoRoot logic exists exactly once in TestHelpers.fs RepoPath module`` () =
    let testFiles = Directory.GetFiles(Path.Combine(RepoPath.srcDir, "LeoBloom.Tests"), "*.fs")
    let filesWithRepoRoot =
        testFiles
        // Exclude this test file -- it references repoRoot in string literals for assertions
        |> Array.filter (fun f -> not (f.EndsWith("ConsolidatedHelpersTests.fs")))
        |> Array.filter (fun f ->
            let content = File.ReadAllText(f)
            // Look for a repoRoot definition (let, not just usage via RepoPath.repoRoot)
            content.Contains("let repoRoot") || content.Contains("let private repoRoot"))
    Assert.Equal(1, filesWithRepoRoot.Length)
    Assert.True(filesWithRepoRoot.[0].EndsWith("TestHelpers.fs"),
        $"repoRoot should only be defined in TestHelpers.fs, found in: {filesWithRepoRoot.[0]}")

// =====================================================================
// Structural: No private repoRoot in LogModuleStructureTests or LoggingInfrastructureTests
// =====================================================================

[<Fact>]
let ``No private repoRoot in LogModuleStructureTests or LoggingInfrastructureTests`` () =
    let files =
        [| "LeoBloom.Tests/LogModuleStructureTests.fs"
           "LeoBloom.Tests/LoggingInfrastructureTests.fs" |]
        |> Array.map (fun f -> Path.Combine(RepoPath.srcDir, f))

    let violations =
        files
        |> Array.filter (fun f ->
            File.Exists(f) && (File.ReadAllText(f).Contains("let private repoRoot")))
        |> Array.map (fun f -> f.Replace(RepoPath.repoRoot, ""))

    Assert.True(violations.Length = 0,
        sprintf "Found private repoRoot in: %s" (String.Join(", ", violations)))

// =====================================================================
// Structural: Constraint helpers exist exactly once in TestHelpers.fs
// =====================================================================

[<Fact>]
let ``Constraint helpers exist exactly once in TestHelpers.fs`` () =
    let helpers = [| "let tryExec"; "let tryInsert"; "let assertSqlState"; "let assertNotNull"; "let assertUnique"; "let assertFk"; "let assertSuccess" |]
    let testFiles =
        Directory.GetFiles(Path.Combine(RepoPath.srcDir, "LeoBloom.Tests"), "*.fs")
        // Exclude this test file -- it references helper names in string literals for assertions
        |> Array.filter (fun f -> not (f.EndsWith("ConsolidatedHelpersTests.fs")))

    for helper in helpers do
        let filesWithDef =
            testFiles
            |> Array.filter (fun f ->
                let content = File.ReadAllText(f)
                content.Contains(helper))
        let fileNames = filesWithDef |> Array.map Path.GetFileName
        let fileList = String.Join(", ", fileNames)
        Assert.True(filesWithDef.Length = 1,
            sprintf "'%s' should be defined in exactly 1 file, found in %d: %s" helper filesWithDef.Length fileList)
        Assert.True(filesWithDef.[0].EndsWith("TestHelpers.fs"),
            sprintf "'%s' should only be in TestHelpers.fs, found in: %s" helper (Path.GetFileName filesWithDef.[0]))

// =====================================================================
// Structural: No duplicate constraint definitions in LedgerConstraint/OpsConstraint tests
// =====================================================================

[<Fact>]
let ``No constraint helper definitions in LedgerConstraintTests or OpsConstraintTests`` () =
    let helperDefs = [| "let tryExec"; "let tryInsert"; "let assertSqlState"; "let assertNotNull"; "let assertUnique"; "let assertFk"; "let assertSuccess" |]
    let files =
        [| "LeoBloom.Tests/LedgerConstraintTests.fs"
           "LeoBloom.Tests/OpsConstraintTests.fs" |]
        |> Array.map (fun f -> Path.Combine(RepoPath.srcDir, f))

    let violations = ResizeArray<string>()
    for f in files do
        if File.Exists(f) then
            let content = File.ReadAllText(f)
            for h in helperDefs do
                if content.Contains(h) then
                    violations.Add($"{Path.GetFileName f} contains '{h}'")

    Assert.True(violations.Count = 0,
        sprintf "Found constraint helper definitions that should have been removed: %s"
            (String.Join("; ", violations)))
