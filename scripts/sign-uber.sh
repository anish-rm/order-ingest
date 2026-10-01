#!/usr/bin/env bash
# Computes the X-Uber-Signature for a fixture (lowercase-hex HMAC-SHA256 of
# the exact file bytes, keyed with the dev-only client secret) and prints a
# ready-to-run curl. --data-binary is essential: plain -d strips newlines,
# which would change the signed bytes.
set -euo pipefail

FIXTURE="${1:-fixtures/uber-notification.json}"
SECRET="${UBER_CLIENT_SECRET:-dev-only-uber-client-secret}"
URL="${API_URL:-http://localhost:5080}/webhooks/orders"

if [[ ! -f "$FIXTURE" ]]; then
  echo "Fixture not found: $FIXTURE (run from the repo root)" >&2
  exit 1
fi

SIGNATURE=$(openssl dgst -sha256 -hmac "$SECRET" <"$FIXTURE" | awk '{print $NF}')

echo "X-Uber-Signature: $SIGNATURE"
echo
echo "curl -i -X POST '$URL' \\"
echo "  -H 'Content-Type: application/json' \\"
echo "  -H 'X-Uber-Signature: $SIGNATURE' \\"
echo "  --data-binary @$FIXTURE"
