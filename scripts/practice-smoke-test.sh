#!/usr/bin/env bash
# Smoke test for local practice mode (read-only OFF).
# Usage: scripts/practice-smoke-test.sh http://localhost:5116
# Checks that pages load, no read-only banner shows, and a real form POST adds a customer.
# Run it against a throwaway database: it writes one customer row.
set -u
BASE="${1:?Pass the base URL, e.g. http://localhost:5116}"
BASE="${BASE%/}"
pass=0; fail=0
check() { if [ "$2" = "$3" ]; then echo "PASS  $1"; pass=$((pass+1)); else echo "FAIL  $1 (expected $3, got $2)"; fail=$((fail+1)); fi; }

for p in / /Ask /Customers /WorkOrders /Reports /Learn/Normalization; do
  check "GET $p" "$(curl -s -o /dev/null -w '%{http_code}' "$BASE$p")" 200
done
check "No read-only banner" "$(curl -s "$BASE/" | grep -c 'Read-only public demo')" 0

jar=$(mktemp)
token=$(curl -s -c "$jar" "$BASE/Customers/Create" | grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
name="CI Practice Customer $RANDOM"
check "POST /Customers/Create redirects (302)" \
  "$(curl -s -o /dev/null -w '%{http_code}' -b "$jar" -X POST "$BASE/Customers/Create" --data-urlencode "__RequestVerificationToken=$token" --data-urlencode "Name=$name" -d 'City=Testville')" 302
check "New customer is listed" "$(curl -s "$BASE/Customers" | grep -c "$name")" 1
rm -f "$jar"

echo "== $pass passed, $fail failed"
[ "$fail" -eq 0 ]
