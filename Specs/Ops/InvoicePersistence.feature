Feature: Invoice Persistence
    Record, retrieve, and list invoices for tenant billing. An invoice is a
    source document capturing rent and utility charges for a tenant in a
    given fiscal period. It is not a ledger posting -- closed periods are
    allowed.

    # --- Record: Happy Path ---

    @FT-INV-001
    Scenario: Recording a valid invoice persists it and returns a complete record
        Given an active fiscal period
        When I record an invoice with:
            | field          | value              |
            | tenant         | Jeffrey            |
            | rentAmount     | 1200.00            |
            | utilityShare   | 85.50              |
            | totalAmount    | 1285.50            |
            | generatedAt    | 2026-04-01T12:00Z  |
            | documentPath   | /docs/inv-001.pdf  |
            | notes          | April charges      |
        Then the record succeeds with a generated id and timestamps
        And the returned invoice has tenant "Jeffrey" and totalAmount 1285.50
        And the returned invoice generatedAt matches the supplied value 2026-04-01T12:00Z

    @FT-INV-002
    Scenario: Recording an invoice with null optional fields succeeds
        Given an active fiscal period
        When I record an invoice with tenant "Jeffrey", rentAmount 1200.00, utilityShare 0.00, totalAmount 1200.00, and no documentPath or notes
        Then the record succeeds with a generated id and timestamps
        And the returned invoice has null documentPath and null notes

    @FT-INV-003
    Scenario: Recording an invoice for a closed fiscal period succeeds
        Given a closed fiscal period
        When I record an invoice with tenant "Jeffrey", rentAmount 1200.00, utilityShare 85.50, totalAmount 1285.50
        Then the record succeeds with a generated id and timestamps

    @FT-INV-004
    Scenario: Recording an invoice with zero rent and non-zero utility succeeds
        Given an active fiscal period
        When I record an invoice with tenant "Jeffrey", rentAmount 0.00, utilityShare 85.50, totalAmount 85.50
        Then the record succeeds with a generated id and timestamps

    @FT-INV-005
    Scenario: Recording an invoice with zero utility and non-zero rent succeeds
        Given an active fiscal period
        When I record an invoice with tenant "Jeffrey", rentAmount 1200.00, utilityShare 0.00, totalAmount 1200.00
        Then the record succeeds with a generated id and timestamps

    # --- Record: Pure Validation ---

    @FT-INV-006
    Scenario: Recording with empty tenant is rejected
        Given an active fiscal period
        When I record an invoice with tenant ""
        Then the record fails with error containing "tenant"

    @FT-INV-007
    Scenario: Recording with tenant exceeding 50 characters is rejected
        Given an active fiscal period
        When I record an invoice with a 51-character tenant
        Then the record fails with error containing "tenant"

    @FT-INV-008
    Scenario Outline: Recording with negative amount is rejected
        Given an active fiscal period
        When I record an invoice with <field> set to <value>
        Then the record fails with error containing "<field>"

        Examples:
            | field        | value  |
            | rentAmount   | -1.00  |
            | utilityShare | -1.00  |
            | totalAmount  | -1.00  |

    @FT-INV-009
    Scenario Outline: Recording with more than 2 decimal places is rejected
        Given an active fiscal period
        When I record an invoice with <field> set to <value>
        Then the record fails with error containing "decimal places"

        Examples:
            | field        | value   |
            | rentAmount   | 100.005 |
            | utilityShare | 50.999  |
            | totalAmount  | 150.001 |

    @FT-INV-010
    Scenario: Recording with total not equal to rent plus utility is rejected
        Given an active fiscal period
        When I record an invoice with rentAmount 1200.00, utilityShare 85.50, totalAmount 1300.00
        Then the record fails with error containing "total"

    @FT-INV-011
    Scenario: Recording with multiple validation errors collects all errors
        Given an active fiscal period
        When I record an invoice with tenant "", rentAmount -1.00, utilityShare -1.00, totalAmount -1.00
        Then the record fails with at least 3 errors

    # --- Record: DB Validation ---

    @FT-INV-012
    Scenario: Recording with nonexistent fiscal period is rejected
        When I record an invoice referencing fiscal period ID 999999
        Then the record fails with error containing "fiscal period"

    @FT-INV-013
    Scenario: Recording a duplicate tenant + fiscal period is rejected
        Given an active fiscal period
        And an existing invoice for tenant "Jeffrey" in that fiscal period
        When I record an invoice with tenant "Jeffrey" in the same fiscal period
        Then the record fails with error containing "already exists"

    # --- Show: Retrieve by ID ---

    @FT-INV-014
    Scenario: Showing an invoice by ID returns the full record
        Given an active fiscal period
        And an existing invoice for tenant "Jeffrey" in that fiscal period
        When I show the invoice by its ID
        Then the result contains the invoice with tenant "Jeffrey"

    @FT-INV-015
    Scenario: Showing a nonexistent invoice returns an error
        When I show invoice by ID 999999
        Then the show fails with error containing "does not exist"

    # --- List: Filtered Queries ---

    @FT-INV-016
    Scenario: Listing invoices with no filter returns all active invoices
        Given an active fiscal period
        And existing invoices for tenants "Jeffrey" and "Adam" in that fiscal period
        When I list invoices with no filter
        Then the result contains invoices for both "Jeffrey" and "Adam"

    @FT-INV-017
    Scenario: Listing invoices filtered by tenant returns only that tenant
        Given an active fiscal period
        And existing invoices for tenants "Jeffrey" and "Adam" in that fiscal period
        When I list invoices filtered by tenant "Jeffrey"
        Then the result contains an invoice for "Jeffrey"
        And the result does not contain an invoice for "Adam"

    @FT-INV-018
    Scenario: Listing invoices filtered by fiscal period returns only that period
        Given two active fiscal periods
        And an existing invoice for tenant "Jeffrey" in the first fiscal period
        And an existing invoice for tenant "Jeffrey" in the second fiscal period
        When I list invoices filtered by the first fiscal period
        Then the result contains exactly 1 invoice

    @FT-INV-019
    Scenario: Listing invoices filtered by both tenant and fiscal period
        Given two active fiscal periods
        And an existing invoice for tenant "Jeffrey" in the first fiscal period
        And an existing invoice for tenant "Adam" in the first fiscal period
        And an existing invoice for tenant "Jeffrey" in the second fiscal period
        When I list invoices filtered by tenant "Jeffrey" and the first fiscal period
        Then the result contains exactly 1 invoice
        And the result invoice has tenant "Jeffrey"

    @FT-INV-020
    Scenario: Listing invoices returns empty when none match
        When I list invoices filtered by tenant "Nobody"
        Then the result is an empty list
