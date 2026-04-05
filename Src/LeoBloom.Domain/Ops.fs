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
