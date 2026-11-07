#!/usr/bin/env bash
# Smoke test for the read-only public demo.
# Usage: scripts/demo-smoke-test.sh http://localhost:5115
#        scripts/demo-smoke-test.sh https://<your-function-url>.lambda-url.<region>.on.aws
# Checks that GET pages work and every data-changing request is refused
# server-side (403) without changing what the pages show. The public demo
# returns "Changes unavailable" HTML for mutations (no read-only banner).
set -u
BASE="${1:?Pass the base URL, e.g. http://localhost:5115}"
BASE="${BASE%/}"
pass=0; fail=0
check() { if [ "$2" = "$3" ]; then echo "PASS  $1"; pass=$((pass+1)); else echo "FAIL  $1 (expected $3, got $2)"; fail=$((fail+1)); fi; }
rows() { curl -s "$BASE$1" | grep -c '<tr>'; }

echo "== GET pages"
for p in / /Ask /Customers /WorkOrders "/WorkOrders?status=Open" /WorkOrders/Details/1 /Reports "/Reports?buggy=true" "/Reports?customerId=2" /Learn/Normalization /Customers/Create /WorkOrders/Create /Customers/Edit/1 /WorkOrders/Edit/1; do
  check "GET $p" "$(curl -s -o /dev/null -w '%{http_code}' "$BASE$p")" 200
done
check "Ask page loads" "$(curl -s "$BASE/Ask" | grep -c 'Ask JobDesk')" 1
check "No old read-only banner copy" "$(curl -s "$BASE/" | grep -c 'Read-only public demo')" 0
check "Upload form hidden on Details" "$(curl -s "$BASE/WorkOrders/Details/1" | grep -c 'enctype="multipart/form-data"')" 0
check "X-Content-Type-Options header" "$(curl -s -D - -o /dev/null "$BASE/" | grep -ci '^x-content-type-options: nosniff')" 1

customers_before=$(rows /Customers); orders_before=$(rows /WorkOrders); details_before=$(curl -s "$BASE/WorkOrders/Details/1" | md5sum)

echo "== Mutations must be refused (403)"
# A real antiforgery token and cookie, so a refusal can't be blamed on a missing token.
jar=$(mktemp)
token=$(curl -s -c "$jar" "$BASE/Customers/Create" | grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
post() { curl -s -o /dev/null -w '%{http_code}' -b "$jar" -X POST "$BASE$1" --data-urlencode "__RequestVerificationToken=$token" "${@:2}"; }
check "POST /Customers/Create"           "$(post /Customers/Create -d 'Name=Intruder&City=X')" 403
check "POST /Customers/Edit/1"           "$(post /Customers/Edit/1 -d 'Id=1&Name=Changed')" 403
check "POST /WorkOrders/Create"          "$(post /WorkOrders/Create -d 'Title=Intruder&CustomerId=1&Status=0')" 403
check "POST /WorkOrders/Edit/1"          "$(post /WorkOrders/Edit/1 -d 'Id=1&Title=Changed&CustomerId=1&Status=3')" 403
check "POST /WorkOrders/DeleteAttachment/1" "$(post /WorkOrders/DeleteAttachment/1)" 403
printf 'fake image' > "$jar.png"
check "POST /WorkOrders/Upload/1 (multipart)" "$(curl -s -o /dev/null -w '%{http_code}' -b "$jar" -X POST "$BASE/WorkOrders/Upload/1" -F "__RequestVerificationToken=$token" -F "file=@$jar.png;type=image/png")" 403
for m in PUT PATCH DELETE; do
  check "$m /Customers/Edit/1" "$(curl -s -o /dev/null -w '%{http_code}' -X $m "$BASE/Customers/Edit/1")" 403
  check "$m /WorkOrders/DeleteAttachment/1" "$(curl -s -o /dev/null -w '%{http_code}' -X $m "$BASE/WorkOrders/DeleteAttachment/1")" 403
done
check "POST without token is also refused" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/Customers/Create" -d 'Name=x')" 403
rm -f "$jar" "$jar.png"

echo "== Data unchanged"
check "Customer rows unchanged"   "$(rows /Customers)" "$customers_before"
check "Work order rows unchanged" "$(rows /WorkOrders)" "$orders_before"
check "Work order 1 unchanged"    "$(curl -s "$BASE/WorkOrders/Details/1" | md5sum)" "$details_before"

echo "== $pass passed, $fail failed"
[ "$fail" -eq 0 ]
