module LeoBloom.Tests.NormalBalanceResolutionTests

open System
open System.IO
open Xunit
open LeoBloom.Domain.Ledger
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Structural: resolveBalance is accessible from LeoBloom.Domain.Ledger
// (AC-1) The fact that the [<Theory>] below compiles proves it.
// =====================================================================

// =====================================================================
// @FT-NBC-001 — resolveBalance arithmetic is correct for all cases
// =====================================================================

[<Theory>]
[<Trait("GherkinId", "FT-NBC-001")>]
[<InlineData("Debit",  1000.00, 400.00,   600.00)>]
[<InlineData("Debit",  400.00,  1000.00, -600.00)>]
[<InlineData("Debit",  500.00,  500.00,    0.00)>]
[<InlineData("Debit",  0.00,    0.00,      0.00)>]
[<InlineData("Credit", 400.00,  1000.00,  600.00)>]
[<InlineData("Credit", 1000.00, 400.00,  -600.00)>]
[<InlineData("Credit", 500.00,  500.00,    0.00)>]
[<InlineData("Credit", 0.00,    0.00,      0.00)>]
let ``resolveBalance computes directional balance by normal balance type``
    (normalBalanceStr: string) (debits: decimal) (credits: decimal) (expected: decimal) =
    let nb =
        match normalBalanceStr with
        | "Debit"  -> NormalBalance.Debit
        | _        -> NormalBalance.Credit
    let result = resolveBalance nb debits credits
    Assert.Equal(expected, result)

// =====================================================================
// @FT-NBC-002 — No inline normal-balance arithmetic outside Domain
// =====================================================================
//
// The consolidation goal is defeated if call sites re-implement their own
// debit/credit arithmetic instead of delegating to resolveBalance.
// This test enforces that guardrail at the source level.
//
// Patterns that must NOT appear in any .fs file outside LeoBloom.Domain:
//   debitTotal - creditTotal
//   creditTotal - debitTotal
//   -rawBalance   (the sign-flip pattern formerly in AccountBalanceRepository)
//
// Strings are split across two literals to prevent this test file itself
// from tripping the search.

[<Fact>]
[<Trait("GherkinId", "FT-NBC-002")>]
let ``No inline normal-balance arithmetic exists outside the Domain module`` () =
    let pattern1 = "debitTotal - " + "creditTotal"
    let pattern2 = "creditTotal - " + "debitTotal"
    let pattern3 = "-" + "rawBalance"

    let fsFiles =
        Directory.GetFiles(RepoPath.srcDir, "*.fs", SearchOption.AllDirectories)
        |> Array.filter (fun f ->
            not (f.Contains("LeoBloom.Domain", StringComparison.OrdinalIgnoreCase))
            && not (f.Contains("LeoBloom.Tests", StringComparison.OrdinalIgnoreCase)))

    let violations =
        fsFiles
        |> Array.choose (fun f ->
            let content = File.ReadAllText(f)
            let hits =
                [ if content.Contains(pattern1) then yield $"  debitTotal - creditTotal"
                  if content.Contains(pattern2) then yield $"  creditTotal - debitTotal"
                  if content.Contains(pattern3) then yield $"  -rawBalance" ]
            if hits.IsEmpty then None
            else Some (f.Replace(RepoPath.repoRoot, "") + ":\n" + String.concat "\n" hits))

    let msg =
        "Inline normal-balance arithmetic found outside LeoBloom.Domain — use resolveBalance instead:\n"
        + String.concat "\n" violations

    Assert.True(violations.Length = 0, msg)
