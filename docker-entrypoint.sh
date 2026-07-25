#!/bin/sh
# Drop to APP_UID after fixing Data Protection + mesh TLS dir ownership (named volumes are root-owned).
set -eu

KEYS="${BARDIE_DP_KEYS_PATH:-/app/dp-keys}"
TLS="${MODULE_TLS_DATA_PATH:-/data/mtls}"
mkdir -p "$KEYS" "$TLS"

if [ "$(id -u)" = "0" ]; then
  uid="${APP_UID:-1654}"
  chown -R "${uid}:${uid}" "$KEYS" "$TLS"
  # Alpine: su-exec (setpriv is util-linux and heavier).
  exec su-exec "${uid}:${uid}" "$@"
fi

exec "$@"
