module LeoBloom.Tests.PortfolioStructuralConstraintsTests

open System
open Npgsql
open Xunit
open LeoBloom.Utilities
open LeoBloom.Tests.TestHelpers

// =====================================================================
// Portfolio schema structural constraint tests
// Maps to Specs/Structural/PortfolioStructuralConstraints.feature
//
// Cleanup strategy: no shared tracker — each test creates isolated data
// using a unique prefix and deletes it inline in the finally block.
// FK delete order: position -> investment_account -> fund -> account_group -> tax_bucket
// =====================================================================

// =====================================================================
// tax_bucket
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PSC-001")>]
let ``tax_bucket requires a name`` () =
    use conn = DataSource.openConnection()
    try
        let ex = ConstraintAssert.tryInsert conn
                    "INSERT INTO portfolio.tax_bucket (name) VALUES (NULL)"
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally ()

[<Fact>]
[<Trait("GherkinId", "FT-PSC-002")>]
let ``tax_bucket name must be unique`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let name = sprintf "%s_taxbucket" prefix
    let mutable insertedId = 0
    try
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.tax_bucket (name) VALUES (@n) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        insertedId <- cmd.ExecuteScalar() :?> int
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.tax_bucket (name) VALUES (@n)"
                    (fun c -> c.Parameters.AddWithValue("@n", name) |> ignore)
        ConstraintAssert.assertUnique ex "Expected UNIQUE violation"
    finally
        if insertedId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.tax_bucket WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", insertedId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-002 cleanup error: %s" ex.Message

// =====================================================================
// account_group
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PSC-003")>]
let ``account_group requires a name`` () =
    use conn = DataSource.openConnection()
    try
        let ex = ConstraintAssert.tryInsert conn
                    "INSERT INTO portfolio.account_group (name) VALUES (NULL)"
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally ()

[<Fact>]
[<Trait("GherkinId", "FT-PSC-004")>]
let ``account_group name must be unique`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let name = sprintf "%s_acctgrp" prefix
    let mutable insertedId = 0
    try
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.account_group (name) VALUES (@n) RETURNING id", conn)
        cmd.Parameters.AddWithValue("@n", name) |> ignore
        insertedId <- cmd.ExecuteScalar() :?> int
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.account_group (name) VALUES (@n)"
                    (fun c -> c.Parameters.AddWithValue("@n", name) |> ignore)
        ConstraintAssert.assertUnique ex "Expected UNIQUE violation"
    finally
        if insertedId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.account_group WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", insertedId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-004 cleanup error: %s" ex.Message

// =====================================================================
// investment_account
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PSC-005")>]
let ``investment_account requires a name`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let mutable tbId = 0
    let mutable agId = 0
    try
        use cmdTb = new NpgsqlCommand(
            "INSERT INTO portfolio.tax_bucket (name) VALUES (@n) RETURNING id", conn)
        cmdTb.Parameters.AddWithValue("@n", sprintf "%s_tb005" prefix) |> ignore
        tbId <- cmdTb.ExecuteScalar() :?> int

        use cmdAg = new NpgsqlCommand(
            "INSERT INTO portfolio.account_group (name) VALUES (@n) RETURNING id", conn)
        cmdAg.Parameters.AddWithValue("@n", sprintf "%s_ag005" prefix) |> ignore
        agId <- cmdAg.ExecuteScalar() :?> int

        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id) VALUES (NULL, @tb, @ag)"
                    (fun c ->
                        c.Parameters.AddWithValue("@tb", tbId) |> ignore
                        c.Parameters.AddWithValue("@ag", agId) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally
        if agId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.account_group WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", agId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-005 cleanup (account_group) error: %s" ex.Message
        if tbId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.tax_bucket WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", tbId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-005 cleanup (tax_bucket) error: %s" ex.Message

[<Fact>]
[<Trait("GherkinId", "FT-PSC-006")>]
let ``investment_account tax_bucket_id must reference a valid tax_bucket`` () =
    use conn = DataSource.openConnection()
    try
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id) VALUES ('Test', @tb, 1)"
                    (fun c -> c.Parameters.AddWithValue("@tb", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally ()

[<Fact>]
[<Trait("GherkinId", "FT-PSC-007")>]
let ``investment_account account_group_id must reference a valid account_group`` () =
    use conn = DataSource.openConnection()
    try
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id) VALUES ('Test', 1, @ag)"
                    (fun c -> c.Parameters.AddWithValue("@ag", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally ()

// =====================================================================
// fund
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PSC-008")>]
let ``fund symbol serves as the primary key — duplicate symbols are rejected`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let symbol = sprintf "%sFND" prefix
    try
        use cmd = new NpgsqlCommand(
            "INSERT INTO portfolio.fund (symbol, name) VALUES (@s, @n)", conn)
        cmd.Parameters.AddWithValue("@s", symbol) |> ignore
        cmd.Parameters.AddWithValue("@n", sprintf "%s test fund" prefix) |> ignore
        cmd.ExecuteNonQuery() |> ignore

        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.fund (symbol, name) VALUES (@s, @n)"
                    (fun c ->
                        c.Parameters.AddWithValue("@s", symbol) |> ignore
                        c.Parameters.AddWithValue("@n", "Duplicate Fund") |> ignore)
        ConstraintAssert.assertSqlState "23505" ex "Expected PK violation (unique)"
    finally
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM portfolio.fund WHERE symbol = @s", conn)
            cmd.Parameters.AddWithValue("@s", symbol) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex -> eprintfn "PSC-008 cleanup error: %s" ex.Message

