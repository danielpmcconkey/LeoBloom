module LeoBloom.CLI.ObligationCommands

open System
open Argu
open LeoBloom.Domain.Ops
open LeoBloom.Ops
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter

// --- Agreement leaf arg types ---

type AgreementListArgs =
    | Type of string
    | Cadence of string
    | Inactive
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Type _ -> "Filter by type (receivable|payable)"
            | Cadence _ -> "Filter by cadence (monthly|quarterly|annual|one_time)"
            | Inactive -> "Include inactive agreements"
            | Json -> "Output in JSON format"

type AgreementShowArgs =
    | [<MainCommand; Mandatory>] Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Id _ -> "Agreement ID"
            | Json -> "Output in JSON format"

type AgreementCreateArgs =
    | [<Mandatory>] Name of string
    | [<Mandatory>] Type of string
    | [<Mandatory>] Cadence of string
    | Counterparty of string
    | Amount of decimal
    | Expected_Day of int
    | Payment_Method of string
    | Source_Account of int
    | Dest_Account of int
    | Notes of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Name _ -> "Agreement name"
            | Type _ -> "Obligation type (receivable|payable)"
            | Cadence _ -> "Recurrence cadence (monthly|quarterly|annual|one_time)"
            | Counterparty _ -> "Counterparty name"
            | Amount _ -> "Expected amount"
            | Expected_Day _ -> "Expected day of month (1-31)"
            | Payment_Method _ -> "Payment method (autopay_pull|ach|zelle|cheque|bill_pay|manual)"
            | Source_Account _ -> "Source account ID"
            | Dest_Account _ -> "Destination account ID"
            | Notes _ -> "Notes"
            | Json -> "Output in JSON format"

type AgreementUpdateArgs =
    | [<MainCommand; Mandatory>] Id of int
    | [<Mandatory>] Name of string
    | [<Mandatory>] Type of string
    | [<Mandatory>] Cadence of string
    | Counterparty of string
    | Amount of decimal
    | Expected_Day of int
    | Payment_Method of string
    | Source_Account of int
    | Dest_Account of int
    | Inactive
    | Notes of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Id _ -> "Agreement ID"
            | Name _ -> "Agreement name"
            | Type _ -> "Obligation type (receivable|payable)"
            | Cadence _ -> "Recurrence cadence (monthly|quarterly|annual|one_time)"
            | Counterparty _ -> "Counterparty name"
            | Amount _ -> "Expected amount"
            | Expected_Day _ -> "Expected day of month (1-31)"
            | Payment_Method _ -> "Payment method (autopay_pull|ach|zelle|cheque|bill_pay|manual)"
            | Source_Account _ -> "Source account ID"
            | Dest_Account _ -> "Destination account ID"
            | Inactive -> "Mark agreement as inactive"
            | Notes _ -> "Notes"
            | Json -> "Output in JSON format"

type AgreementDeactivateArgs =
    | [<MainCommand; Mandatory>] Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Id _ -> "Agreement ID"
            | Json -> "Output in JSON format"

type AgreementArgs =
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<AgreementListArgs>
    | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<AgreementShowArgs>
    | [<CliPrefix(CliPrefix.None)>] Create of ParseResults<AgreementCreateArgs>
    | [<CliPrefix(CliPrefix.None)>] Update of ParseResults<AgreementUpdateArgs>
    | [<CliPrefix(CliPrefix.None)>] Deactivate of ParseResults<AgreementDeactivateArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | List _ -> "List obligation agreements"
            | Show _ -> "Show agreement details"
            | Create _ -> "Create a new agreement"
            | Update _ -> "Update an existing agreement"
            | Deactivate _ -> "Deactivate an agreement"

// --- Instance leaf arg types ---

type InstanceListArgs =
    | Status of string
    | Due_Before of string
    | Due_After of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Status _ -> "Filter by status (expected|in_flight|confirmed|posted|overdue|skipped)"
            | Due_Before _ -> "Filter by expected_date <= DATE (yyyy-MM-dd)"
            | Due_After _ -> "Filter by expected_date >= DATE (yyyy-MM-dd)"
            | Json -> "Output in JSON format"

