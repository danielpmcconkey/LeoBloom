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

    type ObligationType =
        { id: int
          name: string }

    type ObligationStatus =
        { id: int
          name: string }

    type Cadence =
        { id: int
          name: string }

    type PaymentMethod =
        { id: int
          name: string }

    type ObligationAgreement =
        { id: int
          name: string
          obligationTypeId: int
          counterparty: string option
          amount: decimal option
          cadenceId: int
          expectedDay: int option
          paymentMethodId: int option
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
          statusId: int
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
