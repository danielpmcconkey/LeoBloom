module LeoBloom.Domain.Tests.LedgerValidationTests

open System
open Xunit
open LeoBloom.Domain.Ledger

let makeLine (accountId: int) (amount: decimal) (entryType: EntryType) : JournalEntryLine =
    { id = 0
      journalEntryId = 0
      accountId = accountId
      amount = amount
      entryType = entryType
      memo = None }

// --- Balance rule tests ---

[<Fact>]
let ``balanced two-line entry passes`` () =
    let lines =
        [ makeLine 1 100.00m EntryType.Debit
          makeLine 2 100.00m EntryType.Credit ]
    match validateBalanced lines with
    | Ok _ -> ()
    | Error msgs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" msgs)

[<Fact>]
let ``unbalanced entry fails`` () =
    let lines =
        [ makeLine 1 100.00m EntryType.Debit
          makeLine 2 50.00m EntryType.Credit ]
    match validateBalanced lines with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

[<Fact>]
let ``balanced compound entry (3+ lines) passes`` () =
    let lines =
        [ makeLine 1 100.00m EntryType.Debit
          makeLine 2 60.00m EntryType.Credit
          makeLine 3 40.00m EntryType.Credit ]
    match validateBalanced lines with
    | Ok _ -> ()
    | Error msgs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" msgs)

// --- Amount positivity tests ---

[<Fact>]
let ``positive amounts pass`` () =
    let lines =
        [ makeLine 1 100.00m EntryType.Debit
          makeLine 2 100.00m EntryType.Credit ]
    match validateAmountsPositive lines with
    | Ok _ -> ()
    | Error msgs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" msgs)

[<Fact>]
let ``zero amount fails`` () =
    let lines =
        [ makeLine 1 0m EntryType.Debit
          makeLine 2 100.00m EntryType.Credit ]
    match validateAmountsPositive lines with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

[<Fact>]
let ``negative amount fails`` () =
    let lines =
        [ makeLine 1 -50.00m EntryType.Debit
          makeLine 2 100.00m EntryType.Credit ]
    match validateAmountsPositive lines with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

// --- Minimum line count tests ---

[<Fact>]
let ``zero lines fails`` () =
    match validateMinimumLineCount [] with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

[<Fact>]
let ``one line fails`` () =
    let lines = [ makeLine 1 100.00m EntryType.Debit ]
    match validateMinimumLineCount lines with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

[<Fact>]
let ``two lines passes`` () =
    let lines =
        [ makeLine 1 100.00m EntryType.Debit
          makeLine 2 100.00m EntryType.Credit ]
    match validateMinimumLineCount lines with
    | Ok _ -> ()
    | Error msgs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" msgs)

[<Fact>]
let ``three lines passes`` () =
    let lines =
        [ makeLine 1 100.00m EntryType.Debit
          makeLine 2 60.00m EntryType.Credit
          makeLine 3 40.00m EntryType.Credit ]
    match validateMinimumLineCount lines with
    | Ok _ -> ()
    | Error msgs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" msgs)

// --- Void reason tests ---

[<Fact>]
let ``voidedAt None needs no reason`` () =
    match validateVoidReason None None with
    | Ok _ -> ()
    | Error msgs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" msgs)

[<Fact>]
let ``voidedAt Some with non-empty reason passes`` () =
    match validateVoidReason (Some DateTimeOffset.UtcNow) (Some "Duplicate entry") with
    | Ok _ -> ()
    | Error msgs -> Assert.Fail(sprintf "Expected Ok, got Error: %A" msgs)

[<Fact>]
let ``voidedAt Some with reason None fails`` () =
    match validateVoidReason (Some DateTimeOffset.UtcNow) None with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

[<Fact>]
let ``voidedAt Some with empty reason fails`` () =
    match validateVoidReason (Some DateTimeOffset.UtcNow) (Some "") with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")
