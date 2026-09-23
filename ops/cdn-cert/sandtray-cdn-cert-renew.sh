#!/usr/bin/env bash
set -euo pipefail

readonly ACME_HOME="/root/.acme.sh"
readonly CREDENTIALS_FILE="/etc/sandtray/cdn-cert.env"

if [[ ! -x "${ACME_HOME}/acme.sh" ]]; then
  echo "acme.sh is not installed at ${ACME_HOME}" >&2
  exit 1
fi

if [[ ! -r "${CREDENTIALS_FILE}" ]]; then
  echo "CDN certificate credentials are unavailable" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "${CREDENTIALS_FILE}"
set +a

exec "${ACME_HOME}/acme.sh" --cron --home "${ACME_HOME}"
