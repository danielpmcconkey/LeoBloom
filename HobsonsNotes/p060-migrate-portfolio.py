#!/usr/bin/env python3
"""
P060 — One-time portfolio data migration
householdbudget.personalfinance -> leobloom_prod.portfolio

Idempotent: uses ON CONFLICT DO NOTHING throughout.
Run from Hobson's side only. Script lives in HobsonsNotes/, not src tree.
"""

import os
import sys
import psycopg2

LEOBLOOM_DB_PASSWORD = os.environ.get("LEOBLOOM_DB_PASSWORD")
if not LEOBLOOM_DB_PASSWORD:
    print("ERROR: LEOBLOOM_DB_PASSWORD not set")
    sys.exit(1)

SOURCE = {
    "host": "localhost",
    "port": 5432,
    "dbname": "householdbudget",
    "user": "leobloom_hobson",
    "password": LEOBLOOM_DB_PASSWORD,
}

TARGET = {
    "host": "localhost",
    "port": 5432,
    "dbname": "leobloom_prod",
    "user": "leobloom_hobson",
    "password": LEOBLOOM_DB_PASSWORD,
}


def read_source(conn):
    """Read all source data into memory."""
    cur = conn.cursor()

    cur.execute("SELECT id, name FROM personalfinance.taxbucket ORDER BY id")
    tax_buckets = cur.fetchall()

    cur.execute("SELECT id, name FROM personalfinance.investmentaccountgroup ORDER BY id")
    account_groups = cur.fetchall()

    cur.execute(
        "SELECT id, name, taxbucket, investmentaccountgroup "
        "FROM personalfinance.investmentaccount ORDER BY id"
    )
    accounts = cur.fetchall()

    cur.execute("SELECT id, name FROM personalfinance.fundtype ORDER BY id")
    fundtypes = cur.fetchall()

    cur.execute(
        "SELECT symbol, name, investment_type, size, index_or_individual, "
        "sector, region, objective FROM personalfinance.fund ORDER BY symbol"
    )
    funds = cur.fetchall()

    cur.execute(
        "SELECT investmentaccount, symbol, position_date, price, "
        "total_quantity, current_value, cost_basis "
        "FROM personalfinance.position ORDER BY position_date, symbol"
    )
    positions = cur.fetchall()

    cur.close()
    return tax_buckets, account_groups, accounts, fundtypes, funds, positions


