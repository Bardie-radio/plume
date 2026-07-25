#!/bin/sh
# Drop to APP_UID after fixing Data Protection key dir ownership (named volumes are root-owned).
set -eu

KEYS="${BARDIE_DP_KEYS_PATH:-/app/dp-keys}"
mkdir -p "$KEYS"

if [ "$(id -u)" = "0" ]; then
  uid="${APP_UID:-1654}"
  chown -R "${uid}:${uid}" "$KEYS"
  exec setpriv --reuid="$uid" --regid="$uid" --clear-groups -- "$@"
fi

exec "$@"
