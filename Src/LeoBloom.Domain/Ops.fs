namespace LeoBloom.Domain

/// Ops domain types — obligations, transfers, invoices.
/// Can reference Ledger types. Ledger cannot reference Ops.
module Ops =

    open System

    type ObligationDirection = Receivable | Payable

    type InstanceStatus =
        | Expected
        | InFlight
        | Confirmed
        | Posted
        | Overdue
        | Skipped

    type RecurrenceCadence = Monthly | Quarterly | Annual | OneTime

    type PaymentMethodType =
        | AutopayPull
        | Ach
        | Zelle
        | Cheque
        | BillPay
        | Manual

    type TransferStatus = Initiated | Confirmed

    module ObligationDirection =
        let toString = function
            | Receivable -> "receivable"
            | Payable -> "payable"

        let fromString (s: string) =
            match s with
            | "receivable" -> Ok Receivable
            | "payable" -> Ok Payable
            | _ -> Error (sprintf "Invalid ObligationDirection: '%s'" s)

    module InstanceStatus =
        let toString (s: InstanceStatus) =
            match s with
            | InstanceStatus.Expected -> "expected"
            | InstanceStatus.InFlight -> "in_flight"
            | InstanceStatus.Confirmed -> "confirmed"
            | InstanceStatus.Posted -> "posted"
            | InstanceStatus.Overdue -> "overdue"
            | InstanceStatus.Skipped -> "skipped"

        let fromString (s: string) =
            match s with
            | "expected" -> Ok InstanceStatus.Expected
            | "in_flight" -> Ok InstanceStatus.InFlight
            | "confirmed" -> Ok InstanceStatus.Confirmed
            | "posted" -> Ok InstanceStatus.Posted
            | "overdue" -> Ok InstanceStatus.Overdue
            | "skipped" -> Ok InstanceStatus.Skipped
            | _ -> Error (sprintf "Invalid InstanceStatus: '%s'" s)

    module RecurrenceCadence =
        let toString = function
            | Monthly -> "monthly"
            | Quarterly -> "quarterly"
            | Annual -> "annual"
            | OneTime -> "one_time"

        let fromString (s: string) =
            match s with
            | "monthly" -> Ok Monthly
            | "quarterly" -> Ok Quarterly
            | "annual" -> Ok Annual
            | "one_time" -> Ok OneTime
            | _ -> Error (sprintf "Invalid RecurrenceCadence: '%s'" s)

    module PaymentMethodType =
        let toString = function
            | AutopayPull -> "autopay_pull"
            | Ach -> "ach"
            | Zelle -> "zelle"
            | Cheque -> "cheque"
            | BillPay -> "bill_pay"
            | Manual -> "manual"

        let fromString (s: string) =
            match s with
            | "autopay_pull" -> Ok AutopayPull
            | "ach" -> Ok Ach
            | "zelle" -> Ok Zelle
            | "cheque" -> Ok Cheque
            | "bill_pay" -> Ok BillPay
            | "manual" -> Ok Manual
            | _ -> Error (sprintf "Invalid PaymentMethodType: '%s'" s)

    type ObligationAgreement =
        { id: int
          name: string
          obligationType: ObligationDirection
          counterparty: string option
          amount: decimal option
          cadence: RecurrenceCadence
          expectedDay: int option
          paymentMethod: PaymentMethodType option
          sourceAccountId: int option
          destAccountId: int option
          isActive: bool
          notes: string option
          createdAt: DateTimeOffset
          modifiedAt: DateTimeOffset }

    type ObligationInstance =
        { id: int
          obligationAgreementId: int
          name: string
          status: InstanceStatus
          amount: decimal option
          expectedDate: DateOnly
          confirmedDate: DateOnly option
          dueDate: DateOnly option
          documentPath: string option
          journalEntryId: int option
          notes: string option
          isActive: bool
          createdAt: DateTimeOffset
          modifiedAt: DateTimeOffset }

    type Transfer =
        { id: int
          fromAccountId: int
          toAccountId: int
          amount: decimal
          status: TransferStatus
          initiatedDate: DateOnly
          expectedSettlement: DateOnly option
          confirmedDate: DateOnly option
          journalEntryId: int option
          description: string option
          isActive: bool
          createdAt: DateTimeOffset
          modifiedAt: DateTimeOffset }

    type Invoice =
        { id: int
          tenant: string
          fiscalPeriodId: int
          rentAmount: decimal
          utilityShare: decimal
          totalAmount: decimal
          generatedAt: DateTimeOffset
          documentPath: string option
          notes: string option
          isActive: bool
          createdAt: DateTimeOffset
          modifiedAt: DateTimeOffset }

    type CreateObligationAgreementCommand =
        { name: string
          obligationType: ObligationDirection
          counterparty: string option
          amount: decimal option
          cadence: RecurrenceCadence
          expectedDay: int option
          paymentMethod: PaymentMethodType option
          sourceAccountId: int option
          destAccountId: int option
          notes: string option }

    type UpdateObligationAgreementCommand =
        { id: int
          name: string
          obligationType: ObligationDirection
          counterparty: string option
          amount: decimal option
          cadence: RecurrenceCadence
          expectedDay: int option
          paymentMethod: PaymentMethodType option
          sourceAccountId: int option
          destAccountId: int option
          isActive: bool
          notes: string option }

    module ObligationAgreementValidation =

        let validateName (name: string) : Result<unit, string list> =
            let errors =
                [ if System.String.IsNullOrWhiteSpace name then
                      "name is required and cannot be empty"
                  if name.Length > 100 then
                      "name must not exceed 100 characters" ]
            if errors.IsEmpty then Ok () else Error errors

        let validateCounterparty (counterparty: string option) : Result<unit, string list> =
            match counterparty with
            | None -> Ok ()
            | Some cp when cp.Length > 100 ->
                Error [ "counterparty must not exceed 100 characters" ]
            | _ -> Ok ()

        let validateAmount (amount: decimal option) : Result<unit, string list> =
            match amount with
            | None -> Ok ()
            | Some a when a <= 0m ->
                Error [ "amount must be greater than zero" ]
            | _ -> Ok ()

        let validateExpectedDay (day: int option) : Result<unit, string list> =
            match day with
            | None -> Ok ()
            | Some d when d < 1 || d > 31 ->
                Error [ "expected day must be between 1 and 31" ]
            | _ -> Ok ()

        let validateCreateCommand (cmd: CreateObligationAgreementCommand) : Result<unit, string list> =
            let allErrors =
                [ validateName cmd.name
                  validateCounterparty cmd.counterparty
                  validateAmount cmd.amount
                  validateExpectedDay cmd.expectedDay ]
                |> List.collect (function Error errs -> errs | Ok _ -> [])
            if allErrors.IsEmpty then Ok () else Error allErrors

        let validateUpdateCommand (cmd: UpdateObligationAgreementCommand) : Result<unit, string list> =
            let idErrors =
                if cmd.id <= 0 then [ "id must be greater than zero" ] else []
            let allErrors =
                idErrors @
                ([ validateName cmd.name
                   validateCounterparty cmd.counterparty
                   validateAmount cmd.amount
                   validateExpectedDay cmd.expectedDay ]
                 |> List.collect (function Error errs -> errs | Ok _ -> []))
            if allErrors.IsEmpty then Ok () else Error allErrors

    type SpawnObligationInstancesCommand =
        { obligationAgreementId: int
          startDate: DateOnly
          endDate: DateOnly }

    type SpawnResult =
        { created: ObligationInstance list
          skippedCount: int }

    module ObligationInstanceSpawning =

        let private clampDay (year: int) (month: int) (day: int) : int =
            min day (DateTime.DaysInMonth(year, month))

        let generateExpectedDates
            (cadence: RecurrenceCadence)
            (effectiveDay: int)
            (startDate: DateOnly)
            (endDate: DateOnly)
            : DateOnly list =
            match cadence with
            | OneTime ->
                [ startDate ]
            | Monthly ->
                let mutable dates = []
                let mutable current = DateOnly(startDate.Year, startDate.Month, 1)
                while current.Year < endDate.Year || (current.Year = endDate.Year && current.Month <= endDate.Month) do
                    let clamped = clampDay current.Year current.Month effectiveDay
                    let candidate = DateOnly(current.Year, current.Month, clamped)
                    if candidate >= startDate && candidate <= endDate then
                        dates <- candidate :: dates
                    current <- current.AddMonths(1)
                dates |> List.rev
            | Quarterly ->
                let quarterMonths = [ 1; 4; 7; 10 ]
                let mutable dates = []
                let mutable year = startDate.Year
                while year <= endDate.Year do
                    for month in quarterMonths do
                        let clamped = clampDay year month effectiveDay
                        let candidate = DateOnly(year, month, clamped)
                        if candidate >= startDate && candidate <= endDate then
                            dates <- candidate :: dates
                    year <- year + 1
                dates |> List.rev
            | Annual ->
                let anchorMonth = startDate.Month
                let mutable dates = []
                let mutable year = startDate.Year
                while year <= endDate.Year do
                    let clamped = clampDay year anchorMonth effectiveDay
                    let candidate = DateOnly(year, anchorMonth, clamped)
                    if candidate >= startDate && candidate <= endDate then
                        dates <- candidate :: dates
                    year <- year + 1
                dates |> List.rev

        let generateInstanceName (cadence: RecurrenceCadence) (date: DateOnly) : string =
            match cadence with
            | Monthly ->
                date.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)
            | Quarterly ->
                let quarter = (date.Month - 1) / 3 + 1
                sprintf "Q%d %d" quarter date.Year
            | Annual ->
                sprintf "%d" date.Year
            | OneTime ->
                "One-time"

        let validateSpawnCommand (cmd: SpawnObligationInstancesCommand) : Result<unit, string list> =
            let errors =
                [ if cmd.startDate > cmd.endDate then
                      "startDate must be on or before endDate"
                  if cmd.obligationAgreementId <= 0 then
                      "obligationAgreementId must be greater than zero" ]
            if errors.IsEmpty then Ok () else Error errors

    type TransitionCommand =
        { instanceId: int
          targetStatus: InstanceStatus
          amount: decimal option
          confirmedDate: DateOnly option
          journalEntryId: int option
          notes: string option }

    module StatusTransition =

        let private allowedTransitions =
            Map.ofList
                [ (InstanceStatus.Expected, set [ InstanceStatus.InFlight; InstanceStatus.Confirmed; InstanceStatus.Overdue; InstanceStatus.Skipped ])
                  (InstanceStatus.InFlight, set [ InstanceStatus.Confirmed; InstanceStatus.Overdue ])
                  (InstanceStatus.Overdue, set [ InstanceStatus.Confirmed ])
                  (InstanceStatus.Confirmed, set [ InstanceStatus.Posted ]) ]

        let isValidTransition (fromStatus: InstanceStatus) (toStatus: InstanceStatus) : bool =
            match allowedTransitions |> Map.tryFind fromStatus with
            | Some targets -> targets.Contains toStatus
            | None -> false

        let validateTransitionCommand (cmd: TransitionCommand) : Result<unit, string list> =
            let errors =
                [ if cmd.instanceId <= 0 then
                      "instanceId must be greater than zero"
                  if cmd.targetStatus = InstanceStatus.Posted && cmd.journalEntryId.IsNone then
                      "journal_entry_id is required for posted transition"
                  if cmd.targetStatus = InstanceStatus.Confirmed && cmd.confirmedDate.IsNone then
                      "confirmed_date is required for confirmed transition" ]
            if errors.IsEmpty then Ok () else Error errors

    type OverdueDetectionResult =
        { transitioned: int
          errors: (int * string) list
          queryFailed: bool }

    type PostToLedgerCommand =
        { instanceId: int }

    type PostToLedgerResult =
        { journalEntryId: int
          instanceId: int }

    module TransferStatus =
        let toString = function
            | Initiated -> "initiated"
            | Confirmed -> "confirmed"
        let fromString (s: string) =
            match s with
            | "initiated" -> Ok Initiated
            | "confirmed" -> Ok Confirmed
            | _ -> Error (sprintf "Invalid TransferStatus: '%s'" s)

    type InitiateTransferCommand =
        { fromAccountId: int
          toAccountId: int
          amount: decimal
          initiatedDate: DateOnly
          expectedSettlement: DateOnly option
          description: string option }

    type ConfirmTransferCommand =
        { transferId: int
          confirmedDate: DateOnly }

    type RecordInvoiceCommand =
        { tenant: string
          fiscalPeriodId: int
          rentAmount: decimal
          utilityShare: decimal
          totalAmount: decimal
          generatedAt: DateTimeOffset
          documentPath: string option
          notes: string option }

    module InvoiceValidation =

        let validateTenant (tenant: string) : Result<unit, string list> =
            let errors =
                [ if System.String.IsNullOrWhiteSpace tenant then
                      "tenant is required and cannot be empty"
                  if tenant.Length > 50 then
                      "tenant must not exceed 50 characters" ]
            if errors.IsEmpty then Ok () else Error errors

        let validateAmount (fieldName: string) (amount: decimal) : Result<unit, string list> =
            let errors =
                [ if amount < 0m then
                      sprintf "%s must not be negative" fieldName
                  if amount <> System.Math.Round(amount, 2, MidpointRounding.AwayFromZero) then
                      sprintf "%s must have at most 2 decimal places" fieldName ]
            if errors.IsEmpty then Ok () else Error errors

        let validateTotalEqualsComponents (rent: decimal) (utility: decimal) (total: decimal) : Result<unit, string list> =
            if total <> rent + utility then
                Error [ "totalAmount must equal rentAmount + utilityShare" ]
            else
                Ok ()

        let validateFiscalPeriodId (id: int) : Result<unit, string list> =
            if id <= 0 then
                Error [ "fiscalPeriodId must be greater than zero" ]
            else
                Ok ()

        let validateCommand (cmd: RecordInvoiceCommand) : Result<unit, string list> =
            let allErrors =
                [ validateTenant cmd.tenant
                  validateAmount "rentAmount" cmd.rentAmount
                  validateAmount "utilityShare" cmd.utilityShare
                  validateAmount "totalAmount" cmd.totalAmount
                  validateTotalEqualsComponents cmd.rentAmount cmd.utilityShare cmd.totalAmount
                  validateFiscalPeriodId cmd.fiscalPeriodId ]
                |> List.collect (function Error errs -> errs | Ok _ -> [])
            if allErrors.IsEmpty then Ok () else Error allErrors
