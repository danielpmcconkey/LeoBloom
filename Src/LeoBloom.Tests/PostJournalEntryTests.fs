module LeoBloom.Tests.PostJournalEntryTests

open System
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities
open LeoBloom.Ledger
open LeoBloom.Tests.TestHelpers

[<Fact>]
[<Trait("GherkinId", "FT-PJE-001")>]
let ``simple two-line entry posts successfully`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Test entry"
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
        Assert.True(posted.entry.createdAt > DateTimeOffset.MinValue)
        Assert.Equal(2, List.length posted.lines)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-002")>]
let ``compound three-line entry posts successfully`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let expAt = InsertHelpers.insertAccountType txn (prefix + "_ex") "debit"
    let liabAt = InsertHelpers.insertAccountType txn (prefix + "_li") "credit"
    let astAt = InsertHelpers.insertAccountType txn (prefix + "_as") "debit"
    let expense = InsertHelpers.insertAccount txn (prefix + "EX") "Expense" expAt true
    let liability = InsertHelpers.insertAccount txn (prefix + "LI") "Liability" liabAt true
    let asset = InsertHelpers.insertAccount txn (prefix + "AS") "Asset" astAt true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Compound entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = expense; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = liability; amount = 300m; entryType = EntryType.Debit; memo = None }
              { accountId = asset; amount = 800m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.Equal(3, List.length posted.lines)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-003")>]
let ``entry with references posts successfully`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Entry with refs"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references =
            [ { referenceType = "cheque"; referenceValue = "12345" }
              { referenceType = "zelle_confirmation"; referenceValue = "ZEL-9876" } ]
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.Equal(2, List.length posted.references)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-004")>]
let ``null source accepted`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "No source entry"
          source = None
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 250m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 250m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.True(posted.entry.source.IsNone)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-005")>]
let ``memo on lines preserved`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Memo entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 750m; entryType = EntryType.Debit; memo = Some "Debit memo" }
              { accountId = acct2; amount = 750m; entryType = EntryType.Credit; memo = Some "Credit memo" } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.True(posted.lines |> List.forall (fun l -> l.memo.IsSome))
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-006")>]
let ``unbalanced entry rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Unbalanced entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for unbalanced entry")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("do not equal")),
                    sprintf "Expected error containing 'do not equal': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-007")>]
let ``zero amount rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Zero amount entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 0m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 0m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for zero amount")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("non-positive amount")),
                    sprintf "Expected error containing 'non-positive amount': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-008")>]
let ``negative amount rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Negative amount entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = -100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = -100m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for negative amount")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("non-positive amount")),
                    sprintf "Expected error containing 'non-positive amount': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-009")>]
let ``single line rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Single line entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for single line")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("at least 2 lines")),
                    sprintf "Expected error containing 'at least 2 lines': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-010")>]
let ``empty description rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = ""
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for empty description")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("Description")),
                    sprintf "Expected error containing 'Description': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-011")>]
let ``invalid entry type returns error`` () =
    let result = EntryType.fromString "foo"
    match result with
    | Ok _ -> Assert.Fail("Expected Error for invalid entry type")
    | Error err ->
        Assert.True(true)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-012")>]
let ``empty source string rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Empty source entry"
          source = Some ""
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 200m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 200m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for empty source")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("Source")),
                    sprintf "Expected error containing 'Source': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-013")>]
let ``closed fiscal period rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) false
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Closed period entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for closed fiscal period")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("not open")),
                    sprintf "Expected error containing 'not open': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-014")>]
let ``entry date outside period range rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 4, 15)
          description = "Outside period entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for date outside period")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("outside")),
                    sprintf "Expected error containing 'outside': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-015")>]
let ``inactive account rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Active" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Inactive" atId false
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Inactive account entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for inactive account")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("inactive")),
                    sprintf "Expected error containing 'inactive': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-016")>]
let ``nonexistent fiscal period rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Bad period entry"
          source = Some "manual"
          fiscalPeriodId = 99999
          lines =
            [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for nonexistent fiscal period")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("does not exist")),
                    sprintf "Expected error containing 'does not exist': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-017")>]
let ``empty reference type rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Empty ref type entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references =
            [ { referenceType = ""; referenceValue = "some-value" } ]
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for empty reference type")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("reference_type")),
                    sprintf "Expected error containing 'reference_type': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-018")>]
let ``empty reference value rejected`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = "Empty ref value entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references =
            [ { referenceType = "cheque"; referenceValue = "" } ]
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for empty reference value")
    | Error errs ->
        Assert.True(errs |> List.exists (fun e -> e.Contains("reference_value")),
                    sprintf "Expected error containing 'reference_value': %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-019")>]
