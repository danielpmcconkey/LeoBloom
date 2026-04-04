#!/usr/bin/env bash
# Check for potentially ambiguous TickSpec step definitions across all step def files.
#
# TickSpec scans the entire assembly — two step functions with overlapping regexes
# in different modules cause runtime "Ambiguous step definition" errors.
#
# This script extracts all step regexes and flags:
#   1. Exact duplicates across files
#   2. Patterns where one regex is a prefix of another (greedy .+ traps)
#
# Run before committing test code. Exit code 0 = clean, 1 = problems found.

set -euo pipefail

STEP_FILES=$(find "$(dirname "$0")/../Src" -name "*StepDefinitions.fs" 2>/dev/null)

if [ -z "$STEP_FILES" ]; then
    echo "No step definition files found."
    exit 0
fi

# Extract step regexes: lines matching [<Given>], [<When>], [<Then>] followed by ``regex``
# Output format: FILE:STEP_TYPE:REGEX
extract_steps() {
    local file="$1"
    local basename
    basename=$(basename "$file")
    grep -oP '\[<(Given|When|Then)>\]\s*``([^`]+)``' "$file" \
        | sed -E "s/\[<(Given|When|Then)>\]\s*\`\`([^\`]+)\`\`/${basename}:\1:\2/" \
        2>/dev/null || true
}

PROBLEMS=0
ALL_STEPS=""

for f in $STEP_FILES; do
    steps=$(extract_steps "$f")
    if [ -n "$steps" ]; then
        ALL_STEPS="${ALL_STEPS}${steps}"$'\n'
    fi
done

# Remove empty lines
ALL_STEPS=$(echo "$ALL_STEPS" | sed '/^$/d')

if [ -z "$ALL_STEPS" ]; then
    echo "No step definitions found."
    exit 0
fi

# Check 1: Exact duplicate regexes across different files
echo "=== Checking for exact duplicate step regexes ==="
dupes=$(echo "$ALL_STEPS" | awk -F: '{key=$2":"$3; files[key]=files[key] " " $1} END {for (k in files) { split(files[k], a, " "); n=0; for(i in a) if(a[i]!="") n++; if(n>1) print "  DUPLICATE ["k"] in:" files[k]}}')

if [ -n "$dupes" ]; then
    echo "$dupes"
    PROBLEMS=1
else
    echo "  None found."
fi

# Check 2: Prefix overlaps within same step type (Given/When/Then)
# A regex like "the balance is (.+)" will match "the balance is 1000.00 for a normal-debit..."
echo ""
echo "=== Checking for prefix overlaps (greedy .+ traps) ==="

# Get unique type:regex pairs
UNIQUE_STEPS=$(echo "$ALL_STEPS" | awk -F: '{print $2":"$3}' | sort -u)

overlap_found=0
while IFS= read -r step1; do
    type1="${step1%%:*}"
    regex1="${step1#*:}"
    while IFS= read -r step2; do
        type2="${step2%%:*}"
        regex2="${step2#*:}"
        # Same type, different regex, one starts with the other's literal prefix
        if [ "$type1" = "$type2" ] && [ "$regex1" != "$regex2" ]; then
            # Get the literal prefix (before first regex metachar)
            prefix1=$(echo "$regex1" | sed 's/[(.\\].*//')
            prefix2=$(echo "$regex2" | sed 's/[(.\\].*//')
            if [ -n "$prefix1" ] && [ -n "$prefix2" ]; then
                # Check if one prefix starts with the other
                if [[ "$prefix2" == "$prefix1"* ]] || [[ "$prefix1" == "$prefix2"* ]]; then
                    # Only report if the shorter one has a greedy pattern
                    shorter="$regex1"
                    longer="$regex2"
                    if [ ${#regex1} -gt ${#regex2} ]; then
                        shorter="$regex2"
                        longer="$regex1"
                    fi
                    if echo "$shorter" | grep -qP '\(\.\+\)$|\(\.\*\)$'; then
                        echo "  OVERLAP [$type1]:"
                        echo "    short: $shorter"
                        echo "    long:  $longer"
                        echo "    The shorter pattern's trailing (.+) or (.*) will match the longer line."
                        overlap_found=1
                    fi
                fi
            fi
        fi
    done <<< "$UNIQUE_STEPS"
done <<< "$UNIQUE_STEPS"

if [ $overlap_found -eq 0 ]; then
    echo "  None found."
fi

if [ $PROBLEMS -eq 1 ] || [ $overlap_found -eq 1 ]; then
    echo ""
    echo "FAILED: Step definition ambiguities detected."
    echo ""
    echo "How to fix:"
    echo "  DUPLICATES: TickSpec scans the entire assembly. Two modules cannot"
    echo "    define the same step regex. Add a feature-specific prefix to one:"
    echo "    e.g. 'a valid open fiscal period' -> 'a balance-test open fiscal period'"
    echo ""
    echo "  OVERLAPS: A step ending in (.+) or (.*) will greedily match longer"
    echo "    step text. Either:"
    echo "    (a) Use a more specific regex: (.+) -> (\\d+\\.\\d+)"
    echo "    (b) Rename the shorter step to be unambiguous:"
    echo "        'the balance is (.+)' -> 'the balance result is exactly (.+)'"
    exit 1
else
    echo ""
    echo "OK: No ambiguities detected."
    exit 0
fi