[<Fact>]
[<Trait("GherkinId", "FT-PSC-009")>]
let ``fund requires a name`` () =
    use conn = DataSource.openConnection()
    try
        let ex = ConstraintAssert.tryInsert conn
                    "INSERT INTO portfolio.fund (symbol, name) VALUES ('TSTNULL', NULL)"
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally ()

// =====================================================================
// position
// =====================================================================

[<Fact>]
[<Trait("GherkinId", "FT-PSC-010")>]
let ``position rejects duplicate (investment_account, symbol, position_date) combination`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let symbol = sprintf "%sPOS" prefix
    let mutable tbId = 0
    let mutable agId = 0
    let mutable iaId = 0
    let mutable posId = 0
    try
        use cmdTb = new NpgsqlCommand(
            "INSERT INTO portfolio.tax_bucket (name) VALUES (@n) RETURNING id", conn)
        cmdTb.Parameters.AddWithValue("@n", sprintf "%s_tb010" prefix) |> ignore
        tbId <- cmdTb.ExecuteScalar() :?> int

        use cmdAg = new NpgsqlCommand(
            "INSERT INTO portfolio.account_group (name) VALUES (@n) RETURNING id", conn)
        cmdAg.Parameters.AddWithValue("@n", sprintf "%s_ag010" prefix) |> ignore
        agId <- cmdAg.ExecuteScalar() :?> int

        use cmdIa = new NpgsqlCommand(
            "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id) VALUES (@n, @tb, @ag) RETURNING id", conn)
        cmdIa.Parameters.AddWithValue("@n", sprintf "%s_ia010" prefix) |> ignore
        cmdIa.Parameters.AddWithValue("@tb", tbId) |> ignore
        cmdIa.Parameters.AddWithValue("@ag", agId) |> ignore
        iaId <- cmdIa.ExecuteScalar() :?> int

        use cmdFund = new NpgsqlCommand(
            "INSERT INTO portfolio.fund (symbol, name) VALUES (@s, @n)", conn)
        cmdFund.Parameters.AddWithValue("@s", symbol) |> ignore
        cmdFund.Parameters.AddWithValue("@n", sprintf "%s test fund" prefix) |> ignore
        cmdFund.ExecuteNonQuery() |> ignore

        use cmdPos = new NpgsqlCommand(
            "INSERT INTO portfolio.position (investment_account_id, symbol, position_date, price, quantity, current_value, cost_basis) \
             VALUES (@ia, @s, '2026-01-01', 100.0000, 10.0000, 1000.0000, 950.0000) RETURNING id", conn)
        cmdPos.Parameters.AddWithValue("@ia", iaId) |> ignore
        cmdPos.Parameters.AddWithValue("@s", symbol) |> ignore
        posId <- cmdPos.ExecuteScalar() :?> int

        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.position (investment_account_id, symbol, position_date, price, quantity, current_value, cost_basis) \
                     VALUES (@ia, @s, '2026-01-01', 101.0000, 10.0000, 1010.0000, 950.0000)"
                    (fun c ->
                        c.Parameters.AddWithValue("@ia", iaId) |> ignore
                        c.Parameters.AddWithValue("@s", symbol) |> ignore)
        ConstraintAssert.assertUnique ex "Expected UNIQUE violation on (investment_account_id, symbol, position_date)"
    finally
        if posId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.position WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", posId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-010 cleanup (position) error: %s" ex.Message
        if iaId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.investment_account WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", iaId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-010 cleanup (investment_account) error: %s" ex.Message
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM portfolio.fund WHERE symbol = @s", conn)
            cmd.Parameters.AddWithValue("@s", symbol) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex -> eprintfn "PSC-010 cleanup (fund) error: %s" ex.Message
        if agId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.account_group WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", agId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-010 cleanup (account_group) error: %s" ex.Message
        if tbId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.tax_bucket WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", tbId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-010 cleanup (tax_bucket) error: %s" ex.Message

[<Fact>]
[<Trait("GherkinId", "FT-PSC-011")>]
let ``position investment_account_id must reference a valid investment_account`` () =
    use conn = DataSource.openConnection()
    try
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.position (investment_account_id, symbol, position_date, price, quantity, current_value, cost_basis) \
                     VALUES (@ia, 'VTI', '2026-01-01', 100.0000, 10.0000, 1000.0000, 950.0000)"
                    (fun c -> c.Parameters.AddWithValue("@ia", 9999) |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally ()

[<Fact>]
[<Trait("GherkinId", "FT-PSC-012")>]
let ``position symbol must reference a valid fund`` () =
    use conn = DataSource.openConnection()
    try
        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.position (investment_account_id, symbol, position_date, price, quantity, current_value, cost_basis) \
                     VALUES (1, @s, '2026-01-01', 100.0000, 10.0000, 1000.0000, 950.0000)"
                    (fun c -> c.Parameters.AddWithValue("@s", "FAKE") |> ignore)
        ConstraintAssert.assertFk ex "Expected FK violation"
    finally ()

[<Fact>]
[<Trait("GherkinId", "FT-PSC-013")>]
let ``position requires a position_date`` () =
    use conn = DataSource.openConnection()
    let prefix = TestData.uniquePrefix()
    let symbol = sprintf "%sPD3" prefix
    let mutable tbId = 0
    let mutable agId = 0
    let mutable iaId = 0
    try
        use cmdTb = new NpgsqlCommand(
            "INSERT INTO portfolio.tax_bucket (name) VALUES (@n) RETURNING id", conn)
        cmdTb.Parameters.AddWithValue("@n", sprintf "%s_tb013" prefix) |> ignore
        tbId <- cmdTb.ExecuteScalar() :?> int

        use cmdAg = new NpgsqlCommand(
            "INSERT INTO portfolio.account_group (name) VALUES (@n) RETURNING id", conn)
        cmdAg.Parameters.AddWithValue("@n", sprintf "%s_ag013" prefix) |> ignore
        agId <- cmdAg.ExecuteScalar() :?> int

        use cmdIa = new NpgsqlCommand(
            "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id) VALUES (@n, @tb, @ag) RETURNING id", conn)
        cmdIa.Parameters.AddWithValue("@n", sprintf "%s_ia013" prefix) |> ignore
        cmdIa.Parameters.AddWithValue("@tb", tbId) |> ignore
        cmdIa.Parameters.AddWithValue("@ag", agId) |> ignore
        iaId <- cmdIa.ExecuteScalar() :?> int

        use cmdFund = new NpgsqlCommand(
            "INSERT INTO portfolio.fund (symbol, name) VALUES (@s, @n)", conn)
        cmdFund.Parameters.AddWithValue("@s", symbol) |> ignore
        cmdFund.Parameters.AddWithValue("@n", sprintf "%s test fund" prefix) |> ignore
        cmdFund.ExecuteNonQuery() |> ignore

        let ex = ConstraintAssert.tryExec conn
                    "INSERT INTO portfolio.position (investment_account_id, symbol, position_date, price, quantity, current_value, cost_basis) \
                     VALUES (@ia, @s, NULL, 100.0000, 10.0000, 1000.0000, 950.0000)"
                    (fun c ->
                        c.Parameters.AddWithValue("@ia", iaId) |> ignore
                        c.Parameters.AddWithValue("@s", symbol) |> ignore)
        ConstraintAssert.assertNotNull ex "Expected NOT NULL violation"
    finally
        if iaId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.investment_account WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", iaId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-013 cleanup (investment_account) error: %s" ex.Message
        try
            use cmd = new NpgsqlCommand(
                "DELETE FROM portfolio.fund WHERE symbol = @s", conn)
            cmd.Parameters.AddWithValue("@s", symbol) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        with ex -> eprintfn "PSC-013 cleanup (fund) error: %s" ex.Message
        if agId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.account_group WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", agId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-013 cleanup (account_group) error: %s" ex.Message
        if tbId > 0 then
            try
                use cmd = new NpgsqlCommand(
                    "DELETE FROM portfolio.tax_bucket WHERE id = @id", conn)
                cmd.Parameters.AddWithValue("@id", tbId) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            with ex -> eprintfn "PSC-013 cleanup (tax_bucket) error: %s" ex.Message
