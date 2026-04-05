namespace LeoBloom.Ledger

open Npgsql
open LeoBloom.Domain.Ledger
open LeoBloom.Utilities

/// Convenience service for posting opening account balances.
/// Builds a balanced journal entry from account/balance pairs and delegates
/// to JournalEntryService.post.
module OpeningBalanceService =

    type private AccountInfo =
        { id: int
          normalBalance: string
          accountTypeName: string }

    let private lookupAccounts (conn: NpgsqlConnection) (accountIds: int list) : AccountInfo list =
        if accountIds.IsEmpty then []
        else
            let paramNames =
                accountIds
                |> List.mapi (fun i _ -> sprintf "@a%d" i)
                |> String.concat ", "
            let query = sprintf
                            "SELECT a.id, at.normal_balance, at.name
                             FROM ledger.account a
                             JOIN ledger.account_type at ON a.account_type_id = at.id
                             WHERE a.id IN (%s)" paramNames
            use cmd = new NpgsqlCommand(query, conn)
            accountIds
            |> List.iteri (fun i aid ->
                cmd.Parameters.AddWithValue(sprintf "@a%d" i, aid) |> ignore)
            use reader = cmd.ExecuteReader()
            let mutable results = []
            while reader.Read() do
                results <- { id = reader.GetInt32(0)
                             normalBalance = reader.GetString(1)
                             accountTypeName = reader.GetString(2) } :: results
            reader.Close()
            results

    let private validateCommand (cmd: PostOpeningBalancesCommand) : Result<unit, string list> =
        let mutable errors = []

        if cmd.entries.IsEmpty then
            errors <- "Entries list cannot be empty" :: errors

        let duplicates =
            cmd.entries
            |> List.groupBy (fun e -> e.accountId)
            |> List.filter (fun (_, group) -> group.Length > 1)
            |> List.map fst
        for dup in duplicates do
            errors <- (sprintf "Duplicate account id %d in entries" dup) :: errors

        for entry in cmd.entries do
            if entry.balance <= 0m then
                errors <- (sprintf "Account %d has non-positive balance: %M" entry.accountId entry.balance) :: errors

        if cmd.entries |> List.exists (fun e -> e.accountId = cmd.balancingAccountId) then
            errors <- "Balancing account cannot also appear in the entries list" :: errors

        if errors.IsEmpty then Ok () else Error (List.rev errors)

    let post (cmd: PostOpeningBalancesCommand) : Result<PostedJournalEntry, string list> =
        Log.info "Posting opening balances for {EntryCount} accounts, fiscal period {FiscalPeriodId}" [| cmd.entries.Length :> obj; cmd.fiscalPeriodId :> obj |]

        match validateCommand cmd with
        | Error errs ->
            Log.warn "Opening balance validation failed: {Errors}" [| errs :> obj |]
            Error errs
        | Ok () ->
            // Look up normal balance direction for all accounts + balancing account
            use conn = DataSource.openConnection()
            let allAccountIds = cmd.balancingAccountId :: (cmd.entries |> List.map (fun e -> e.accountId))
            let accountInfos = lookupAccounts conn allAccountIds
            let infoMap = accountInfos |> List.map (fun a -> a.id, a) |> Map.ofList

            let mutable errors = []

            // Validate all accounts exist
            for entry in cmd.entries do
                if not (Map.containsKey entry.accountId infoMap) then
                    errors <- (sprintf "Account with id %d does not exist" entry.accountId) :: errors

            // Validate balancing account exists and is equity type
            match Map.tryFind cmd.balancingAccountId infoMap with
            | None ->
                errors <- (sprintf "Balancing account with id %d does not exist" cmd.balancingAccountId) :: errors
            | Some info ->
                if info.accountTypeName <> "equity" then
                    errors <- (sprintf "Balancing account (id %d) must be an equity account, got '%s'" cmd.balancingAccountId info.accountTypeName) :: errors

            if not errors.IsEmpty then
                Error (List.rev errors)
            else
                // Build journal entry lines
                let mutable totalDebits = 0m
                let mutable totalCredits = 0m
                let entryLines =
                    cmd.entries
                    |> List.map (fun entry ->
                        let info = Map.find entry.accountId infoMap
                        let entryType =
                            match info.normalBalance with
                            | "debit" ->
                                totalDebits <- totalDebits + entry.balance
                                EntryType.Debit
                            | _ ->
                                totalCredits <- totalCredits + entry.balance
                                EntryType.Credit
                        { accountId = entry.accountId
                          amount = entry.balance
                          entryType = entryType
                          memo = None } : PostLineCommand)

                // Compute balancing line
                let diff = totalDebits - totalCredits
                let allLines =
                    if diff = 0m then
                        entryLines
                    elif diff > 0m then
                        // More debits — credit the balancing account
                        entryLines @ [ { accountId = cmd.balancingAccountId
                                         amount = diff
                                         entryType = EntryType.Credit
                                         memo = Some "Opening balance equity" } ]
                    else
                        // More credits — debit the balancing account
                        entryLines @ [ { accountId = cmd.balancingAccountId
                                         amount = -diff
                                         entryType = EntryType.Debit
                                         memo = Some "Opening balance equity" } ]

                let description = cmd.description |> Option.defaultValue "Opening balances"

                let jeCmd : PostJournalEntryCommand =
                    { entryDate = cmd.entryDate
                      description = description
                      source = Some "opening_balance"
                      fiscalPeriodId = cmd.fiscalPeriodId
                      lines = allLines
                      references = [] }

                JournalEntryService.post jeCmd
