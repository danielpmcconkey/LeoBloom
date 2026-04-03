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
