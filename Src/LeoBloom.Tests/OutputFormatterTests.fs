module LeoBloom.Tests.OutputFormatterTests

open Xunit
open LeoBloom.Tests.CliFrameworkTests

// =====================================================================
// P072 — Empty-list message formatting (FT-FMT-001, FT-FMT-002)
//
// Verifies that formatInvoiceList and formatTransferList return a
// descriptive message instead of empty string when the list is empty.
// Tests exercise the CLI end-to-end so the full write path is covered.
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-FMT-001")>]
let ``invoice list with no matching results prints descriptive message`` () =
    // "NonexistentTenant" will never match any seeded or test data.
    let result = CliRunner.run "invoice list --tenant \"NonexistentTenant\""
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("(no invoices found)", stdout)

[<Fact>]
[<Trait("GherkinId", "FT-FMT-002")>]
let ``transfer list with no matching results prints descriptive message`` () =
    // Use a far-future date range guaranteed to have no transfers.
    // (Gherkin spec wrote "--status pending" but "pending" is not a valid
    //  TransferStatus; this date-range filter achieves the same intent:
    //  an empty result set → descriptive empty-list message.)
    let result = CliRunner.run "transfer list --from 2098-01-01 --to 2098-01-02"
    Assert.Equal(0, result.ExitCode)
    let stdout = CliRunner.stripLogLines result.Stdout
    Assert.Contains("(no transfers found)", stdout)
