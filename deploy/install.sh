#!/usr/bin/env bash
# SpoolHero installer for Debian 12/13 and Ubuntu 22.04/24.04 (LXC container or VM).
#
# Installs the ASP.NET Core 10 runtime, MariaDB, the latest SpoolHero release and a
# systemd service. Safe to run again: existing passwords, secrets and settings are kept.
#
# Usage (as root):
#   bash install.sh                  # fresh install, or repair an existing one
#   bash install.sh --update         # update to the latest release (with backup + rollback)
#   bash install.sh --version 2.4.2  # install/update to a specific release
#   bash install.sh --help
#
# One-liner:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/Fewiel/SpoolHero/main/deploy/install.sh)"

set -Eeuo pipefail

# ---------------------------------------------------------------------------
# Defaults (every option can also be set via environment variable)
# ---------------------------------------------------------------------------
REPO="${SPOOLHERO_REPO:-Fewiel/SpoolHero}"
INSTALL_DIR="${SPOOLHERO_INSTALL_DIR:-/opt/spoolhero}"
SERVICE_NAME="${SPOOLHERO_SERVICE_NAME:-spoolhero}"
SERVICE_USER="${SPOOLHERO_USER:-spoolhero}"
DB_NAME="${SPOOLHERO_DB_NAME:-spoolhero}"
DB_USER="${SPOOLHERO_DB_USER:-spoolhero}"
PORT="${SPOOLHERO_PORT:-}"
VERSION="${SPOOLHERO_VERSION:-}"
ZIP_FILE="${SPOOLHERO_ZIP:-}"
UNIT_DIR="${SPOOLHERO_UNIT_DIR:-/etc/systemd/system}"
BACKUP_DIR="${SPOOLHERO_BACKUP_DIR:-/var/backups/spoolhero}"
STATE_DIR="${SPOOLHERO_STATE_DIR:-/var/lib/spoolhero}"
BIN_DIR="${SPOOLHERO_BIN_DIR:-/usr/local/sbin}"
SKIP_SYSTEMD="${SPOOLHERO_SKIP_SYSTEMD:-0}"
HEALTH_TIMEOUT="${SPOOLHERO_HEALTH_TIMEOUT:-180}"
DOTNET_INSTALL_DIR="/usr/share/dotnet"
DEFAULT_PORT=5000
UPDATE=0
FORCE=0

APP_DLL="SpoolManager.Server.dll"
VERSION_FILE_NAME=".spoolhero-version"
PLACEHOLDER_SECRET="CHANGE_ME_TO_A_SECURE_RANDOM_KEY_AT_LEAST_32_CHARS"

# ---------------------------------------------------------------------------
# Logging
# ---------------------------------------------------------------------------
if [[ -t 1 ]]; then
  C_RESET=$'\e[0m'; C_BLUE=$'\e[34m'; C_GREEN=$'\e[32m'; C_YELLOW=$'\e[33m'; C_RED=$'\e[31m'; C_BOLD=$'\e[1m'
else
  C_RESET=""; C_BLUE=""; C_GREEN=""; C_YELLOW=""; C_RED=""; C_BOLD=""
fi
info() { printf '%s[INFO]%s %s\n' "$C_BLUE" "$C_RESET" "$*"; }
ok()   { printf '%s[ OK ]%s %s\n' "$C_GREEN" "$C_RESET" "$*"; }
warn() { printf '%s[WARN]%s %s\n' "$C_YELLOW" "$C_RESET" "$*" >&2; }
die()  { printf '%s[FAIL]%s %s\n' "$C_RED" "$C_RESET" "$*" >&2; exit 1; }
on_error() { printf '%s[FAIL]%s Unexpected error (exit %s) at line %s: %s\n' "$C_RED" "$C_RESET" "$1" "$2" "$3" >&2; }
trap 'on_error $? $LINENO "$BASH_COMMAND"' ERR

usage() {
  cat <<EOF
SpoolHero installer for Debian/Ubuntu (run as root inside the LXC container or VM).

Usage: install.sh [options]

Options:
  --update             Update an existing installation (database backup, rollback on failure)
  --version X.Y.Z      Install this release instead of the latest one
  --zip PATH           Install from a local release zip instead of downloading it
  --port N             HTTP port SpoolHero listens on (default ${DEFAULT_PORT}; changes an existing install)
  --force              With --update: reinstall even if the version is already installed
  --install-dir DIR    Installation directory (default ${INSTALL_DIR})
  --user NAME          System user the service runs as (default ${SERVICE_USER}, created if missing)
  --db-name NAME       Database name for a new installation (default ${DB_NAME})
  --db-user NAME       Database user for a new installation (default ${DB_USER})
  --repo OWNER/NAME    GitHub repository to download releases from (default ${REPO})
  -h, --help           Show this help

Environment:
  SPOOLHERO_SKIP_SYSTEMD=1   Do not use systemd (auto-detected when systemd is not running);
                             the unit file is still written, the app is started in the background.
  SPOOLHERO_UNIT_DIR, SPOOLHERO_BACKUP_DIR, SPOOLHERO_STATE_DIR, SPOOLHERO_BIN_DIR,
  SPOOLHERO_HEALTH_TIMEOUT
EOF
}