type InstanceSpawnArgs =
    | [<MainCommand; Mandatory>] Agreement_Id of int
    | [<Mandatory>] From of string
    | [<Mandatory>] To of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Agreement_Id _ -> "Obligation agreement ID"
            | From _ -> "Start date (yyyy-MM-dd)"
            | To _ -> "End date (yyyy-MM-dd)"
            | Json -> "Output in JSON format"

type InstanceTransitionArgs =
    | [<MainCommand; Mandatory>] Instance_Id of int
    | [<Mandatory>] To of string
    | Amount of decimal
    | Date of string
    | Notes of string
    | Journal_Entry_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Instance_Id _ -> "Instance ID"
            | To _ -> "Target status (in_flight|confirmed|posted|overdue|skipped)"
            | Amount _ -> "Confirmed amount"
            | Date _ -> "Confirmed date (yyyy-MM-dd)"
            | Notes _ -> "Notes"
            | Journal_Entry_Id _ -> "Journal entry ID (required for posted transition)"
            | Json -> "Output in JSON format"

type InstancePostArgs =
    | [<MainCommand; Mandatory>] Instance_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Instance_Id _ -> "Instance ID"
            | Json -> "Output in JSON format"

type InstanceArgs =
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<InstanceListArgs>
    | [<CliPrefix(CliPrefix.None)>] Spawn of ParseResults<InstanceSpawnArgs>
    | [<CliPrefix(CliPrefix.None)>] Transition of ParseResults<InstanceTransitionArgs>
    | [<CliPrefix(CliPrefix.None)>] Post of ParseResults<InstancePostArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | List _ -> "List obligation instances"
            | Spawn _ -> "Spawn instances for an agreement"
            | Transition _ -> "Transition an instance to a new status"
            | Post _ -> "Post a confirmed instance to the ledger"

// --- Top-level obligation arg types ---

type OverdueArgs =
    | As_Of of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | As_Of _ -> "Reference date (yyyy-MM-dd, defaults to today)"
            | Json -> "Output in JSON format"

type UpcomingArgs =
    | Days of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Days _ -> "Number of days ahead to look (default: 30)"
            | Json -> "Output in JSON format"

type ObligationArgs =
    | [<CliPrefix(CliPrefix.None)>] Agreement of ParseResults<AgreementArgs>
    | [<CliPrefix(CliPrefix.None)>] Instance of ParseResults<InstanceArgs>
    | [<CliPrefix(CliPrefix.None)>] Overdue of ParseResults<OverdueArgs>
    | [<CliPrefix(CliPrefix.None)>] Upcoming of ParseResults<UpcomingArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Agreement _ -> "Obligation agreement commands (list, show, create, update, deactivate)"
            | Instance _ -> "Obligation instance commands (list, spawn, transition, post)"
            | Overdue _ -> "Run overdue detection"
            | Upcoming _ -> "List upcoming instances"

// --- Parse helpers ---