def write_target(conn, tax_buckets, account_groups, accounts, fundtypes, funds, positions):
    """Write all data to leobloom_prod.portfolio in a single transaction."""
    cur = conn.cursor()

    # Build fundtype ID -> name lookup
    ft_lookup = {row[0]: row[1] for row in fundtypes}

    # --- 1. Tax buckets ---
    old_to_new_tb = {}
    for old_id, name in tax_buckets:
        cur.execute(
            "INSERT INTO portfolio.tax_bucket (name) VALUES (%s) "
            "ON CONFLICT (name) DO NOTHING RETURNING id",
            (name,),
        )
        row = cur.fetchone()
        if row:
            old_to_new_tb[old_id] = row[0]
        else:
            cur.execute("SELECT id FROM portfolio.tax_bucket WHERE name = %s", (name,))
            old_to_new_tb[old_id] = cur.fetchone()[0]
    print(f"  tax_bucket: {len(old_to_new_tb)} rows")

    # --- 2. Account groups ---
    old_to_new_ag = {}
    for old_id, name in account_groups:
        cur.execute(
            "INSERT INTO portfolio.account_group (name) VALUES (%s) "
            "ON CONFLICT (name) DO NOTHING RETURNING id",
            (name,),
        )
        row = cur.fetchone()
        if row:
            old_to_new_ag[old_id] = row[0]
        else:
            cur.execute("SELECT id FROM portfolio.account_group WHERE name = %s", (name,))
            old_to_new_ag[old_id] = cur.fetchone()[0]
    print(f"  account_group: {len(old_to_new_ag)} rows")

    # --- 3. Dimension tables ---
    # Collect distinct values per axis from fund data
    dim_axes = {
        "investment_type": ("portfolio.dim_investment_type", 2),  # fund column index
        "market_cap": ("portfolio.dim_market_cap", 3),
        "index_type": ("portfolio.dim_index_type", 4),
        "sector": ("portfolio.dim_sector", 5),
        "region": ("portfolio.dim_region", 6),
        "objective": ("portfolio.dim_objective", 7),
    }

    # For each axis, collect the distinct fundtype IDs used, resolve to names,
    # insert into the dim table, and build old_ft_id -> new_dim_id mappings
    dim_mappings = {}  # axis_name -> {old_fundtype_id: new_dim_id}

    for axis_name, (table, col_idx) in dim_axes.items():
        mapping = {}
        distinct_ft_ids = set(f[col_idx] for f in funds if f[col_idx] is not None)

        for ft_id in sorted(distinct_ft_ids):
            name = ft_lookup[ft_id]
            cur.execute(
                f"INSERT INTO {table} (name) VALUES (%s) "
                f"ON CONFLICT (name) DO NOTHING RETURNING id",
                (name,),
            )
            row = cur.fetchone()
            if row:
                mapping[ft_id] = row[0]
            else:
                cur.execute(f"SELECT id FROM {table} WHERE name = %s", (name,))
                mapping[ft_id] = cur.fetchone()[0]

        dim_mappings[axis_name] = mapping
        print(f"  {table}: {len(mapping)} distinct values")

    # --- 4. Investment accounts ---
    old_to_new_acct = {}
    for old_id, name, tb_id, ag_id in accounts:
        new_tb = old_to_new_tb[tb_id]
        new_ag = old_to_new_ag[ag_id]
        cur.execute(
            "INSERT INTO portfolio.investment_account (name, tax_bucket_id, account_group_id) "
            "VALUES (%s, %s, %s) RETURNING id",
            (name, new_tb, new_ag),
        )
        row = cur.fetchone()
        if row:
            old_to_new_acct[old_id] = row[0]
        else:
            # ON CONFLICT not possible here (no unique on name), so this is
            # a genuine duplicate insert attempt — shouldn't happen on first run
            print(f"  WARNING: duplicate investment account '{name}', skipping")
    print(f"  investment_account: {len(old_to_new_acct)} rows")

    # --- 5. Funds ---
    fund_count = 0
    for symbol, name, it, sz, ioi, sec, reg, obj in funds:
        new_it = dim_mappings["investment_type"].get(it)
        new_mc = dim_mappings["market_cap"].get(sz)
        new_ix = dim_mappings["index_type"].get(ioi)
        new_sec = dim_mappings["sector"].get(sec)
        new_reg = dim_mappings["region"].get(reg)
        new_obj = dim_mappings["objective"].get(obj)
        cur.execute(
            "INSERT INTO portfolio.fund "
            "(symbol, name, investment_type_id, market_cap_id, index_type_id, "
            "sector_id, region_id, objective_id) "
            "VALUES (%s, %s, %s, %s, %s, %s, %s, %s) "
            "ON CONFLICT (symbol) DO NOTHING",
            (symbol, name, new_it, new_mc, new_ix, new_sec, new_reg, new_obj),
        )
        fund_count += 1
    print(f"  fund: {fund_count} rows")

    # --- 6. Positions ---
    pos_count = 0
    for acct_id, symbol, pos_date, price, quantity, value, cost_basis in positions:
        new_acct = old_to_new_acct[acct_id]
        cur.execute(
            "INSERT INTO portfolio.position "
            "(investment_account_id, symbol, position_date, price, quantity, "
            "current_value, cost_basis) "
            "VALUES (%s, %s, %s, %s, %s, %s, %s) "
            "ON CONFLICT (investment_account_id, symbol, position_date) DO NOTHING",
            (new_acct, symbol, pos_date, price, quantity, value, cost_basis),
        )
        pos_count += 1
    print(f"  position: {pos_count} rows")

    conn.commit()
    cur.close()


