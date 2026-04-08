module LeoBloom.CLI.PortfolioCommands

open System
open Argu
open LeoBloom.Domain.Portfolio
open LeoBloom.Portfolio
open LeoBloom.Utilities
open LeoBloom.CLI.OutputFormatter

// --- PortfolioAccountArgs ---

type PortfolioAccountCreateArgs =
    | [<Mandatory>] Name of string
    | [<Mandatory; CustomAppSettings "tax-bucket-id">] Tax_Bucket_Id of int
    | [<Mandatory; CustomAppSettings "account-group-id">] Account_Group_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Name _           -> "Investment account name"
            | Tax_Bucket_Id _  -> "Tax bucket ID"
            | Account_Group_Id _ -> "Account group ID"
            | Json             -> "Output in JSON format"

type PortfolioAccountListArgs =
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Json -> "Output in JSON format"

type PortfolioAccountArgs =
    | [<CliPrefix(CliPrefix.None)>] Create of ParseResults<PortfolioAccountCreateArgs>
    | [<CliPrefix(CliPrefix.None)>] List   of ParseResults<PortfolioAccountListArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Create _ -> "Create a new investment account"
            | List _   -> "List all investment accounts"

// --- PortfolioFundArgs ---

type PortfolioFundCreateArgs =
    | [<Mandatory>] Symbol of string
    | [<Mandatory>] Name   of string
    | [<CustomAppSettings "investment-type-id">] Investment_Type_Id of int
    | [<CustomAppSettings "market-cap-id">] Market_Cap_Id of int
    | [<CustomAppSettings "index-type-id">] Index_Type_Id of int
    | [<CustomAppSettings "sector-id">] Sector_Id of int
    | [<CustomAppSettings "region-id">] Region_Id of int
    | [<CustomAppSettings "objective-id">] Objective_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Symbol _             -> "Fund ticker symbol (e.g. VTI)"
            | Name _               -> "Fund name"
            | Investment_Type_Id _ -> "Investment type dimension ID (optional)"
            | Market_Cap_Id _      -> "Market cap dimension ID (optional)"
            | Index_Type_Id _      -> "Index type dimension ID (optional)"
            | Sector_Id _          -> "Sector dimension ID (optional)"
            | Region_Id _          -> "Region dimension ID (optional)"
            | Objective_Id _       -> "Objective dimension ID (optional)"
            | Json                 -> "Output in JSON format"

type PortfolioFundListArgs =
    | [<CustomAppSettings "investment-type-id">] Investment_Type_Id of int
    | [<CustomAppSettings "market-cap-id">] Market_Cap_Id of int
    | [<CustomAppSettings "index-type-id">] Index_Type_Id of int
    | [<CustomAppSettings "sector-id">] Sector_Id of int
    | [<CustomAppSettings "region-id">] Region_Id of int
    | [<CustomAppSettings "objective-id">] Objective_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Investment_Type_Id _ -> "Filter by investment type ID"
            | Market_Cap_Id _      -> "Filter by market cap ID"
            | Index_Type_Id _      -> "Filter by index type ID"
            | Sector_Id _          -> "Filter by sector ID"
            | Region_Id _          -> "Filter by region ID"
            | Objective_Id _       -> "Filter by objective ID"
            | Json                 -> "Output in JSON format"

type PortfolioFundShowArgs =
    | [<MainCommand; Mandatory>] Symbol of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Symbol _ -> "Fund ticker symbol"
            | Json     -> "Output in JSON format"

type PortfolioFundArgs =
    | [<CliPrefix(CliPrefix.None)>] Create of ParseResults<PortfolioFundCreateArgs>
    | [<CliPrefix(CliPrefix.None)>] List   of ParseResults<PortfolioFundListArgs>
    | [<CliPrefix(CliPrefix.None)>] Show   of ParseResults<PortfolioFundShowArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Create _ -> "Create a new fund"
            | List _   -> "List funds with optional dimension filter"
            | Show _   -> "Show fund details by symbol"

// --- PortfolioPositionArgs ---

