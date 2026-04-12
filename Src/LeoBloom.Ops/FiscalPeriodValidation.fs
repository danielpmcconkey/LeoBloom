namespace LeoBloom.Ops

open System
open Npgsql
open LeoBloom.Domain.Ops
open LeoBloom.Ledger
open LeoBloom.Utilities

/// Validation check identifiers for pre-close validation.
type ValidationCheck =
    | TrialBalanceEquilibrium
    | BalanceSheetEquation
    | DataHygiene
    | OpenObligations

/// Result of a single validation check.
type CheckResult =
    { Check: ValidationCheck
      Passed: bool
      Message: string }

/// Aggregate result of all pre-close validation checks.
type PreCloseValidationResult =
    { PeriodId: int
      Checks: CheckResult list
      AllPassed: bool }

/// Runs the four GAAP-informed pre-close validation checks for a fiscal period.
module FiscalPeriodValidation =

    // --- Individual checks ---

    let private checkTrialBalanceEquilibrium (txn: NpgsqlTransaction) (periodId: int) : CheckResult =
        use sql = new NpgsqlCommand(
            "SELECT \
               COALESCE(SUM(CASE WHEN jel.entry_type = 'debit'  THEN jel.amount ELSE 0 END), 0) AS total_debits, \
               COALESCE(SUM(CASE WHEN jel.entry_type = 'credit' THEN jel.amount ELSE 0 END), 0) AS total_credits \
             FROM ledger.journal_entry je \
             JOIN ledger.journal_entry_line jel ON jel.journal_entry_id = je.id \
             WHERE je.fiscal_period_id = @period_id \
               AND je.voided_at IS NULL",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@period_id", periodId) |> ignore
        use reader = sql.ExecuteReader()
        reader.Read() |> ignore
        let totalDebits  = reader.GetDecimal(0)
        let totalCredits = reader.GetDecimal(1)
        reader.Close()
        if totalDebits = totalCredits then
            { Check = TrialBalanceEquilibrium
              Passed = true
              Message = sprintf "Trial balance is in equilibrium: debits = credits = %M" totalDebits }
        else
            { Check = TrialBalanceEquilibrium
              Passed = false
              Message = sprintf "Trial balance disequilibrium: debits = %M, credits = %M" totalDebits totalCredits }

    let private checkBalanceSheetEquation (txn: NpgsqlTransaction) (endDate: DateOnly) : CheckResult =
        match BalanceSheetService.getAsOfDate txn endDate with
        | Error msg ->
            { Check = BalanceSheetEquation
              Passed = false
              Message = sprintf "Balance sheet query failed: %s" msg }
        | Ok report ->
            if report.isBalanced then
                { Check = BalanceSheetEquation
                  Passed = true
                  Message = sprintf "Balance sheet equation holds: assets = %M, liabilities + equity = %M"
                                report.assets.sectionTotal
                                (report.liabilities.sectionTotal + report.totalEquity) }
            else
                { Check = BalanceSheetEquation
                  Passed = false
                  Message = sprintf "Balance sheet equation violated: assets = %M, liabilities + equity = %M"
                                report.assets.sectionTotal
                                (report.liabilities.sectionTotal + report.totalEquity) }

    let private checkDataHygiene (txn: NpgsqlTransaction) (periodId: int) (startDate: DateOnly) (endDate: DateOnly) : CheckResult =
        // Sub-check 1: voided JEs with NULL void_reason
        let voidedWithNullReason =
            use sql = new NpgsqlCommand(
                "SELECT id FROM ledger.journal_entry \
                 WHERE fiscal_period_id = @period_id \
                   AND voided_at IS NOT NULL \
                   AND void_reason IS NULL",
                txn.Connection, txn)
            sql.Parameters.AddWithValue("@period_id", periodId) |> ignore
            use reader = sql.ExecuteReader()
            let mutable ids = []
            while reader.Read() do
                ids <- reader.GetInt32(0) :: ids
            reader.Close()
            ids |> List.rev

        // Sub-check 2: JEs with entry_date outside period range
        let outOfRangeIds =
            use sql = new NpgsqlCommand(
                "SELECT id FROM ledger.journal_entry \
                 WHERE fiscal_period_id = @period_id \
                   AND (entry_date < @start_date OR entry_date > @end_date)",
                txn.Connection, txn)
            sql.Parameters.AddWithValue("@period_id", periodId) |> ignore
            sql.Parameters.AddWithValue("@start_date", startDate) |> ignore
            sql.Parameters.AddWithValue("@end_date", endDate) |> ignore
            use reader = sql.ExecuteReader()
            let mutable ids = []
            while reader.Read() do
                ids <- reader.GetInt32(0) :: ids
            reader.Close()
            ids |> List.rev

        let failures = ResizeArray<string>()
        if voidedWithNullReason <> [] then
            let ids = voidedWithNullReason |> List.map string |> String.concat ", "
            failures.Add(sprintf "Voided JEs with no void_reason: [%s]" ids)
        if outOfRangeIds <> [] then
            let ids = outOfRangeIds |> List.map string |> String.concat ", "
            failures.Add(sprintf "JEs with entry_date outside period range: [%s]" ids)

        if failures.Count = 0 then
            { Check = DataHygiene
              Passed = true
              Message = "Data hygiene checks passed: no voided JEs missing void_reason, no out-of-range entry dates" }
        else
            { Check = DataHygiene
              Passed = false
              Message = String.concat "; " failures }

    let private checkOpenObligations (txn: NpgsqlTransaction) (startDate: DateOnly) (endDate: DateOnly) : CheckResult =
        use sql = new NpgsqlCommand(
            "SELECT oi.id, oa.name \
             FROM ops.obligation_instance oi \
             JOIN ops.obligation_agreement oa ON oa.id = oi.obligation_agreement_id \
             WHERE oi.status = 'in_flight' \
               AND oi.expected_date >= @start_date \
               AND oi.expected_date <= @end_date",
            txn.Connection, txn)
        sql.Parameters.AddWithValue("@start_date", startDate) |> ignore
        sql.Parameters.AddWithValue("@end_date", endDate) |> ignore
        use reader = sql.ExecuteReader()
        let mutable inflight = []
        while reader.Read() do
            inflight <- (reader.GetInt32(0), reader.GetString(1)) :: inflight
        reader.Close()
        let inflight = inflight |> List.rev

        if inflight = [] then
            { Check = OpenObligations
              Passed = true
              Message = "No in-flight obligation instances in period" }
        else
            let detail =
                inflight
                |> List.map (fun (id, agrName) -> sprintf "instance %d (%s)" id agrName)
                |> String.concat ", "
            { Check = OpenObligations
              Passed = false
              Message = sprintf "In-flight obligation instances blocking close: %s" detail }

    // --- Main entry point ---

    /// Runs all four pre-close validation checks for the given fiscal period.
    /// Returns a PreCloseValidationResult with all check results regardless of individual failures.
    let validatePreClose (txn: NpgsqlTransaction) (periodId: int) : Result<PreCloseValidationResult, string list> =
        Log.info "Running pre-close validation for fiscal period {PeriodId}" [| periodId :> obj |]
        try
            match FiscalPeriodRepository.findById txn periodId with
            | None ->
                Error [ sprintf "Fiscal period with id %d does not exist" periodId ]
            | Some period ->
                let checks =
                    [ checkTrialBalanceEquilibrium txn periodId
                      checkBalanceSheetEquation txn period.endDate
                      checkDataHygiene txn periodId period.startDate period.endDate
                      checkOpenObligations txn period.startDate period.endDate ]
                let allPassed = checks |> List.forall (fun c -> c.Passed)
                Log.info "Pre-close validation for period {PeriodId}: allPassed={AllPassed}" [| periodId :> obj; allPassed :> obj |]
                Ok { PeriodId = periodId; Checks = checks; AllPassed = allPassed }
        with ex ->
            Log.errorExn ex "Pre-close validation failed for period {PeriodId}" [| periodId :> obj |]
            Error [ sprintf "Validation error: %s" ex.Message ]
