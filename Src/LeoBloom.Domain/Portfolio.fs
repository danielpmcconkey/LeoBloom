namespace LeoBloom.Domain

/// Portfolio domain types — investment accounts, funds, and positions.
module Portfolio =

    open System

    // Dimension tables (lookup / reference data)
    type TaxBucket         = { id: int; name: string }
    type AccountGroup      = { id: int; name: string }
    type DimInvestmentType = { id: int; name: string }
    type DimMarketCap      = { id: int; name: string }
    type DimIndexType      = { id: int; name: string }
    type DimSector         = { id: int; name: string }
    type DimRegion         = { id: int; name: string }
    type DimObjective      = { id: int; name: string }

    // Core entities
    type InvestmentAccount =
        { id: int
          name: string
          taxBucketId: int
          accountGroupId: int }

    type Fund =
        { symbol: string
          name: string
          investmentTypeId: int option
          marketCapId: int option
          indexTypeId: int option
          sectorId: int option
          regionId: int option
          objectiveId: int option }

    type Position =
        { id: int
          investmentAccountId: int
          symbol: string
          positionDate: DateOnly
          price: decimal
          quantity: decimal
          currentValue: decimal
          costBasis: decimal }

    // Filter types
    type PositionFilter =
        { investmentAccountId: int option
          startDate: DateOnly option
          endDate: DateOnly option }

    type FundDimensionFilter =
        | ByInvestmentType of int
        | ByMarketCap of int
        | ByIndexType of int
        | BySector of int
        | ByRegion of int
        | ByObjective of int
