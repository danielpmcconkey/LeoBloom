namespace LeoBloom.Reporting

/// COA-to-Schedule E line mapping.
/// Pure data — no I/O.
module ScheduleEMapping =

    /// Maps a set of account codes to a Schedule E line number and description.
    type ScheduleELineMapping =
        { lineNumber: int
          lineDescription: string
          accountCodes: string list }

    /// The canonical mapping from COA account codes to IRS Schedule E lines.
    /// Derived from 1712000006000_SeedChartOfAccounts.sql.
    let scheduleELineMappings : ScheduleELineMapping list =
        [ { lineNumber = 3
            lineDescription = "Rents received"
            accountCodes = [ "4110"; "4120"; "4130"; "4140"; "4150" ] }
          { lineNumber = 9
            lineDescription = "Insurance"
            accountCodes = [ "5150" ] }
          { lineNumber = 12
            lineDescription = "Mortgage interest paid to banks, etc."
            accountCodes = [ "5110" ] }
          { lineNumber = 14
            lineDescription = "Repairs"
            accountCodes = [ "5170" ] }
          { lineNumber = 16
            lineDescription = "Taxes"
            accountCodes = [ "5140" ] }
          { lineNumber = 18
            lineDescription = "Depreciation expense or depletion"
            accountCodes = [ "5190" ] }
          { lineNumber = 19
            lineDescription = "Other"
            accountCodes = [ "5160"; "5120"; "5130"; "5200"; "5210"; "5180" ] } ]

    /// Sub-detail descriptions for line 19 "Other" accounts.
    let line19SubDetail : Map<string, string> =
        Map.ofList
            [ "5160", "HOA Dues"
              "5120", "Water & Electric"
              "5130", "Gas"
              "5200", "Lawn Care"
              "5210", "Pest Control"
              "5180", "Supplies" ]

    /// All account codes used in Schedule E mappings (for querying).
    let allMappedAccountCodes : string list =
        scheduleELineMappings
        |> List.collect (fun m -> m.accountCodes)
