module LeoBloom.Tests.DomainTests

open System
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Domain.Ops

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

// --- ObligationDirection conversion tests ---

[<Theory>]
[<InlineData("receivable")>]
[<InlineData("payable")>]
let ``ObligationDirection round-trips`` (s: string) =
    match ObligationDirection.fromString s with
    | Ok v -> Assert.Equal(s, ObligationDirection.toString v)
    | Error msg -> Assert.Fail(msg)

[<Fact>]
let ``ObligationDirection.fromString rejects invalid`` () =
    match ObligationDirection.fromString "invalid" with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

// --- InstanceStatus conversion tests ---

[<Theory>]
[<InlineData("expected")>]
[<InlineData("in_flight")>]
[<InlineData("confirmed")>]
[<InlineData("posted")>]
[<InlineData("overdue")>]
[<InlineData("skipped")>]
let ``InstanceStatus round-trips`` (s: string) =
    match InstanceStatus.fromString s with
    | Ok v -> Assert.Equal(s, InstanceStatus.toString v)
    | Error msg -> Assert.Fail(msg)

[<Fact>]
let ``InstanceStatus.fromString rejects invalid`` () =
    match InstanceStatus.fromString "invalid" with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

// --- RecurrenceCadence conversion tests ---

[<Theory>]
[<InlineData("monthly")>]
[<InlineData("quarterly")>]
[<InlineData("annual")>]
[<InlineData("one_time")>]
let ``RecurrenceCadence round-trips`` (s: string) =
    match RecurrenceCadence.fromString s with
    | Ok v -> Assert.Equal(s, RecurrenceCadence.toString v)
    | Error msg -> Assert.Fail(msg)

[<Fact>]
let ``RecurrenceCadence.fromString rejects invalid`` () =
    match RecurrenceCadence.fromString "invalid" with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")

// --- PaymentMethodType conversion tests ---

[<Theory>]
[<InlineData("autopay_pull")>]
[<InlineData("ach")>]
[<InlineData("zelle")>]
[<InlineData("cheque")>]
[<InlineData("bill_pay")>]
[<InlineData("manual")>]
let ``PaymentMethodType round-trips`` (s: string) =
    match PaymentMethodType.fromString s with
    | Ok v -> Assert.Equal(s, PaymentMethodType.toString v)
    | Error msg -> Assert.Fail(msg)

[<Fact>]
let ``PaymentMethodType.fromString rejects invalid`` () =
    match PaymentMethodType.fromString "invalid" with
    | Error _ -> ()
    | Ok _ -> Assert.Fail("Expected Error")
