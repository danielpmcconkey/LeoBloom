# P058 — Investment Domain Types and Repository Layer: Plan

## Objective

Build the F# domain types, Npgsql repository layer, and thin service layer for
the 10 portfolio tables created in P057. This gives downstream projects (CLI,
data migration, reporting) a typed, tested interface to portfolio data. The new
`LeoBloom.Portfolio` project mirrors the Ledger/Ops pattern exactly.

## Phases

### Phase 1: Domain Types

**What:** Add `Portfolio.fs` to `LeoBloom.Domain` with F# records for every
portfolio entity.

**Decision note:** The PO kickoff says "domain types go in the Portfolio
project." However, the actual codebase pattern (post-P045) is that domain types
live in `LeoBloom.Domain` — `Ledger.fs` and `Ops.fs` are there, not in their
owning projects. Following the established pattern for consistency. If PO wants
to break this pattern for Portfolio specifically, that's a scope discussion.

**Files:**
- Create `Src/LeoBloom.Domain/Portfolio.fs`
- Edit `Src/LeoBloom.Domain/LeoBloom.Domain.fsproj` — add `Portfolio.fs` to
  `<Compile>` list (after `Ops.fs`)

**Types to define:**

```fsharp
module Portfolio =
    // Dimension tables (all identical shape)
    type TaxBucket       = { id: int; name: string }
    type AccountGroup    = { id: int; name: string }
    type DimInvestmentType = { id: int; name: string }
    type DimMarketCap    = { id: int; name: string }
    type DimIndexType    = { id: int; name: string }
    type DimSector       = { id: int; name: string }
    type DimRegion       = { id: int; name: string }
    type DimObjective    = { id: int; name: string }

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
```

**Verification:** `dotnet build Src/LeoBloom.Domain` succeeds. All existing
tests still pass.

---

### Phase 2: Portfolio Project Skeleton

**What:** Create `LeoBloom.Portfolio` F# class library, wire into solution and
test project.

**Files:**
- Create `Src/LeoBloom.Portfolio/LeoBloom.Portfolio.fsproj`
- Edit `LeoBloom.sln` — add project reference
- Edit `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add `<ProjectReference>`

**fsproj template** (matches Ledger.fsproj):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="InvestmentAccountRepository.fs" />
    <Compile Include="FundRepository.fs" />
    <Compile Include="PositionRepository.fs" />
    <Compile Include="InvestmentAccountService.fs" />
    <Compile Include="FundService.fs" />
    <Compile Include="PositionService.fs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\LeoBloom.Domain\LeoBloom.Domain.fsproj" />
    <ProjectReference Include="..\LeoBloom.Utilities\LeoBloom.Utilities.fsproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="9.0.3" />
  </ItemGroup>
</Project>
```

**Verification:** `dotnet build` succeeds for the full solution.

---

### Phase 3: Repositories

**What:** Npgsql repositories for each entity, following the FiscalPeriodRepository
pattern (private reader helper, functions taking `NpgsqlTransaction`).

**Files:**
- Create `Src/LeoBloom.Portfolio/InvestmentAccountRepository.fs`
- Create `Src/LeoBloom.Portfolio/FundRepository.fs`
- Create `Src/LeoBloom.Portfolio/PositionRepository.fs`

#### InvestmentAccountRepository

| Function | Signature | SQL |
|----------|-----------|-----|
| `findById` | `txn → int → InvestmentAccount option` | `SELECT ... FROM portfolio.investment_account WHERE id = @id` |
| `create` | `txn → string → int → int → InvestmentAccount` | `INSERT ... RETURNING ...` |
| `listAll` | `txn → InvestmentAccount list` | `SELECT ... ORDER BY id` |

#### FundRepository

| Function | Signature | SQL |
|----------|-----------|-----|
| `findBySymbol` | `txn → string → Fund option` | `SELECT ... WHERE symbol = @symbol` |
| `create` | `txn → Fund → Fund` | `INSERT ... RETURNING ...` (fund uses symbol as PK, no serial) |
| `listAll` | `txn → Fund list` | `SELECT ... ORDER BY symbol` |
| `listByDimension` | `txn → FundDimensionFilter → Fund list` | Dynamic WHERE clause on the matching dim column |

#### PositionRepository

| Function | Signature | SQL |
|----------|-----------|-----|
| `findById` | `txn → int → Position option` | standard |
| `create` | `txn → Position-fields → Position` | `INSERT ... RETURNING ...` |
| `listByFilter` | `txn → PositionFilter → Position list` | Dynamic WHERE with optional account ID + date range |
| `latestByAccount` | `txn → int → Position list` | Window/DISTINCT ON query: `SELECT DISTINCT ON (symbol) ... FROM portfolio.position WHERE investment_account_id = @id ORDER BY symbol, position_date DESC` |
| `latestAll` | `txn → Position list` | Same pattern but no account filter — latest per (account, symbol) pair |

**Key query — latest positions:**
```sql
SELECT DISTINCT ON (investment_account_id, symbol)
       id, investment_account_id, symbol, position_date,
       price, quantity, current_value, cost_basis
FROM portfolio.position
ORDER BY investment_account_id, symbol, position_date DESC
```

**Verification:** Solution builds. Repositories are pure data access, no
validation logic.

---

### Phase 4: Service Layer

**What:** Thin services matching the Ledger pattern — open connection, validate,
call repository, commit/rollback, log.

