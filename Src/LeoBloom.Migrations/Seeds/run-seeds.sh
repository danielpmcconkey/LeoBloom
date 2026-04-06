#!/usr/bin/env bash
# Seed runner for LeoBloom
# Executes all .sql files in Seeds/{env}/ via psql in sorted order.
# Usage: ./run-seeds.sh <env>

set -euo pipefail

if [ $# -lt 1 ]; then
    echo "Usage: $0 <environment>" >&2
    echo "Example: $0 dev" >&2
    exit 1
fi

ENV="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SEED_DIR="${SCRIPT_DIR}/${ENV}"

if [ ! -d "$SEED_DIR" ]; then
    echo "Error: Seed directory not found: ${SEED_DIR}" >&2
    exit 1
fi

DB_HOST="${LEOBLOOM_DB_HOST:-172.18.0.1}"
DB_PORT="${LEOBLOOM_DB_PORT:-5432}"
DB_NAME="${LEOBLOOM_DB_NAME:-leobloom_dev}"
DB_USER="${LEOBLOOM_DB_USER:-claude}"

if [ -z "${LEOBLOOM_DB_PASSWORD:-}" ]; then
    echo "Error: LEOBLOOM_DB_PASSWORD environment variable is required" >&2
    exit 1
fi

export PGPASSWORD="$LEOBLOOM_DB_PASSWORD"

SQL_FILES=$(find "$SEED_DIR" -maxdepth 1 -name '*.sql' -printf '%f\n' | sort)

if [ -z "$SQL_FILES" ]; then
    echo "No .sql files found in ${SEED_DIR}"
    exit 0
fi

FAILED=0
while IFS= read -r sql_file; do
    echo "Running: ${sql_file}"
    if ! psql -h "$DB_HOST" -p "$DB_PORT" -d "$DB_NAME" -U "$DB_USER" \
         --set ON_ERROR_STOP=on -f "${SEED_DIR}/${sql_file}"; then
        echo "FAILED: ${sql_file}" >&2
        FAILED=1
        break
    fi
    echo "OK: ${sql_file}"
done <<< "$SQL_FILES"

if [ "$FAILED" -ne 0 ]; then
    echo "Seed run FAILED." >&2
    exit 1
fi

echo "All seeds applied successfully for environment: ${ENV}"