let ``validation failure leaves no persisted rows`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let testDesc = sprintf "NoRows_%s" prefix
    let cmd =
        { entryDate = DateOnly(2026, 3, 15)
          description = testDesc
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 1000m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok _ -> Assert.Fail("Expected Error for unbalanced entry")
    | Error _ -> ()
    // Verify no rows were persisted (within same transaction scope)
    use checkCmd = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM journal_entry WHERE description = @desc", txn.Connection)
    checkCmd.Transaction <- txn
    let p = checkCmd.CreateParameter()
    p.ParameterName <- "@desc"
    p.Value <- testDesc
    checkCmd.Parameters.Add(p) |> ignore
    let count = checkCmd.ExecuteScalar() :?> int64
    Assert.Equal(0L, count)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-020")>]
let ``duplicate references across entries allowed`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 3, 1)) (DateOnly(2026, 3, 31)) true
    let sharedRef = { referenceType = "cheque"; referenceValue = sprintf "DUP-%s" prefix }
    // First entry
    let cmd1 =
        { entryDate = DateOnly(2026, 3, 15)
          description = "First dup ref entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 100m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 100m; entryType = EntryType.Credit; memo = None } ]
          references = [ sharedRef ]
          adjustmentForPeriodId = None }
    match JournalEntryService.post txn cmd1 with
    | Ok _ -> ()
    | Error errs -> Assert.Fail(sprintf "First entry failed: %A" errs)
    // Second entry with same reference
    let cmd2 =
        { entryDate = DateOnly(2026, 3, 16)
          description = "Second dup ref entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 200m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 200m; entryType = EntryType.Credit; memo = None } ]
          references = [ sharedRef ]
          adjustmentForPeriodId = None }
    let result2 = JournalEntryService.post txn cmd2
    match result2 with
    | Ok _ ->
        Assert.True(true)
    | Error errs -> Assert.Fail(sprintf "Second entry with dup ref failed: %A" errs)

[<Fact>]
[<Trait("GherkinId", "FT-PJE-021")>]
let ``future entry date with valid open period succeeds`` () =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    let prefix = TestData.uniquePrefix()
    let atId = InsertHelpers.insertAccountType txn (prefix + "_at") "debit"
    let acct1 = InsertHelpers.insertAccount txn (prefix + "A1") "Acct1" atId true
    let acct2 = InsertHelpers.insertAccount txn (prefix + "A2") "Acct2" atId true
    let fpId = InsertHelpers.insertFiscalPeriod txn (prefix + "FP") (DateOnly(2026, 6, 1)) (DateOnly(2026, 6, 30)) true
    let cmd =
        { entryDate = DateOnly(2026, 6, 15)
          description = "Future date entry"
          source = Some "manual"
          fiscalPeriodId = fpId
          lines =
            [ { accountId = acct1; amount = 500m; entryType = EntryType.Debit; memo = None }
              { accountId = acct2; amount = 500m; entryType = EntryType.Credit; memo = None } ]
          references = []
          adjustmentForPeriodId = None }
    let result = JournalEntryService.post txn cmd
    match result with
    | Ok posted ->
        Assert.True(posted.entry.id > 0)
    | Error errs -> Assert.Fail(sprintf "Expected Ok: %A" errs)
