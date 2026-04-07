Feature: Fund CRUD and Dimension Filtering
    Create, retrieve, list, and filter investment funds. A fund is identified
    by its ticker symbol (the primary key) and carries optional classification
    dimensions -- investment type, market cap, index type, sector, region, and
    objective. Dimension filtering allows callers to query all funds of a given
    classification without scanning the full fund table.

    # --- Create: Happy Path ---

    @FT-PF-010
    Scenario: Create fund with symbol and name only returns a complete record
        Given the portfolio schema exists for fund management
        When I create a fund with symbol "VTI" and name "Vanguard Total Stock Market ETF"
        Then the create succeeds and the returned fund has symbol "VTI"
        And all dimension fields on the returned fund are null
        And a subsequent findBySymbol "VTI" returns the same fund with matching fields

    @FT-PF-011
    Scenario: Create fund with optional dimension fields set returns those values
        Given the portfolio schema exists for fund management
        And a portfolio investment type dimension "Equity" exists with id 1
        And a portfolio sector dimension "Technology" exists with id 2
        When I create a fund with symbol "QQQ", name "Invesco QQQ Trust", investment type 1, and sector 2
        Then the create succeeds and the returned fund has symbol "QQQ"
        And the returned fund has investmentTypeId 1 and sectorId 2

    # --- Create: Pure Validation ---

    @FT-PF-012
    Scenario: Create fund with empty symbol is rejected
        Given the portfolio schema exists for fund management
        When I create a fund with symbol "" and name "Some Fund"
        Then the create fails with error containing "symbol"

    @FT-PF-013
    Scenario: Create fund with empty name is rejected
        Given the portfolio schema exists for fund management
        When I create a fund with symbol "VXUS" and name ""
        Then the create fails with error containing "name"

    # --- findBySymbol ---

    @FT-PF-014
    Scenario: findBySymbol for a nonexistent symbol returns none
        Given the portfolio schema exists for fund management
        When I look up fund by symbol "FAKE"
        Then the result is none

    # --- List All ---

    @FT-PF-015
    Scenario: List all funds returns every fund ordered by symbol
        Given the portfolio schema exists for fund management
        And a fund "BND" exists
        And a fund "VTI" exists
        And a fund "VXUS" exists
        When I list all funds
        Then the result contains "BND", "VTI", and "VXUS"

    # --- List by Dimension ---

    @FT-PF-016
    Scenario Outline: List funds by dimension filter returns only funds matching that dimension
        Given the portfolio schema exists for fund management
        And a portfolio <dimension> dimension with id <dimId> exists
        And a fund "AAA" assigned to <dimension> <dimId> exists
        And a fund "ZZZ" with no <dimension> assignment exists
        When I list funds by <filter> <dimId>
        Then the result contains "AAA"
        And the result does not contain "ZZZ"

        Examples:
            | dimension      | filter           | dimId |
            | investment type | ByInvestmentType | 1     |
            | market cap      | ByMarketCap      | 2     |
            | index type      | ByIndexType      | 3     |
            | sector          | BySector         | 4     |
            | region          | ByRegion         | 5     |
            | objective       | ByObjective      | 6     |

    @FT-PF-017
    Scenario: List funds by dimension returns empty when no funds match
        Given the portfolio schema exists for fund management
        And no funds exist with investment type id 99
        When I list funds by ByInvestmentType 99
        Then the result is an empty list