let private parseDate (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
    | true, d -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

let private parseObligationType (raw: string) : Result<ObligationDirection, string> =
    match ObligationDirection.fromString (raw.ToLowerInvariant()) with
    | Ok v -> Ok v
    | Error _ -> Error (sprintf "Invalid type '%s' -- expected receivable or payable" raw)

let private parseCadence (raw: string) : Result<RecurrenceCadence, string> =
    match RecurrenceCadence.fromString (raw.ToLowerInvariant()) with
    | Ok v -> Ok v
    | Error _ -> Error (sprintf "Invalid cadence '%s' -- expected monthly, quarterly, annual, or one_time" raw)

let private parseInstanceStatus (raw: string) : Result<InstanceStatus, string> =
    match InstanceStatus.fromString (raw.ToLowerInvariant()) with
    | Ok v -> Ok v
    | Error _ -> Error (sprintf "Invalid status '%s' -- expected expected, in_flight, confirmed, posted, overdue, or skipped" raw)

let private parsePaymentMethod (raw: string) : Result<PaymentMethodType, string> =
    match PaymentMethodType.fromString (raw.ToLowerInvariant()) with
    | Ok v -> Ok v
    | Error _ -> Error (sprintf "Invalid payment method '%s' -- expected autopay_pull, ach, zelle, cheque, bill_pay, or manual" raw)

let private parseOptDate (raw: string option) : Result<DateOnly option, string> =
    match raw with
    | None -> Ok None
    | Some s -> parseDate s |> Result.map Some

// --- Agreement handlers ---

let private handleAgreementList (isJson: bool) (args: ParseResults<AgreementListArgs>) : int =
    let isJson = isJson || args.Contains AgreementListArgs.Json
    let typeRaw = args.TryGetResult AgreementListArgs.Type
    let cadenceRaw = args.TryGetResult AgreementListArgs.Cadence
    let includeInactive = args.Contains AgreementListArgs.Inactive

    let typeResult =
        match typeRaw with
        | None -> Ok None
        | Some s -> parseObligationType s |> Result.map Some
    let cadenceResult =
        match cadenceRaw with
        | None -> Ok None
        | Some s -> parseCadence s |> Result.map Some

    match typeResult, cadenceResult with
    | Error e, _ -> write isJson (Error [e])
    | _, Error e -> write isJson (Error [e])
    | Ok obligationType, Ok cadence ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let isActive = if includeInactive then None else Some true
            let filter : ListAgreementsFilter =
                { isActive = isActive
                  obligationType = obligationType
                  cadence = cadence }
            let agreements = ObligationAgreementService.list txn filter
            txn.Commit()
            writeAgreementList isJson agreements
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleAgreementShow (isJson: bool) (args: ParseResults<AgreementShowArgs>) : int =
    let isJson = isJson || args.Contains AgreementShowArgs.Json
    let id = args.GetResult AgreementShowArgs.Id
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let found = ObligationAgreementService.getById txn id
        txn.Commit()
        match found with
        | None -> write isJson (Error [sprintf "Obligation agreement with id %d does not exist" id])
        | Some agreement -> write isJson (Ok (agreement :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleAgreementCreate (isJson: bool) (args: ParseResults<AgreementCreateArgs>) : int =
    let isJson = isJson || args.Contains AgreementCreateArgs.Json
    let nameRaw = args.GetResult AgreementCreateArgs.Name
    let typeRaw = args.GetResult AgreementCreateArgs.Type
    let cadenceRaw = args.GetResult AgreementCreateArgs.Cadence
    let counterparty = args.TryGetResult AgreementCreateArgs.Counterparty
    let amount = args.TryGetResult AgreementCreateArgs.Amount
    let expectedDay = args.TryGetResult AgreementCreateArgs.Expected_Day
    let paymentMethodRaw = args.TryGetResult AgreementCreateArgs.Payment_Method
    let sourceAccount = args.TryGetResult AgreementCreateArgs.Source_Account
    let destAccount = args.TryGetResult AgreementCreateArgs.Dest_Account
    let notes = args.TryGetResult AgreementCreateArgs.Notes

    let pmResult =
        match paymentMethodRaw with
        | None -> Ok None
        | Some s -> parsePaymentMethod s |> Result.map Some

    match parseObligationType typeRaw, parseCadence cadenceRaw, pmResult with
    | Error e, _, _ -> write isJson (Error [e])
    | _, Error e, _ -> write isJson (Error [e])
    | _, _, Error e -> write isJson (Error [e])
    | Ok obligationType, Ok cadence, Ok paymentMethod ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let cmd : CreateObligationAgreementCommand =
                { name = nameRaw
                  obligationType = obligationType
                  counterparty = counterparty
                  amount = amount
                  cadence = cadence
                  expectedDay = expectedDay
                  paymentMethod = paymentMethod
                  sourceAccountId = sourceAccount
                  destAccountId = destAccount
                  notes = notes }
            let result = ObligationAgreementService.create txn cmd
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleAgreementUpdate (isJson: bool) (args: ParseResults<AgreementUpdateArgs>) : int =
    let isJson = isJson || args.Contains AgreementUpdateArgs.Json
    let id = args.GetResult AgreementUpdateArgs.Id
    let nameRaw = args.GetResult AgreementUpdateArgs.Name
    let typeRaw = args.GetResult AgreementUpdateArgs.Type
    let cadenceRaw = args.GetResult AgreementUpdateArgs.Cadence
    let counterparty = args.TryGetResult AgreementUpdateArgs.Counterparty
    let amount = args.TryGetResult AgreementUpdateArgs.Amount
    let expectedDay = args.TryGetResult AgreementUpdateArgs.Expected_Day
    let paymentMethodRaw = args.TryGetResult AgreementUpdateArgs.Payment_Method
    let sourceAccount = args.TryGetResult AgreementUpdateArgs.Source_Account
    let destAccount = args.TryGetResult AgreementUpdateArgs.Dest_Account
    let isActive = not (args.Contains AgreementUpdateArgs.Inactive)
    let notes = args.TryGetResult AgreementUpdateArgs.Notes

    let pmResult =
        match paymentMethodRaw with
        | None -> Ok None
        | Some s -> parsePaymentMethod s |> Result.map Some

    match parseObligationType typeRaw, parseCadence cadenceRaw, pmResult with
    | Error e, _, _ -> write isJson (Error [e])
    | _, Error e, _ -> write isJson (Error [e])
    | _, _, Error e -> write isJson (Error [e])
    | Ok obligationType, Ok cadence, Ok paymentMethod ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let cmd : UpdateObligationAgreementCommand =
                { id = id
                  name = nameRaw
                  obligationType = obligationType
                  counterparty = counterparty
                  amount = amount
                  cadence = cadence
                  expectedDay = expectedDay
                  paymentMethod = paymentMethod
                  sourceAccountId = sourceAccount
                  destAccountId = destAccount
                  isActive = isActive
                  notes = notes }
            let result = ObligationAgreementService.update txn cmd
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleAgreementDeactivate (isJson: bool) (args: ParseResults<AgreementDeactivateArgs>) : int =
    let isJson = isJson || args.Contains AgreementDeactivateArgs.Json
    let id = args.GetResult AgreementDeactivateArgs.Id
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = ObligationAgreementService.deactivate txn id
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        write isJson (result |> Result.map (fun v -> v :> obj))
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

// --- Instance handlers ---

let private handleInstanceList (isJson: bool) (args: ParseResults<InstanceListArgs>) : int =
    let isJson = isJson || args.Contains InstanceListArgs.Json
    let statusRaw = args.TryGetResult InstanceListArgs.Status
    let dueBeforeRaw = args.TryGetResult InstanceListArgs.Due_Before
    let dueAfterRaw = args.TryGetResult InstanceListArgs.Due_After

    let statusResult =
        match statusRaw with
        | None -> Ok None
        | Some s -> parseInstanceStatus s |> Result.map Some

    match statusResult, parseOptDate dueBeforeRaw, parseOptDate dueAfterRaw with
    | Error e, _, _ -> write isJson (Error [e])
    | _, Error e, _ -> write isJson (Error [e])
    | _, _, Error e -> write isJson (Error [e])
    | Ok status, Ok dueBefore, Ok dueAfter ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let filter : ListInstancesFilter =
                { status = status
                  dueBefore = dueBefore
                  dueAfter = dueAfter }
            let instances = ObligationInstanceService.list txn filter
            txn.Commit()
            writeInstanceList isJson instances
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleInstanceSpawn (isJson: bool) (args: ParseResults<InstanceSpawnArgs>) : int =
    let isJson = isJson || args.Contains InstanceSpawnArgs.Json
    let agreementId = args.GetResult InstanceSpawnArgs.Agreement_Id
    let fromRaw = args.GetResult InstanceSpawnArgs.From
    let toRaw = args.GetResult InstanceSpawnArgs.To

    match parseDate fromRaw, parseDate toRaw with
    | Error e, _ -> write isJson (Error [e])
    | _, Error e -> write isJson (Error [e])
    | Ok startDate, Ok endDate ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let cmd : SpawnObligationInstancesCommand =
                { obligationAgreementId = agreementId
                  startDate = startDate
                  endDate = endDate }
            let result = ObligationInstanceService.spawn txn cmd
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            match result with
            | Error errs -> write isJson (Error errs)
            | Ok spawnResult -> writeSpawnResult isJson spawnResult
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleInstanceTransition (isJson: bool) (args: ParseResults<InstanceTransitionArgs>) : int =
    let isJson = isJson || args.Contains InstanceTransitionArgs.Json
    let instanceId = args.GetResult InstanceTransitionArgs.Instance_Id
    let toRaw = args.GetResult InstanceTransitionArgs.To
    let amount = args.TryGetResult InstanceTransitionArgs.Amount
    let dateRaw = args.TryGetResult InstanceTransitionArgs.Date
    let notes = args.TryGetResult InstanceTransitionArgs.Notes
    let journalEntryId = args.TryGetResult InstanceTransitionArgs.Journal_Entry_Id

    match parseInstanceStatus toRaw, parseOptDate dateRaw with
    | Error e, _ -> write isJson (Error [e])
    | _, Error e -> write isJson (Error [e])
    | Ok targetStatus, Ok confirmedDate ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let cmd : TransitionCommand =
                { instanceId = instanceId
                  targetStatus = targetStatus
                  amount = amount
                  confirmedDate = confirmedDate
                  journalEntryId = journalEntryId
                  notes = notes }
            let result = ObligationInstanceService.transition txn cmd
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            write isJson (result |> Result.map (fun v -> v :> obj))
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleInstancePost (isJson: bool) (args: ParseResults<InstancePostArgs>) : int =
    let isJson = isJson || args.Contains InstancePostArgs.Json
    let instanceId = args.GetResult InstancePostArgs.Instance_Id
    let cmd : PostToLedgerCommand = { instanceId = instanceId }
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = ObligationPostingService.postToLedger txn cmd
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        match result with
        | Error errs -> write isJson (Error errs)
        | Ok postResult -> writePostResult isJson postResult
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

// --- Overdue / Upcoming handlers ---

let private handleOverdue (isJson: bool) (args: ParseResults<OverdueArgs>) : int =
    let isJson = isJson || args.Contains OverdueArgs.Json
    let asOfRaw = args.TryGetResult OverdueArgs.As_Of

    match parseOptDate asOfRaw with
    | Error e -> write isJson (Error [e])
    | Ok dateOpt ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let referenceDate = dateOpt |> Option.defaultWith (fun () -> DateOnly.FromDateTime(DateTime.Today))
            let result = ObligationInstanceService.detectOverdue txn referenceDate
            txn.Commit()
            writeOverdueResult isJson result
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleUpcoming (isJson: bool) (args: ParseResults<UpcomingArgs>) : int =
    let isJson = isJson || args.Contains UpcomingArgs.Json
    let days = args.TryGetResult UpcomingArgs.Days |> Option.defaultValue 30
    let today = DateOnly.FromDateTime(DateTime.Today)
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let instances = ObligationInstanceService.findUpcoming txn today days
        txn.Commit()
        writeInstanceList isJson instances
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

// --- Dispatch ---

let dispatch (isJson: bool) (args: ParseResults<ObligationArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Agreement agrResults) ->
        match agrResults.TryGetSubCommand() with
        | Some (AgreementArgs.List listArgs) -> handleAgreementList isJson listArgs
        | Some (AgreementArgs.Show showArgs) -> handleAgreementShow isJson showArgs
        | Some (AgreementArgs.Create createArgs) -> handleAgreementCreate isJson createArgs
        | Some (AgreementArgs.Update updateArgs) -> handleAgreementUpdate isJson updateArgs
        | Some (AgreementArgs.Deactivate deactivateArgs) -> handleAgreementDeactivate isJson deactivateArgs
        | None ->
            Console.Error.WriteLine(agrResults.Parser.PrintUsage())
            ExitCodes.systemError
    | Some (Instance instResults) ->
        match instResults.TryGetSubCommand() with
        | Some (InstanceArgs.List listArgs) -> handleInstanceList isJson listArgs
        | Some (InstanceArgs.Spawn spawnArgs) -> handleInstanceSpawn isJson spawnArgs
        | Some (InstanceArgs.Transition transArgs) -> handleInstanceTransition isJson transArgs
        | Some (InstanceArgs.Post postArgs) -> handleInstancePost isJson postArgs
        | None ->
            Console.Error.WriteLine(instResults.Parser.PrintUsage())
            ExitCodes.systemError
    | Some (Overdue overdueArgs) -> handleOverdue isJson overdueArgs
    | Some (Upcoming upcomingArgs) -> handleUpcoming isJson upcomingArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