type PortfolioPositionRecordArgs =
    | [<Mandatory; CustomAppSettings "account-id">] Account_Id of int
    | [<Mandatory>] Symbol of string
    | [<Mandatory>] Date   of string
    | [<Mandatory>] Price  of decimal
    | [<Mandatory>] Quantity of decimal
    | [<Mandatory>] Value  of decimal
    | [<Mandatory; CustomAppSettings "cost-basis">] Cost_Basis of decimal
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account_Id _ -> "Investment account ID"
            | Symbol _     -> "Fund ticker symbol"
            | Date _       -> "Position date (yyyy-MM-dd)"
            | Price _      -> "Price per unit"
            | Quantity _   -> "Number of units"
            | Value _      -> "Current market value"
            | Cost_Basis _ -> "Cost basis"
            | Json         -> "Output in JSON format"

type PortfolioPositionListArgs =
    | [<CustomAppSettings "account-id">] Account_Id  of int
    | [<CustomAppSettings "start-date">] Start_Date  of string
    | [<CustomAppSettings "end-date">]   End_Date    of string
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account_Id _  -> "Filter by investment account ID"
            | Start_Date _  -> "Filter by start date (yyyy-MM-dd)"
            | End_Date _    -> "Filter by end date (yyyy-MM-dd)"
            | Json          -> "Output in JSON format"

type PortfolioPositionLatestArgs =
    | [<CustomAppSettings "account-id">] Account_Id of int
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account_Id _ -> "Filter to a specific account ID"
            | Json         -> "Output in JSON format"

type PortfolioPositionArgs =
    | [<CliPrefix(CliPrefix.None)>] Record of ParseResults<PortfolioPositionRecordArgs>
    | [<CliPrefix(CliPrefix.None)>] List   of ParseResults<PortfolioPositionListArgs>
    | [<CliPrefix(CliPrefix.None)>] Latest of ParseResults<PortfolioPositionLatestArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Record _ -> "Record a new position snapshot"
            | List _   -> "List positions with optional filters"
            | Latest _ -> "Show latest position per symbol (optionally filtered by account)"

// --- Top-level PortfolioArgs ---

type PortfolioArgs =
    | [<CliPrefix(CliPrefix.None)>] Account  of ParseResults<PortfolioAccountArgs>
    | [<CliPrefix(CliPrefix.None)>] Fund     of ParseResults<PortfolioFundArgs>
    | [<CliPrefix(CliPrefix.None)>] Position of ParseResults<PortfolioPositionArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Account _  -> "Investment account commands (create, list)"
            | Fund _     -> "Fund commands (create, list, show)"
            | Position _ -> "Position commands (record, list, latest)"

// --- Helpers ---