**Files:**
- Create `Src/LeoBloom.Portfolio/InvestmentAccountService.fs`
- Create `Src/LeoBloom.Portfolio/FundService.fs`
- Create `Src/LeoBloom.Portfolio/PositionService.fs`

#### InvestmentAccountService

| Function | Validates | Delegates to |
|----------|-----------|-------------|
| `createAccount` | name non-blank | `InvestmentAccountRepository.create` |
| `listAccounts` | — | `InvestmentAccountRepository.listAll` |

#### FundService

| Function | Validates | Delegates to |
|----------|-----------|-------------|
| `createFund` | symbol + name non-blank | `FundRepository.create` |
| `listFunds` | — | `FundRepository.listAll` |
| `listFundsByDimension` | — | `FundRepository.listByDimension` |

#### PositionService

| Function | Validates | Delegates to |
|----------|-----------|-------------|
| `recordPosition` | **AC-B9:** price, quantity, current_value ≥ 0. **AC-B10:** fund must exist (lookup via `FundRepository.findBySymbol`). | `PositionRepository.create` (catches 23505 for **AC-B4** duplicate) |
| `listPositions` | — | `PositionRepository.listByFilter` |
| `latestPositions` | — | `PositionRepository.latestByAccount` or `latestAll` |

**Error handling pattern** (matches Ledger):
- Pure validation first → `Error [messages]`
- DB-dependent validation (fund exists) inside the transaction
- Catch `PostgresException` with `SqlState = "23505"` for duplicate position → friendly error
- Generic `with ex →` for persistence errors

**Verification:** Solution builds. Service functions return
`Result<'T, string list>`.

---

### Phase 5: Tests

**What:** Behavioral tests covering AC-B1 through AC-B10, using existing
TestHelpers patterns.

**Files:**
- Create `Src/LeoBloom.Tests/InvestmentAccountTests.fs`
- Create `Src/LeoBloom.Tests/FundTests.fs`
- Create `Src/LeoBloom.Tests/PositionTests.fs`
- Edit `Src/LeoBloom.Tests/LeoBloom.Tests.fsproj` — add `<Compile>` entries

**Test → AC mapping:**

| Test File | Covers |
|-----------|--------|
| InvestmentAccountTests | AC-B1 |
| FundTests | AC-B2, AC-B8 |
| PositionTests | AC-B3, AC-B4, AC-B5, AC-B6, AC-B7, AC-B9, AC-B10 |

**Test pattern** (matches FiscalPeriodTests):
```fsharp
[<Fact>]
[<Trait("Category", "Portfolio")>]
[<Trait("GherkinId", "AC-B3")>]
let ``Record position — valid account and fund`` () =
    // Setup: create tax bucket, account group, investment account, fund
    // Act: PositionService.recordPosition ...
    // Assert: Ok result, fields match
    // Cleanup: delete in FK-safe order
```

**Setup data helpers:** Add Portfolio-specific insert helpers to TestHelpers or
a new `PortfolioTestHelpers.fs` module (following the `InsertHelpers` pattern).
These create throwaway tax buckets, account groups, accounts, and funds for test
isolation.

**Verification:** `dotnet test` — all new tests pass, all existing tests pass
(AC-S3).

## Acceptance Criteria

- [ ] AC-S1: `LeoBloom.Portfolio.fsproj` exists, is in the solution, and builds
- [ ] AC-S2: Repository/service patterns match Ledger (NpgsqlTransaction, DataSource, Log)
- [ ] AC-S3: All pre-existing tests pass unchanged
- [ ] AC-B1: Create investment account with valid tax bucket + account group
- [ ] AC-B2: Create fund with symbol, name, optional dimensions
- [ ] AC-B3: Record position with price, quantity, current_value, cost_basis
- [ ] AC-B4: Duplicate (account, symbol, date) returns error
- [ ] AC-B5: List positions filtered by account ID
- [ ] AC-B6: List positions filtered by date range
- [ ] AC-B7: Latest positions snapshot (most recent per fund per account)
- [ ] AC-B8: List funds filtered by dimension
- [ ] AC-B9: Negative price/quantity/current_value rejected at service layer
- [ ] AC-B10: Position for nonexistent fund symbol rejected at service layer

## Risks

- **`DISTINCT ON` portability:** This is PostgreSQL-specific, but since LeoBloom
  is 100% Postgres, this is fine. No risk.
- **Decimal precision:** `numeric(18,4)` in PG maps to `decimal` in .NET.
  Npgsql handles this natively. No special handling needed.
- **Fund PK is `symbol` (varchar), not serial int:** Repository pattern differs
  slightly from int-PK entities. `findBySymbol` replaces `findById`. The Fund
  record uses `symbol` as its natural key. `create` returns the inserted Fund
  (use `RETURNING *`).
- **Dimension filter query:** `listByDimension` needs a dynamic WHERE clause
  based on the DU case. Use a match expression to select the column name — keep
  the SQL parameterized to avoid injection.

## Out of Scope

- CLI commands for portfolio management (P059)
- Data migration from external sources (P060)
- Reporting, allocation analysis, rebalancing (P061)
- CRUD for dimension tables (TaxBucket, AccountGroup, dim_*) — these are seeded
  in P057 and managed via SQL. Repositories for reading them are in scope only
  if needed by service validation. (We need `FundRepository.findBySymbol` for
  AC-B10, but we don't need TaxBucket CRUD services.)
