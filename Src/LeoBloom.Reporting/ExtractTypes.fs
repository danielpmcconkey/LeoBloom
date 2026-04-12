namespace LeoBloom.Reporting

open System
open System.Text.Json.Serialization

/// Record types for the four reporting data extracts.
/// Fields use [<JsonPropertyName>] to produce snake_case JSON output.
module ExtractTypes =

    [<CLIMutable>]
    type AccountTreeRow =
        { [<JsonPropertyName("id")>] id: int
          [<JsonPropertyName("code")>] code: string
          [<JsonPropertyName("name")>] name: string
          [<JsonPropertyName("parent_id")>] parentId: int option
          [<JsonPropertyName("account_type")>] accountType: string
          [<JsonPropertyName("normal_balance")>] normalBalance: string
          [<JsonPropertyName("subtype")>] subtype: string option
          [<JsonPropertyName("is_active")>] isActive: bool }

    [<CLIMutable>]
    type AccountBalanceRow =
        { [<JsonPropertyName("account_id")>] accountId: int
          [<JsonPropertyName("code")>] code: string
          [<JsonPropertyName("name")>] name: string
          [<JsonPropertyName("balance")>] balance: decimal }

    [<CLIMutable>]
    type PortfolioPositionRow =
        { [<JsonPropertyName("investment_account_id")>] investmentAccountId: int
          [<JsonPropertyName("investment_account_name")>] investmentAccountName: string
          [<JsonPropertyName("tax_bucket")>] taxBucket: string
          [<JsonPropertyName("symbol")>] symbol: string
          [<JsonPropertyName("fund_name")>] fundName: string
          [<JsonPropertyName("position_date")>] positionDate: DateOnly
          [<JsonPropertyName("price")>] price: decimal
          [<JsonPropertyName("quantity")>] quantity: decimal
          [<JsonPropertyName("current_value")>] currentValue: decimal
          [<JsonPropertyName("cost_basis")>] costBasis: decimal }

    [<CLIMutable>]
    type JournalEntryLineRow =
        { [<JsonPropertyName("journal_entry_id")>] journalEntryId: int
          [<JsonPropertyName("entry_date")>] entryDate: DateOnly
          [<JsonPropertyName("description")>] description: string
          [<JsonPropertyName("source")>] source: string option
          [<JsonPropertyName("account_id")>] accountId: int
          [<JsonPropertyName("account_code")>] accountCode: string
          [<JsonPropertyName("account_name")>] accountName: string
          [<JsonPropertyName("amount")>] amount: decimal
          [<JsonPropertyName("entry_type")>] entryType: string
          [<JsonPropertyName("memo")>] memo: string option }

    [<CLIMutable>]
    type PeriodMetadataEnvelope =
        { [<JsonPropertyName("period_key")>] periodKey: string
          [<JsonPropertyName("start_date")>] startDate: DateOnly
          [<JsonPropertyName("end_date")>] endDate: DateOnly
          [<JsonPropertyName("status")>] status: string
          [<JsonPropertyName("closed_at")>] closedAt: DateTimeOffset option
          [<JsonPropertyName("closed_by")>] closedBy: string option
          [<JsonPropertyName("reopened_count")>] reopenedCount: int
          [<JsonPropertyName("adjustment_count")>] adjustmentCount: int
          [<JsonPropertyName("adjustment_net_impact")>] adjustmentNetImpact: decimal
          [<JsonPropertyName("lines")>] lines: JournalEntryLineRow list }