def verify(conn):
    """Post-migration verification."""
    cur = conn.cursor()

    counts = {}
    for table in [
        "tax_bucket", "account_group", "investment_account", "fund", "position",
        "dim_investment_type", "dim_market_cap", "dim_index_type",
        "dim_sector", "dim_region", "dim_objective",
    ]:
        cur.execute(f"SELECT count(*) FROM portfolio.{table}")
        counts[table] = cur.fetchone()[0]

    print("\nVerification — row counts:")
    for t, c in counts.items():
        print(f"  portfolio.{t}: {c}")

    # Spot check: FXAIX
    cur.execute(
        "SELECT f.symbol, f.name, dit.name, dmc.name, dix.name, ds.name, dr.name, do2.name "
        "FROM portfolio.fund f "
        "LEFT JOIN portfolio.dim_investment_type dit ON f.investment_type_id = dit.id "
        "LEFT JOIN portfolio.dim_market_cap dmc ON f.market_cap_id = dmc.id "
        "LEFT JOIN portfolio.dim_index_type dix ON f.index_type_id = dix.id "
        "LEFT JOIN portfolio.dim_sector ds ON f.sector_id = ds.id "
        "LEFT JOIN portfolio.dim_region dr ON f.region_id = dr.id "
        "LEFT JOIN portfolio.dim_objective do2 ON f.objective_id = do2.id "
        "WHERE f.symbol = 'FXAIX'"
    )
    fxaix = cur.fetchone()
    if fxaix:
        print(f"\nSpot check — FXAIX:")
        print(f"  Name: {fxaix[1]}")
        print(f"  Type: {fxaix[2]}, Cap: {fxaix[3]}, Index: {fxaix[4]}")
        print(f"  Sector: {fxaix[5]}, Region: {fxaix[6]}, Objective: {fxaix[7]}")

    # Spot check: FXAIX position in Dan's Roth, Jan 2026
    cur.execute(
        "SELECT ia.name, p.position_date, p.price, p.quantity, p.current_value, p.cost_basis "
        "FROM portfolio.position p "
        "JOIN portfolio.investment_account ia ON p.investment_account_id = ia.id "
        "WHERE p.symbol = 'FXAIX' AND ia.name ILIKE '%roth%' "
        "AND p.position_date >= '2026-01-01' AND p.position_date < '2026-02-01'"
    )
    pos = cur.fetchone()
    if pos:
        print(f"\nSpot check — FXAIX in {pos[0]}, {pos[1]}:")
        print(f"  Price: {pos[2]}, Qty: {pos[3]}, Value: {pos[4]}, Cost: {pos[5]}")
    else:
        print("\nWARNING: FXAIX Roth Jan 2026 position not found")

    cur.close()


def main():
    print("P060 — Portfolio Data Migration")
    print("================================\n")

    print("Reading from householdbudget.personalfinance...")
    src = psycopg2.connect(**SOURCE)
    src.set_session(readonly=True)
    tax_buckets, account_groups, accounts, fundtypes, funds, positions = read_source(src)
    src.close()
    print(f"  Read: {len(tax_buckets)} tax_buckets, {len(account_groups)} account_groups, "
          f"{len(accounts)} accounts, {len(fundtypes)} fundtypes, "
          f"{len(funds)} funds, {len(positions)} positions\n")

    print("Writing to leobloom_prod.portfolio...")
    tgt = psycopg2.connect(**TARGET)
    tgt.set_session(autocommit=False)
    try:
        write_target(tgt, tax_buckets, account_groups, accounts, fundtypes, funds, positions)
    except Exception:
        tgt.rollback()
        tgt.close()
        raise

    print("\nVerifying...")
    verify(tgt)
    tgt.close()

    print("\nDone.")


if __name__ == "__main__":
    main()