# ---------------------------------------------------------------------------
# Arguments
# ---------------------------------------------------------------------------
need_arg() { [[ $# -ge 2 && -n "$2" ]] || die "Option $1 requires a value"; }
while [[ $# -gt 0 ]]; do
  case "$1" in
    --update) UPDATE=1 ;;
    --force) FORCE=1 ;;
    --version) need_arg "$@"; VERSION="$2"; shift ;;
    --zip) need_arg "$@"; ZIP_FILE="$2"; shift ;;
    --port) need_arg "$@"; PORT="$2"; shift ;;
    --install-dir) need_arg "$@"; INSTALL_DIR="$2"; shift ;;
    --user) need_arg "$@"; SERVICE_USER="$2"; shift ;;
    --db-name) need_arg "$@"; DB_NAME="$2"; shift ;;
    --db-user) need_arg "$@"; DB_USER="$2"; shift ;;
    --repo) need_arg "$@"; REPO="$2"; shift ;;
    -h|--help) usage; exit 0 ;;
    *) usage >&2; die "Unknown option: $1" ;;
  esac
  shift
done

VERSION="${VERSION#v}"
INSTALL_DIR="${INSTALL_DIR%/}"
[[ -z "$PORT" || "$PORT" =~ ^[0-9]+$ && "$PORT" -ge 1 && "$PORT" -le 65535 ]] || die "Invalid port: $PORT"
[[ -z "$VERSION" || "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Invalid version: $VERSION (expected X.Y.Z)"
[[ "$DB_NAME" =~ ^[A-Za-z0-9_]+$ ]] || die "Invalid database name: $DB_NAME"
[[ "$DB_USER" =~ ^[A-Za-z0-9_]+$ ]] || die "Invalid database user: $DB_USER"
[[ "$SERVICE_USER" =~ ^[a-z_][a-z0-9_-]*$ ]] || die "Invalid user name: $SERVICE_USER"
[[ "$REPO" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || die "Invalid repository: $REPO"
[[ -z "$ZIP_FILE" || -f "$ZIP_FILE" ]] || die "Zip file not found: $ZIP_FILE"
[[ "$INSTALL_DIR" == /* && "$INSTALL_DIR" != "/" ]] || die "Install directory must be an absolute path: $INSTALL_DIR"

NEW_DIR="${INSTALL_DIR}.new"
PREV_DIR="${INSTALL_DIR}.prev"
FAILED_DIR="${INSTALL_DIR}.failed"
CONFIG_FILE="${INSTALL_DIR}/appsettings.json"
UNIT_FILE="${UNIT_DIR}/${SERVICE_NAME}.service"
PID_FILE="${STATE_DIR}/${SERVICE_NAME}.pid"
LOG_FILE="${STATE_DIR}/${SERVICE_NAME}.log"

TMP_DIR="$(mktemp -d)"
cleanup() { rm -rf "$TMP_DIR"; }
trap cleanup EXIT

# ---------------------------------------------------------------------------
# Environment checks
# ---------------------------------------------------------------------------
[[ $EUID -eq 0 ]] || die "Please run as root (e.g. sudo bash install.sh)"
command -v apt-get >/dev/null || die "This installer supports Debian and Ubuntu (apt-get not found)"

OS_ID="unknown"; OS_VERSION=""
if [[ -r /etc/os-release ]]; then
  # Read in a subshell: os-release defines VERSION, which would clobber our own variable.
  # shellcheck disable=SC1091
  OS_ID="$(. /etc/os-release && echo "${ID:-unknown}")"
  # shellcheck disable=SC1091
  OS_VERSION="$(. /etc/os-release && echo "${VERSION_ID:-}")"
fi
case "$OS_ID:$OS_VERSION" in
  debian:12|debian:13|ubuntu:22.04|ubuntu:24.04) ;;
  *) warn "Untested distribution: ${OS_ID} ${OS_VERSION}. Supported: Debian 12/13, Ubuntu 22.04/24.04. Continuing anyway." ;;
esac

USE_SYSTEMD=1
if [[ "$SKIP_SYSTEMD" == "1" ]]; then
  USE_SYSTEMD=0
elif [[ ! -d /run/systemd/system ]] || ! command -v systemctl >/dev/null; then
  warn "systemd is not running - the service unit is written but the app is started in the background instead"
  USE_SYSTEMD=0
fi

export DEBIAN_FRONTEND=noninteractive
APT_UPDATED=0
apt_install() {
  if [[ $APT_UPDATED -eq 0 ]]; then
    info "Updating package lists"
    apt-get update -qq
    APT_UPDATED=1
  fi
  info "Installing packages: $*"
  apt-get install -y -qq --no-install-recommends "$@" >/dev/null
}
apt_has_candidate() {
  local candidate
  candidate="$(apt-cache policy "$1" 2>/dev/null | awk '/Candidate:/ {print $2}')"
  [[ -n "$candidate" && "$candidate" != "(none)" ]]
}

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------
install_prerequisites() {
  local missing=() cmd pkg
  for pair in curl:curl unzip:unzip jq:jq openssl:openssl setpriv:util-linux; do
    cmd="${pair%%:*}"; pkg="${pair#*:}"
    command -v "$cmd" >/dev/null || missing+=("$pkg")
  done
  [[ -e /etc/ssl/certs/ca-certificates.crt ]] || missing+=(ca-certificates)
  if [[ ${#missing[@]} -gt 0 ]]; then
    apt_install "${missing[@]}"
  fi
  ok "Prerequisites available"
}

# ---------------------------------------------------------------------------
# ASP.NET Core 10 runtime
# ---------------------------------------------------------------------------
DOTNET_BIN=""
has_aspnet10() { "$1" --list-runtimes 2>/dev/null | grep -q '^Microsoft\.AspNetCore\.App 10\.'; }
find_dotnet() {
  local c candidates=()
  [[ -n "${DOTNET_ROOT:-}" ]] && candidates+=("${DOTNET_ROOT}/dotnet")
  command -v dotnet >/dev/null && candidates+=("$(command -v dotnet)")
  candidates+=(/usr/bin/dotnet /usr/share/dotnet/dotnet /usr/lib/dotnet/dotnet /usr/local/dotnet/dotnet /usr/local/share/dotnet/dotnet)
  for c in "${candidates[@]}"; do
    if [[ -x "$c" ]] && has_aspnet10 "$c"; then
      DOTNET_BIN="$(readlink -f "$c")"
      return 0
    fi
  done
  return 1
}

install_dotnet_runtime() {
  if [[ -f "${DOTNET_INSTALL_DIR}/.installed-by-spoolhero" && $UPDATE -eq 1 ]]; then
    info "Refreshing the ASP.NET Core 10 runtime (security patches)"
    install_dotnet_via_script
  fi
  if find_dotnet; then
    ok "ASP.NET Core 10 runtime found: ${DOTNET_BIN}"
    return
  fi
  # Prefer the distribution package where one exists (newer Ubuntu releases ship .NET 10),
  # it is then kept up to date by apt. Otherwise use Microsoft's dotnet-install script,
  # which works the same way on every Debian and Ubuntu release.
  if [[ $APT_UPDATED -eq 0 ]]; then apt-get update -qq; APT_UPDATED=1; fi
  if apt_has_candidate aspnetcore-runtime-10.0; then
    apt_install aspnetcore-runtime-10.0
  fi
  if ! find_dotnet; then
    install_dotnet_via_script
  fi
  find_dotnet || die "ASP.NET Core 10 runtime installation failed"
  ok "ASP.NET Core 10 runtime installed: ${DOTNET_BIN}"
}

install_dotnet_via_script() {
  local deps=(zlib1g libstdc++6 libgcc-s1 tzdata) icu ssl
  icu="$(apt-cache pkgnames libicu 2>/dev/null | grep -E '^libicu[0-9]+$' | sort -V | tail -n1 || true)"
  [[ -n "$icu" ]] && deps+=("$icu")
  for ssl in libssl3t64 libssl3; do
    if apt_has_candidate "$ssl"; then deps+=("$ssl"); break; fi
  done
  apt_install "${deps[@]}"
  info "Installing the ASP.NET Core 10 runtime to ${DOTNET_INSTALL_DIR} via dotnet-install.sh"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${TMP_DIR}/dotnet-install.sh"
  bash "${TMP_DIR}/dotnet-install.sh" --channel 10.0 --runtime aspnetcore \
    --install-dir "$DOTNET_INSTALL_DIR" --no-path >/dev/null
  touch "${DOTNET_INSTALL_DIR}/.installed-by-spoolhero"
  if [[ ! -e /usr/bin/dotnet ]]; then
    ln -s "${DOTNET_INSTALL_DIR}/dotnet" /usr/bin/dotnet
  fi
}

# ---------------------------------------------------------------------------
# Database server
# ---------------------------------------------------------------------------
MYSQL_CLI=""; MYSQLDUMP_CLI=""
find_mysql_clients() {
  MYSQL_CLI="$(command -v mariadb || command -v mysql || true)"
  MYSQLDUMP_CLI="$(command -v mariadb-dump || command -v mysqldump || true)"
}
mysql_root() { "$MYSQL_CLI" --protocol=socket -uroot "$@"; }

ensure_database_server() {
  find_mysql_clients
  if [[ -n "$MYSQL_CLI" ]] && mysql_root -e 'SELECT 1' >/dev/null 2>&1; then
    ok "Database server is running"
    return
  fi
  if [[ -z "$MYSQL_CLI" ]] || { ! command -v mariadbd >/dev/null && ! command -v mysqld >/dev/null; }; then
    apt_install mariadb-server mariadb-client
    find_mysql_clients
  fi
  info "Starting MariaDB"
  if [[ $USE_SYSTEMD -eq 1 ]]; then
    systemctl enable --now mariadb >/dev/null 2>&1 || systemctl enable --now mysql >/dev/null 2>&1 || true
  else
    service mariadb start >/dev/null 2>&1 || service mysql start >/dev/null 2>&1 || true
  fi
  for _ in $(seq 1 30); do
    mysql_root -e 'SELECT 1' >/dev/null 2>&1 && { ok "MariaDB is running"; return; }
    sleep 1
  done
  die "Cannot connect to the database server as root via the local socket"
}

# Read a key from an ADO.NET style connection string (case-insensitive key match).
cs_get() {
  local cs="$1" key="$2" part k
  local IFS=';'
  for part in $cs; do
    k="${part%%=*}"; k="${k#"${k%%[![:space:]]*}"}"; k="${k%"${k##*[![:space:]]}"}"
    if [[ "${k,,}" == "${key,,}" ]]; then
      printf '%s' "${part#*=}"
      return 0
    fi
  done
  return 0
}

ensure_database() {
  local cs="$1" server db user pass host
  server="$(cs_get "$cs" Server)"; [[ -n "$server" ]] || server="$(cs_get "$cs" Host)"
  db="$(cs_get "$cs" Database)"; user="$(cs_get "$cs" User)"; [[ -n "$user" ]] || user="$(cs_get "$cs" Uid)"
  pass="$(cs_get "$cs" Password)"; [[ -n "$pass" ]] || pass="$(cs_get "$cs" Pwd)"
  case "$server" in
    localhost|127.0.0.1|::1|/*) ;;
    *) info "Database server '${server}' is not local - skipping database/user setup"; return ;;
  esac
  [[ "$db" =~ ^[A-Za-z0-9_]+$ && "$user" =~ ^[A-Za-z0-9_]+$ ]] || die "Unsupported database or user name in connection string"
  if [[ "$user" == "root" ]]; then
    warn "The connection string uses the database root user - skipping user setup"
    mysql_root -e "CREATE DATABASE IF NOT EXISTS \`${db}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
    return
  fi
  local esc_pass="${pass//\\/\\\\}"; esc_pass="${esc_pass//\'/\\\'}"
  local sql="CREATE DATABASE IF NOT EXISTS \`${db}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
  for host in localhost 127.0.0.1; do
    sql+="CREATE USER IF NOT EXISTS '${user}'@'${host}' IDENTIFIED BY '${esc_pass}';"
    sql+="ALTER USER '${user}'@'${host}' IDENTIFIED BY '${esc_pass}';"
    sql+="GRANT ALL PRIVILEGES ON \`${db}\`.* TO '${user}'@'${host}';"
  done
  sql+="FLUSH PRIVILEGES;"
  mysql_root -e "$sql"
  ok "Database '${db}' and user '${user}' are ready"
}

backup_database() {
  local cs="$1" server port db user pass file
  server="$(cs_get "$cs" Server)"; [[ -n "$server" ]] || server="$(cs_get "$cs" Host)"
  port="$(cs_get "$cs" Port)"; db="$(cs_get "$cs" Database)"
  user="$(cs_get "$cs" User)"; [[ -n "$user" ]] || user="$(cs_get "$cs" Uid)"
  pass="$(cs_get "$cs" Password)"; [[ -n "$pass" ]] || pass="$(cs_get "$cs" Pwd)"
  [[ -n "$MYSQLDUMP_CLI" ]] || die "mysqldump/mariadb-dump not found - cannot back up the database"
  mkdir -p "$BACKUP_DIR"; chmod 700 "$BACKUP_DIR"
  file="${BACKUP_DIR}/${db}-$(installed_version)-$(date +%Y%m%d-%H%M%S).sql.gz"
  info "Backing up database '${db}' to ${file}"
  local args=(--single-transaction --no-tablespaces --routines -u "$user")
  if [[ "$server" == /* ]]; then args+=(--protocol=socket -S "$server"); else args+=(-h "$server" -P "${port:-3306}"); fi
  MYSQL_PWD="$pass" "$MYSQLDUMP_CLI" "${args[@]}" "$db" | gzip > "$file"
  chmod 600 "$file"
  DB_BACKUP_FILE="$file"
  ok "Database backup written"
}

# ---------------------------------------------------------------------------
# Release download
# ---------------------------------------------------------------------------
installed_version() {
  if [[ -f "${INSTALL_DIR}/${VERSION_FILE_NAME}" ]]; then cat "${INSTALL_DIR}/${VERSION_FILE_NAME}"; else echo "unknown"; fi
}

resolve_version() {
  if [[ -n "$ZIP_FILE" ]]; then
    if [[ -z "$VERSION" ]]; then
      VERSION="$(basename "$ZIP_FILE" | sed -nE 's/^spoolhero-([0-9]+\.[0-9]+\.[0-9]+)\.zip$/\1/p')"
      [[ -n "$VERSION" ]] || VERSION="local"
    fi
    return
  fi
  if [[ -z "$VERSION" ]]; then
    info "Looking up the latest release of ${REPO}"
    local url
    url="$(curl -fsSLI -o /dev/null -w '%{url_effective}' "https://github.com/${REPO}/releases/latest")" \
      || die "Cannot reach GitHub to determine the latest release"
    VERSION="${url##*/}"; VERSION="${VERSION#v}"
    [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Could not determine the latest release (got '${url}')"
  fi
  ok "Target version: ${VERSION}"
}

stage_release() {
  local zip="$ZIP_FILE"
  if [[ -z "$zip" ]]; then
    zip="${TMP_DIR}/spoolhero-${VERSION}.zip"
    local url="https://github.com/${REPO}/releases/download/v${VERSION}/spoolhero-${VERSION}.zip"
    info "Downloading ${url}"
    local progress=(-sS)
    [[ -t 2 ]] && progress=(--progress-bar)
    curl -fL --retry 3 "${progress[@]}" -o "$zip" "$url" || die "Download failed: ${url}"
  fi
  info "Unpacking release"
  rm -rf "$NEW_DIR"
  mkdir -p "$NEW_DIR"
  unzip -q "$zip" -d "$NEW_DIR"
  [[ -f "${NEW_DIR}/${APP_DLL}" ]] || die "The release archive does not contain ${APP_DLL}"
  echo "$VERSION" > "${NEW_DIR}/${VERSION_FILE_NAME}"
}

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
SERVICE_GROUP=""
ensure_service_user() {
  if ! id "$SERVICE_USER" >/dev/null 2>&1; then
    info "Creating system user ${SERVICE_USER}"
    useradd --system --home-dir "$STATE_DIR" --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
  fi
  SERVICE_GROUP="$(id -gn "$SERVICE_USER")"
  mkdir -p "$STATE_DIR"
  chown "$SERVICE_USER:$SERVICE_GROUP" "$STATE_DIR"
  chmod 750 "$STATE_DIR"
}

config_is_configured() {
  local file="$1" secret cs
  [[ -f "$file" ]] || return 1
  secret="$(jq -r '.Jwt.SecretKey // ""' "$file" 2>/dev/null)" || return 1
  cs="$(jq -r '.ConnectionStrings.Default // ""' "$file" 2>/dev/null)" || return 1
  [[ ${#secret} -ge 32 && "$secret" != "$PLACEHOLDER_SECRET" && -n "$(cs_get "$cs" Password)" ]]
}

config_port() {
  local url
  url="$(jq -r '.Kestrel.Endpoints.Http.Url // ""' "$1" 2>/dev/null || true)"
  if [[ "$url" =~ :([0-9]+)/?$ ]]; then echo "${BASH_REMATCH[1]}"; else echo "$DEFAULT_PORT"; fi
}

write_new_config() {
  local file="$1" db_pass jwt
  db_pass="$(openssl rand -hex 20)"
  jwt="$(openssl rand -hex 48)"
  jq -n \
    --arg cs "Server=localhost;Port=3306;Database=${DB_NAME};User=${DB_USER};Password=${db_pass};" \
    --arg url "http://0.0.0.0:${PORT:-$DEFAULT_PORT}" \
    --arg jwt "$jwt" \
    '{
      ConnectionStrings: { Default: $cs },
      Kestrel: { Endpoints: { Http: { Url: $url } } },
      Jwt: { SecretKey: $jwt, Issuer: "SpoolHero", Audience: "SpoolHero", ExpiresInHours: 24 },
      Logging: { LogLevel: { Default: "Warning", "Microsoft.AspNetCore": "Warning" } },
      AllowedHosts: "*"
    }' > "$file"
  ok "Generated appsettings.json with a random database password and JWT secret"
}

# Sets the Kestrel port of an existing config. Returns 0 if the file was changed.
apply_port() {
  local file="$1" current
  [[ -n "$PORT" ]] || return 1
  current="$(config_port "$file")"
  [[ "$current" != "$PORT" ]] || return 1
  jq --arg url "http://0.0.0.0:${PORT}" '.Kestrel.Endpoints.Http.Url = $url' "$file" > "${file}.tmp"
  cat "${file}.tmp" > "$file"; rm -f "${file}.tmp"
  ok "Port changed from ${current} to ${PORT}"
  return 0
}

secure_app_dir() {
  local dir="$1"
  chown -R "root:${SERVICE_GROUP}" "$dir"
  chmod -R u=rwX,g=rX,o=rX "$dir"
  chmod 640 "${dir}/appsettings.json"
}

# ---------------------------------------------------------------------------
# Service management
# ---------------------------------------------------------------------------
write_unit() {
  mkdir -p "$UNIT_DIR"
  cat > "${UNIT_FILE}.tmp" <<EOF
[Unit]
Description=SpoolHero filament spool manager
Wants=network-online.target
After=network-online.target mariadb.service mysql.service

[Service]
Type=simple
WorkingDirectory=${INSTALL_DIR}
ExecStart=${DOTNET_BIN} ${INSTALL_DIR}/${APP_DLL}
Restart=always
RestartSec=10
User=${SERVICE_USER}
Group=${SERVICE_GROUP}
Environment=HOME=${STATE_DIR}
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
NoNewPrivileges=true
SyslogIdentifier=${SERVICE_NAME}

[Install]
WantedBy=multi-user.target
EOF
  if [[ -f "$UNIT_FILE" ]] && cmp -s "$UNIT_FILE" "${UNIT_FILE}.tmp"; then
    rm -f "${UNIT_FILE}.tmp"
  else
    mv "${UNIT_FILE}.tmp" "$UNIT_FILE"
    ok "Wrote ${UNIT_FILE}"
    [[ $USE_SYSTEMD -eq 1 ]] && systemctl daemon-reload
  fi
  if [[ $USE_SYSTEMD -eq 1 ]]; then
    systemctl enable "$SERVICE_NAME" >/dev/null 2>&1
  fi
  return 0
}

bg_pid() {
  local pid
  [[ -f "$PID_FILE" ]] || return 1
  pid="$(cat "$PID_FILE")"
  [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null && echo "$pid"
}

service_running() {
  if [[ $USE_SYSTEMD -eq 1 ]]; then systemctl is-active --quiet "$SERVICE_NAME"; else bg_pid >/dev/null; fi
}

service_start() {
  info "Starting ${SERVICE_NAME}"
  if [[ $USE_SYSTEMD -eq 1 ]]; then
    systemctl start "$SERVICE_NAME"
  else
    bg_pid >/dev/null && return 0
    (
      cd "$INSTALL_DIR"
      HOME="$STATE_DIR" ASPNETCORE_ENVIRONMENT=Production DOTNET_PRINT_TELEMETRY_MESSAGE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 \
        nohup setpriv --reuid="$SERVICE_USER" --regid="$SERVICE_GROUP" --init-groups \
        "$DOTNET_BIN" "${INSTALL_DIR}/${APP_DLL}" >> "$LOG_FILE" 2>&1 < /dev/null &
      echo $! > "$PID_FILE"
    )
  fi
}

service_stop() {
  if [[ $USE_SYSTEMD -eq 1 ]]; then
    systemctl stop "$SERVICE_NAME" 2>/dev/null || true
  else
    local pid
    pid="$(bg_pid)" || { rm -f "$PID_FILE"; return 0; }
    info "Stopping ${SERVICE_NAME} (pid ${pid})"
    kill "$pid" 2>/dev/null || true
    for _ in $(seq 1 30); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done
    kill -9 "$pid" 2>/dev/null || true
    rm -f "$PID_FILE"
  fi
}

service_logs() {
  if [[ $USE_SYSTEMD -eq 1 ]]; then
    journalctl -u "$SERVICE_NAME" -n 40 --no-pager 2>/dev/null || true
  else
    tail -n 40 "$LOG_FILE" 2>/dev/null || true
  fi
}

ensure_port_free() {
  local port="$1"
  command -v ss >/dev/null || return 0
  if ss -Hltn "sport = :${port}" 2>/dev/null | grep -q .; then
    die "Port ${port} is already in use. Choose another one with --port."
  fi
}

wait_healthy() {
  local port="$1" url waited=0
  url="http://127.0.0.1:${port}/api/public/branding"
  info "Waiting for SpoolHero to answer on ${url} (first start runs database migrations)"
  while (( waited < HEALTH_TIMEOUT )); do
    if curl -fsS -o /dev/null --max-time 5 "$url" 2>/dev/null; then
      ok "SpoolHero is up (after ${waited}s)"
      return 0
    fi
    if [[ $USE_SYSTEMD -eq 0 ]] && ! bg_pid >/dev/null; then
      warn "The SpoolHero process exited"
      return 1
    fi
    sleep 2; waited=$((waited + 2))
  done
  warn "SpoolHero did not become healthy within ${HEALTH_TIMEOUT}s"
  return 1
}

# ---------------------------------------------------------------------------
# Install / update flows
# ---------------------------------------------------------------------------
FRESH_INSTALL=0
DB_BACKUP_FILE=""
UPDATE_HELPER=""

fresh_install() {
  FRESH_INSTALL=1
  resolve_version
  ensure_port_free "${PORT:-$DEFAULT_PORT}"
  stage_release
  write_new_config "${NEW_DIR}/appsettings.json"
  ensure_database "$(jq -r '.ConnectionStrings.Default' "${NEW_DIR}/appsettings.json")"
  secure_app_dir "$NEW_DIR"
  if [[ -d "$INSTALL_DIR" ]]; then
    # Leftover directory without a working installation: keep it out of the way.
    rm -rf "$FAILED_DIR"; mv "$INSTALL_DIR" "$FAILED_DIR"
    warn "Moved the incomplete previous directory to ${FAILED_DIR}"
  fi
  mv "$NEW_DIR" "$INSTALL_DIR"
  write_unit
  service_start
  if ! wait_healthy "$(config_port "$CONFIG_FILE")"; then
    service_logs
    die "SpoolHero failed to start. Check the log above and run the installer again."
  fi
}

repair_install() {
  local changed=0 cs
  info "SpoolHero $(installed_version) is already installed in ${INSTALL_DIR} (use --update to update)"
  if ! config_is_configured "$CONFIG_FILE"; then
    die "${CONFIG_FILE} is missing or incomplete (placeholder JWT secret or no database password). Fix it or move ${INSTALL_DIR} away for a fresh install."
  fi
  apply_port "$CONFIG_FILE" && changed=1
  cs="$(jq -r '.ConnectionStrings.Default' "$CONFIG_FILE")"
  ensure_database "$cs"
  secure_app_dir "$INSTALL_DIR"
  write_unit
  if [[ $changed -eq 1 ]] && service_running; then service_stop; fi
  if ! service_running; then service_start; fi
  wait_healthy "$(config_port "$CONFIG_FILE")" || { service_logs; die "SpoolHero is not healthy"; }
}

update_install() {
  local current cs port
  current="$(installed_version)"
  config_is_configured "$CONFIG_FILE" || die "${CONFIG_FILE} is missing or incomplete - cannot update safely"
  resolve_version
  if [[ "$current" == "$VERSION" && $FORCE -eq 0 ]]; then
    ok "SpoolHero ${current} is already the requested version (use --force to reinstall)"
    repair_install
    return
  fi
  info "Updating SpoolHero ${current} -> ${VERSION}"
  stage_release
  cp -p "$CONFIG_FILE" "${NEW_DIR}/appsettings.json"
  apply_port "${NEW_DIR}/appsettings.json" || true
  secure_app_dir "$NEW_DIR"
  cs="$(jq -r '.ConnectionStrings.Default' "$CONFIG_FILE")"
  ensure_database "$cs"
  backup_database "$cs"

  service_stop
  rm -rf "$PREV_DIR"
  mv "$INSTALL_DIR" "$PREV_DIR"
  mv "$NEW_DIR" "$INSTALL_DIR"
  write_unit
  service_start
  port="$(config_port "$CONFIG_FILE")"
  if wait_healthy "$port"; then
    ok "Updated to ${VERSION}. The previous version is kept in ${PREV_DIR}"
    return
  fi

  service_logs
  warn "Update failed - rolling back to ${current}"
  service_stop
  rm -rf "$FAILED_DIR"
  mv "$INSTALL_DIR" "$FAILED_DIR"
  mv "$PREV_DIR" "$INSTALL_DIR"
  service_start
  if wait_healthy "$(config_port "$CONFIG_FILE")"; then
    warn "Rolled back to ${current}. The failed release is in ${FAILED_DIR}."
  else
    warn "The previous version does not start either."
  fi
  warn "If the database was already migrated, restore the backup with:"
  warn "  zcat ${DB_BACKUP_FILE} | mysql $(cs_get "$cs" Database)"
  exit 1
}

# Small helper so updates are a single command: spoolhero-update [--version X.Y.Z] [--force]
write_update_helper() {
  local helper="${BIN_DIR}/${SERVICE_NAME}-update"
  mkdir -p "$BIN_DIR"
  cat > "$helper" <<EOF
#!/usr/bin/env bash
# Updates SpoolHero using the latest installer from GitHub. Generated by install.sh.
set -euo pipefail
export SPOOLHERO_INSTALL_DIR='${INSTALL_DIR}' SPOOLHERO_USER='${SERVICE_USER}' SPOOLHERO_SERVICE_NAME='${SERVICE_NAME}'
export SPOOLHERO_REPO='${REPO}' SPOOLHERO_UNIT_DIR='${UNIT_DIR}' SPOOLHERO_BACKUP_DIR='${BACKUP_DIR}'
export SPOOLHERO_STATE_DIR='${STATE_DIR}' SPOOLHERO_BIN_DIR='${BIN_DIR}'
installer="\$(curl -fsSL https://raw.githubusercontent.com/${REPO}/main/deploy/install.sh)"
exec bash -c "\$installer" spoolhero-install --update "\$@"
EOF
  chmod 755 "$helper"
  UPDATE_HELPER="$helper"
}

print_summary() {
  local port ip
  port="$(config_port "$CONFIG_FILE")"
  ip="$(hostname -I 2>/dev/null | awk '{print $1}')"
  [[ -n "$ip" ]] || ip="$(hostname)"
  echo
  printf '%s%s%s\n' "$C_BOLD" "SpoolHero $(installed_version) is running" "$C_RESET"
  echo "  URL:            http://${ip}:${port}"
  if [[ $FRESH_INSTALL -eq 1 ]]; then
    echo "  Login:          admin@localhost / admin  (change the password right after the first login!)"
  fi
  echo "  Install dir:    ${INSTALL_DIR}"
  echo "  Configuration:  ${CONFIG_FILE}"
  if [[ $USE_SYSTEMD -eq 1 ]]; then
    echo "  Service:        systemctl status ${SERVICE_NAME}   |   journalctl -u ${SERVICE_NAME} -f"
  else
    echo "  Process:        pid $(bg_pid || echo '?'), log ${LOG_FILE} (systemd not used)"
  fi
  [[ -n "$DB_BACKUP_FILE" ]] && echo "  DB backup:      ${DB_BACKUP_FILE}"
  echo "  Update later:   ${UPDATE_HELPER}   (or: install.sh --update)"
  echo
  echo "Note: NFC tag reading/writing in the browser (Web NFC) requires HTTPS. Put a reverse proxy"
  echo "with a TLS certificate (e.g. Caddy, nginx, Nginx Proxy Manager, Traefik) in front of"
  echo "http://${ip}:${port} - see the README section 'HTTPS (required for NFC on Android)'."
}

main() {
  info "SpoolHero installer (${OS_ID} ${OS_VERSION}, install dir ${INSTALL_DIR})"
  install_prerequisites
  install_dotnet_runtime
  ensure_database_server
  ensure_service_user

  if [[ -f "${INSTALL_DIR}/${APP_DLL}" ]]; then
    if [[ $UPDATE -eq 1 ]]; then update_install; else repair_install; fi
  else
    [[ $UPDATE -eq 0 ]] || warn "No existing installation found in ${INSTALL_DIR} - performing a fresh install"
    fresh_install
  fi
  write_update_helper
  print_summary
}

main