let private parseDate (raw: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(raw, "yyyy-MM-dd") with
    | true, d  -> Ok d
    | false, _ -> Error (sprintf "Invalid date format '%s' -- expected yyyy-MM-dd" raw)

// --- Account handlers ---

let private handleAccountCreate (isJson: bool) (args: ParseResults<PortfolioAccountCreateArgs>) : int =
    let isJson    = isJson || args.Contains PortfolioAccountCreateArgs.Json
    let name      = args.GetResult PortfolioAccountCreateArgs.Name
    let tbId      = args.GetResult PortfolioAccountCreateArgs.Tax_Bucket_Id
    let agId      = args.GetResult PortfolioAccountCreateArgs.Account_Group_Id
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = InvestmentAccountService.createAccount txn name tbId agId
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        match result with
        | Ok acct  -> write isJson (Ok (acct :> obj))
        | Error es -> write isJson (Error es)
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleAccountList (isJson: bool) (args: ParseResults<PortfolioAccountListArgs>) : int =
    let isJson = isJson || args.Contains PortfolioAccountListArgs.Json
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = InvestmentAccountService.listAccounts txn
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        match result with
        | Ok accounts -> writeInvestmentAccountList isJson accounts
        | Error es    -> write isJson (Error es)
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private dispatchAccount (isJson: bool) (args: ParseResults<PortfolioAccountArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (PortfolioAccountArgs.Create createArgs) -> handleAccountCreate isJson createArgs
    | Some (PortfolioAccountArgs.List   listArgs)   -> handleAccountList   isJson listArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError

// --- Fund handlers ---

let private handleFundCreate (isJson: bool) (args: ParseResults<PortfolioFundCreateArgs>) : int =
    let isJson = isJson || args.Contains PortfolioFundCreateArgs.Json
    let fund =
        { symbol          = args.GetResult PortfolioFundCreateArgs.Symbol
          name            = args.GetResult PortfolioFundCreateArgs.Name
          investmentTypeId = args.TryGetResult PortfolioFundCreateArgs.Investment_Type_Id
          marketCapId      = args.TryGetResult PortfolioFundCreateArgs.Market_Cap_Id
          indexTypeId      = args.TryGetResult PortfolioFundCreateArgs.Index_Type_Id
          sectorId         = args.TryGetResult PortfolioFundCreateArgs.Sector_Id
          regionId         = args.TryGetResult PortfolioFundCreateArgs.Region_Id
          objectiveId      = args.TryGetResult PortfolioFundCreateArgs.Objective_Id }
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = FundService.createFund txn fund
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        match result with
        | Ok f     -> write isJson (Ok (f :> obj))
        | Error es -> write isJson (Error es)
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private handleFundList (isJson: bool) (args: ParseResults<PortfolioFundListArgs>) : int =
    let isJson = isJson || args.Contains PortfolioFundListArgs.Json
    // Collect which dimension filters were provided
    let filters = ResizeArray<FundDimensionFilter>()
    args.TryGetResult PortfolioFundListArgs.Investment_Type_Id |> Option.iter (fun id -> filters.Add(ByInvestmentType id))
    args.TryGetResult PortfolioFundListArgs.Market_Cap_Id      |> Option.iter (fun id -> filters.Add(ByMarketCap id))
    args.TryGetResult PortfolioFundListArgs.Index_Type_Id      |> Option.iter (fun id -> filters.Add(ByIndexType id))
    args.TryGetResult PortfolioFundListArgs.Sector_Id          |> Option.iter (fun id -> filters.Add(BySector id))
    args.TryGetResult PortfolioFundListArgs.Region_Id          |> Option.iter (fun id -> filters.Add(ByRegion id))
    args.TryGetResult PortfolioFundListArgs.Objective_Id       |> Option.iter (fun id -> filters.Add(ByObjective id))
    if filters.Count > 1 then
        write isJson (Error ["Only one dimension filter may be specified at a time"])
    else
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result =
                if filters.Count = 1 then
                    FundService.listFundsByDimension txn filters.[0]
                else
                    FundService.listFunds txn
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            match result with
            | Ok funds -> writeFundList isJson funds
            | Error es -> write isJson (Error es)
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handleFundShow (isJson: bool) (args: ParseResults<PortfolioFundShowArgs>) : int =
    let isJson = isJson || args.Contains PortfolioFundShowArgs.Json
    let symbol = args.GetResult PortfolioFundShowArgs.Symbol
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result = FundService.findFundBySymbol txn symbol
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        match result with
        | Ok (Some f) -> write isJson (Ok (f :> obj))
        | Ok None     -> write isJson (Error [sprintf "Fund '%s' not found" symbol])
        | Error es    -> write isJson (Error es)
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private dispatchFund (isJson: bool) (args: ParseResults<PortfolioFundArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (PortfolioFundArgs.Create createArgs) -> handleFundCreate isJson createArgs
    | Some (PortfolioFundArgs.List   listArgs)   -> handleFundList   isJson listArgs
    | Some (PortfolioFundArgs.Show   showArgs)   -> handleFundShow   isJson showArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError

// --- Position handlers ---

let private handlePositionRecord (isJson: bool) (args: ParseResults<PortfolioPositionRecordArgs>) : int =
    let isJson    = isJson || args.Contains PortfolioPositionRecordArgs.Json
    let accountId = args.GetResult PortfolioPositionRecordArgs.Account_Id
    let symbol    = args.GetResult PortfolioPositionRecordArgs.Symbol
    let dateRaw   = args.GetResult PortfolioPositionRecordArgs.Date
    let price     = args.GetResult PortfolioPositionRecordArgs.Price
    let quantity  = args.GetResult PortfolioPositionRecordArgs.Quantity
    let value     = args.GetResult PortfolioPositionRecordArgs.Value
    let costBasis = args.GetResult PortfolioPositionRecordArgs.Cost_Basis
    match parseDate dateRaw with
    | Error e -> write isJson (Error [e])
    | Ok date ->
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let result = PositionService.recordPosition txn accountId symbol date price quantity value costBasis
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            match result with
            | Ok pos   -> write isJson (Ok (pos :> obj))
            | Error es -> write isJson (Error es)
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handlePositionList (isJson: bool) (args: ParseResults<PortfolioPositionListArgs>) : int =
    let isJson = isJson || args.Contains PortfolioPositionListArgs.Json
    let accountId  = args.TryGetResult PortfolioPositionListArgs.Account_Id
    let startRaw   = args.TryGetResult PortfolioPositionListArgs.Start_Date
    let endRaw     = args.TryGetResult PortfolioPositionListArgs.End_Date
    let startDate  = startRaw |> Option.bind (fun r -> match parseDate r with Ok d -> Some d | Error _ -> None)
    let endDate    = endRaw   |> Option.bind (fun r -> match parseDate r with Ok d -> Some d | Error _ -> None)
    // Validate dates if raw strings were provided
    let dateErrors =
        [ match startRaw with
          | Some r -> match parseDate r with Error e -> yield e | _ -> ()
          | None   -> ()
          match endRaw with
          | Some r -> match parseDate r with Error e -> yield e | _ -> ()
          | None   -> () ]
    if not dateErrors.IsEmpty then
        write isJson (Error dateErrors)
    else
        use conn = DataSource.openConnection()
        use txn = conn.BeginTransaction()
        try
            let filter = { investmentAccountId = accountId; startDate = startDate; endDate = endDate }
            let result = PositionService.listPositions txn filter
            match result with
            | Ok _ -> txn.Commit()
            | Error _ -> txn.Rollback()
            match result with
            | Ok positions -> writePositionList isJson positions
            | Error es     -> write isJson (Error es)
        with ex ->
            try txn.Rollback() with _ -> ()
            reraise()

let private handlePositionLatest (isJson: bool) (args: ParseResults<PortfolioPositionLatestArgs>) : int =
    let isJson    = isJson || args.Contains PortfolioPositionLatestArgs.Json
    let accountId = args.TryGetResult PortfolioPositionLatestArgs.Account_Id
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    try
        let result =
            match accountId with
            | Some id -> PositionService.latestPositionsByAccount txn id
            | None    -> PositionService.latestPositionsAll txn
        match result with
        | Ok _ -> txn.Commit()
        | Error _ -> txn.Rollback()
        match result with
        | Ok positions -> writePositionList isJson positions
        | Error es     -> write isJson (Error es)
    with ex ->
        try txn.Rollback() with _ -> ()
        reraise()

let private dispatchPosition (isJson: bool) (args: ParseResults<PortfolioPositionArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (PortfolioPositionArgs.Record recordArgs) -> handlePositionRecord isJson recordArgs
    | Some (PortfolioPositionArgs.List   listArgs)   -> handlePositionList   isJson listArgs
    | Some (PortfolioPositionArgs.Latest latestArgs) -> handlePositionLatest isJson latestArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError

// --- Top-level dispatch ---

let dispatch (isJson: bool) (args: ParseResults<PortfolioArgs>) : int =
    match args.TryGetSubCommand() with
    | Some (Account  accountArgs)  -> dispatchAccount  isJson accountArgs
    | Some (Fund     fundArgs)     -> dispatchFund     isJson fundArgs
    | Some (Position positionArgs) -> dispatchPosition isJson positionArgs
    | None ->
        Console.Error.WriteLine(args.Parser.PrintUsage())
        ExitCodes.systemError
