Feature: Normal balance resolution

  The resolveBalance function in LeoBloom.Domain.Ledger is the single
  canonical implementation of the normal balance rule:
  debit-normal accounts carry a positive balance when debits exceed credits;
  credit-normal accounts carry a positive balance when credits exceed debits.

  # Scope: Behavioral specification for the resolveBalance domain function,
  # plus a guardrail verifying inline balance arithmetic does not re-accumulate
  # in repositories or services.
  #
  # Structural checks (function placement in Ledger.fs, fsproj compilation order)
  # are the QE's responsibility as source-level assertions, not Gherkin scenarios.

  # --- Domain function arithmetic ---

  @FT-NBC-001
  Scenario Outline: resolveBalance computes directional balance by normal balance type
    When I call resolveBalance with normal balance <normal_balance>, debits <debits>, and credits <credits>
    Then the result is <expected>

    Examples:
      | normal_balance | debits  | credits | expected  |
      | Debit          | 1000.00 | 400.00  | 600.00    |
      | Debit          | 400.00  | 1000.00 | -600.00   |
      | Debit          | 500.00  | 500.00  | 0.00      |
      | Debit          | 0.00    | 0.00    | 0.00      |
      | Credit         | 400.00  | 1000.00 | 600.00    |
      | Credit         | 1000.00 | 400.00  | -600.00   |
      | Credit         | 500.00  | 500.00  | 0.00      |
      | Credit         | 0.00    | 0.00    | 0.00      |

  # --- Consolidation guardrail ---
  #
  # The problem this phase solves is 7 independent inline implementations that
  # can drift from each other. Once consolidated, this scenario prevents
  # re-accumulation of inline arithmetic in future changes.

  @FT-NBC-002
  Scenario: No inline normal-balance arithmetic exists outside the Domain module
    Given the LeoBloom source tree exists
    When I search all .fs files under Src excluding LeoBloom.Domain for inline debit/credit arithmetic patterns
    Then zero matches are found
