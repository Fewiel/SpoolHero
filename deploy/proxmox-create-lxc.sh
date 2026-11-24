#!/usr/bin/env bash
# Creates a Proxmox VE LXC container and installs SpoolHero in it.
#
# Run on the Proxmox VE host (shell of the node), as root:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/Fewiel/SpoolHero/main/deploy/proxmox-create-lxc.sh)"
#
# With options (see --help):
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/Fewiel/SpoolHero/main/deploy/proxmox-create-lxc.sh)" _ --ctid 120 --memory 2048 --yes
#
# The container is an unprivileged Debian 12 LXC. Inside it, deploy/install.sh installs
# the ASP.NET Core runtime, MariaDB and SpoolHero as a systemd service.

set -Eeuo pipefail

REPO="${SPOOLHERO_REPO:-Fewiel/SpoolHero}"
BRANCH="${SPOOLHERO_BRANCH:-main}"
CTID="${CTID:-}"
CT_HOSTNAME="${CT_HOSTNAME:-spoolhero}"
CORES="${CT_CORES:-1}"
MEMORY="${CT_MEMORY:-1024}"
SWAP="${CT_SWAP:-512}"
DISK="${CT_DISK:-8}"
STORAGE="${CT_STORAGE:-}"
TEMPLATE_STORAGE="${CT_TEMPLATE_STORAGE:-}"
TEMPLATE="${CT_TEMPLATE:-}"
BRIDGE="${CT_BRIDGE:-vmbr0}"
IP="${CT_IP:-dhcp}"
GATEWAY="${CT_GATEWAY:-}"
VLAN="${CT_VLAN:-}"
CT_PASSWORD="${CT_PASSWORD:-}"
SSH_KEY_FILE="${CT_SSH_KEY_FILE:-}"
APP_VERSION="${SPOOLHERO_VERSION:-}"
APP_PORT="${SPOOLHERO_PORT:-5000}"
INSTALLER="${SPOOLHERO_INSTALLER:-}"
UPGRADE=1
ASSUME_YES=0

if [[ -t 1 ]]; then
  C_RESET=$'\e[0m'; C_BLUE=$'\e[34m'; C_GREEN=$'\e[32m'; C_YELLOW=$'\e[33m'; C_RED=$'\e[31m'; C_BOLD=$'\e[1m'
else
  C_RESET=""; C_BLUE=""; C_GREEN=""; C_YELLOW=""; C_RED=""; C_BOLD=""
fi
info() { printf '%s[INFO]%s %s\n' "$C_BLUE" "$C_RESET" "$*"; }
ok()   { printf '%s[ OK ]%s %s\n' "$C_GREEN" "$C_RESET" "$*"; }
warn() { printf '%s[WARN]%s %s\n' "$C_YELLOW" "$C_RESET" "$*" >&2; }
die()  { printf '%s[FAIL]%s %s\n' "$C_RED" "$C_RESET" "$*" >&2; exit 1; }

usage() {
  cat <<EOF
Create a Proxmox VE LXC container (unprivileged Debian 12) and install SpoolHero in it.
Run on the Proxmox VE host as root.

Usage: proxmox-create-lxc.sh [options]

Container options:
  --ctid ID              Container ID (default: next free ID)
  --hostname NAME        Hostname (default: ${CT_HOSTNAME})
  --cores N              CPU cores (default: ${CORES})
  --memory MB            Memory in MB (default: ${MEMORY})
  --swap MB              Swap in MB (default: ${SWAP})
  --disk GB              Root disk size in GB (default: ${DISK})
  --storage NAME         Storage for the root disk (default: local-lvm, local-zfs or first suitable)
  --template-storage N   Storage for the container template (default: local or first suitable)
  --template FILE        Template file name (default: newest debian-12-standard)
  --bridge NAME          Network bridge (default: ${BRIDGE})
  --ip CIDR|dhcp         IPv4 address, e.g. 192.168.1.50/24 (default: dhcp)
  --gateway IP           IPv4 gateway (for a static IP)
  --vlan TAG             VLAN tag
  --password PW          Root password for the container console (default: none, use 'pct enter')
  --ssh-key FILE         Public SSH key(s) to install for root
  --no-upgrade           Skip upgrading the container's packages before installing

SpoolHero options:
  --version X.Y.Z        SpoolHero release to install (default: latest)
  --port N               HTTP port inside the container (default: ${APP_PORT})
  --installer PATH|URL   install.sh to use (default: next to this script, else from GitHub ${BRANCH})

  -y, --yes              Do not ask for confirmation
  -h, --help             Show this help
EOF
}

need_arg() { [[ $# -ge 2 && -n "$2" ]] || die "Option $1 requires a value"; }
while [[ $# -gt 0 ]]; do
  case "$1" in
    --ctid) need_arg "$@"; CTID="$2"; shift ;;
    --hostname) need_arg "$@"; CT_HOSTNAME="$2"; shift ;;
    --cores) need_arg "$@"; CORES="$2"; shift ;;
    --memory) need_arg "$@"; MEMORY="$2"; shift ;;
    --swap) need_arg "$@"; SWAP="$2"; shift ;;
    --disk) need_arg "$@"; DISK="$2"; shift ;;
    --storage) need_arg "$@"; STORAGE="$2"; shift ;;
    --template-storage) need_arg "$@"; TEMPLATE_STORAGE="$2"; shift ;;
    --template) need_arg "$@"; TEMPLATE="$2"; shift ;;
    --bridge) need_arg "$@"; BRIDGE="$2"; shift ;;
    --ip) need_arg "$@"; IP="$2"; shift ;;
    --gateway) need_arg "$@"; GATEWAY="$2"; shift ;;
    --vlan) need_arg "$@"; VLAN="$2"; shift ;;
    --password) need_arg "$@"; CT_PASSWORD="$2"; shift ;;
    --ssh-key) need_arg "$@"; SSH_KEY_FILE="$2"; shift ;;
    --no-upgrade) UPGRADE=0 ;;
    --version) need_arg "$@"; APP_VERSION="${2#v}"; shift ;;
    --port) need_arg "$@"; APP_PORT="$2"; shift ;;
    --installer) need_arg "$@"; INSTALLER="$2"; shift ;;
    -y|--yes) ASSUME_YES=1 ;;
    -h|--help) usage; exit 0 ;;
    *) usage >&2; die "Unknown option: $1" ;;
  esac
  shift
done

# ---------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------
# in_range VALUE MIN MAX: VALUE is an unsigned integer within [MIN, MAX]
in_range() { [[ "$1" =~ ^[0-9]{1,9}$ ]] && (( 10#$1 >= $2 && 10#$1 <= $3 )); }
in_range "$CORES" 1 512 || die "Invalid --cores: $CORES"
in_range "$MEMORY" 512 1048576 || die "Invalid --memory: $MEMORY (at least 512 MB)"
in_range "$SWAP" 0 1048576 || die "Invalid --swap: $SWAP"
in_range "$DISK" 4 65536 || die "Invalid --disk: $DISK (at least 4 GB)"
in_range "$APP_PORT" 1 65535 || die "Invalid --port: $APP_PORT"
[[ -z "$APP_VERSION" || "$APP_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Invalid --version: $APP_VERSION"
[[ "$CT_HOSTNAME" =~ ^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?$ ]] || die "Invalid --hostname: $CT_HOSTNAME"
[[ "$IP" == "dhcp" || "$IP" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+/[0-9]+$ ]] || die "Invalid --ip: $IP (use dhcp or CIDR like 192.168.1.50/24)"
[[ -z "$GATEWAY" || "$GATEWAY" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Invalid --gateway: $GATEWAY"
[[ -z "$VLAN" ]] || in_range "$VLAN" 1 4094 || die "Invalid --vlan: $VLAN"
[[ -z "$SSH_KEY_FILE" || -r "$SSH_KEY_FILE" ]] || die "SSH key file not readable: $SSH_KEY_FILE"
[[ $EUID -eq 0 ]] || die "Please run as root on the Proxmox VE host"
for cmd in pct pveam pvesh pvesm; do
  command -v "$cmd" >/dev/null || die "'${cmd}' not found - this script must run on a Proxmox VE host (not inside a container)"
done
command -v curl >/dev/null || die "curl is required"

[[ -d "/sys/class/net/${BRIDGE}" ]] || die "Bridge '${BRIDGE}' does not exist on this host (use --bridge)"

if [[ -z "$CTID" ]]; then
  CTID="$(pvesh get /cluster/nextid)"
fi
in_range "$CTID" 100 999999999 || die "Invalid container ID: $CTID"
if compgen -G "/etc/pve/nodes/*/lxc/${CTID}.conf" >/dev/null || compgen -G "/etc/pve/nodes/*/qemu-server/${CTID}.conf" >/dev/null; then
  die "ID ${CTID} is already used by a container or VM (use --ctid)"
fi

# Active storages that support a content type, one per line.
storages_for() { pvesm status --content "$1" 2>/dev/null | awk 'NR > 1 && $3 == "active" { print $1 }'; }
pick_storage() {
  local content="$1" preferred list s
  shift
  list="$(storages_for "$content")"
  [[ -n "$list" ]] || return 1
  for preferred in "$@"; do
    for s in $list; do [[ "$s" == "$preferred" ]] && { echo "$s"; return 0; }; done
  done
  echo "$list" | head -n1
}
if [[ -z "$STORAGE" ]]; then
  STORAGE="$(pick_storage rootdir local-lvm local-zfs local)" || die "No active storage for container disks found (use --storage)"
else
  storages_for rootdir | grep -qx "$STORAGE" || die "Storage '${STORAGE}' is not active or does not allow container disks"
fi
if [[ -z "$TEMPLATE_STORAGE" ]]; then
  TEMPLATE_STORAGE="$(pick_storage vztmpl local)" || die "No active storage for container templates found (use --template-storage)"
else
  storages_for vztmpl | grep -qx "$TEMPLATE_STORAGE" || die "Storage '${TEMPLATE_STORAGE}' is not active or does not allow container templates"
fi

# ---------------------------------------------------------------------------
# Template
# ---------------------------------------------------------------------------
TEMPLATE_PATTERN='^debian-12-standard_.*_amd64\.tar\.(zst|gz|xz)$'
local_templates() {
  # pveam list prints e.g. "local:vztmpl/debian-12-standard_12.7-1_amd64.tar.zst  123.45MB"
  pveam list "$TEMPLATE_STORAGE" 2>/dev/null | awk 'NR > 1 { sub(/^[^\/]*\//, "", $1); print $1 }'
}
local_template() { local_templates | grep -E "$1" | sort -V | tail -n1 || true; }
if [[ -n "$TEMPLATE" ]]; then
  if ! local_templates | grep -qxF "$TEMPLATE"; then
    info "Downloading template ${TEMPLATE} to ${TEMPLATE_STORAGE}"
    pveam update >/dev/null || warn "pveam update failed, trying the download anyway"
    pveam download "$TEMPLATE_STORAGE" "$TEMPLATE" || die "Template download failed"
  fi
else
  TEMPLATE="$(local_template "$TEMPLATE_PATTERN")"
  if [[ -z "$TEMPLATE" ]]; then
    info "Updating the template list"
    pveam update >/dev/null || warn "pveam update failed, using the cached template list"
    TEMPLATE="$(pveam available --section system | awk '{ print $2 }' | grep -E "$TEMPLATE_PATTERN" | sort -V | tail -n1 || true)"
    [[ -n "$TEMPLATE" ]] || die "No Debian 12 template found in 'pveam available' (use --template)"
    info "Downloading template ${TEMPLATE} to ${TEMPLATE_STORAGE}"
    pveam download "$TEMPLATE_STORAGE" "$TEMPLATE" || die "Template download failed"
  fi
fi
ok "Template: ${TEMPLATE_STORAGE}:vztmpl/${TEMPLATE}"

# ---------------------------------------------------------------------------
# Installer script
# ---------------------------------------------------------------------------
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT
INSTALLER_FILE="${TMP_DIR}/install.sh"
SCRIPT_PATH="${BASH_SOURCE[0]:-}"
if [[ -z "$INSTALLER" && -n "$SCRIPT_PATH" && -f "$SCRIPT_PATH" && -f "$(dirname "$SCRIPT_PATH")/install.sh" ]]; then
  INSTALLER="$(dirname "$SCRIPT_PATH")/install.sh"
fi
[[ -n "$INSTALLER" ]] || INSTALLER="https://raw.githubusercontent.com/${REPO}/${BRANCH}/deploy/install.sh"
if [[ "$INSTALLER" =~ ^https?:// ]]; then
  info "Fetching installer from ${INSTALLER}"
  curl -fsSL "$INSTALLER" -o "$INSTALLER_FILE" || die "Could not download ${INSTALLER}"
else
  [[ -f "$INSTALLER" ]] || die "Installer not found: ${INSTALLER}"
  cp "$INSTALLER" "$INSTALLER_FILE"
  info "Using local installer ${INSTALLER}"
fi
head -n1 "$INSTALLER_FILE" | grep -q '^#!.*bash' || die "The installer does not look like a bash script"

# ---------------------------------------------------------------------------
# Summary + confirmation
# ---------------------------------------------------------------------------
NET0="name=eth0,bridge=${BRIDGE},ip=${IP}"
[[ -n "$GATEWAY" ]] && NET0+=",gw=${GATEWAY}"
[[ -n "$VLAN" ]] && NET0+=",tag=${VLAN}"

echo
printf '%sSpoolHero LXC%s\n' "$C_BOLD" "$C_RESET"
echo "  Container ID:   ${CTID}"
echo "  Hostname:       ${CT_HOSTNAME}"
echo "  Resources:      ${CORES} core(s), ${MEMORY} MB RAM, ${SWAP} MB swap, ${DISK} GB disk on ${STORAGE}"
echo "  Network:        ${NET0}"
echo "  Template:       ${TEMPLATE}"
echo "  SpoolHero:      ${APP_VERSION:-latest release}, port ${APP_PORT}"
echo
if [[ $ASSUME_YES -eq 0 && -t 0 ]]; then
  answer=""
  read -r -p "Create the container? [Y/n] " answer || true
  [[ -z "$answer" || "$answer" =~ ^[Yy] ]] || die "Aborted"
fi

# ---------------------------------------------------------------------------
# Create + start
# ---------------------------------------------------------------------------
CREATE_ARGS=(
  --hostname "$CT_HOSTNAME"
  --cores "$CORES"
  --memory "$MEMORY"
  --swap "$SWAP"
  --rootfs "${STORAGE}:${DISK}"
  --net0 "$NET0"
  --ostype debian
  --unprivileged 1
  --features nesting=1
  --onboot 1
  --timezone host
  --tags spoolhero
  --description "SpoolHero - https://github.com/${REPO}"
)
[[ -n "$CT_PASSWORD" ]] && CREATE_ARGS+=(--password "$CT_PASSWORD")
[[ -n "$SSH_KEY_FILE" ]] && CREATE_ARGS+=(--ssh-public-keys "$SSH_KEY_FILE")

info "Creating container ${CTID}"
pct create "$CTID" "${TEMPLATE_STORAGE}:vztmpl/${TEMPLATE}" "${CREATE_ARGS[@]}" >/dev/null
ok "Container ${CTID} created"

fail_hint() {
  warn "Installation failed. The container ${CTID} was kept for troubleshooting:"
  warn "  pct enter ${CTID}            # open a shell inside the container"
  warn "  pct destroy ${CTID} --purge  # remove it (stop it first with 'pct stop ${CTID}')"
}
on_exit() {
  local rc=$?
  [[ $rc -eq 0 ]] || fail_hint
  rm -rf "$TMP_DIR"
}
trap on_exit EXIT

info "Starting container ${CTID}"
pct start "$CTID"

info "Waiting for the network"
CT_IP=""
for _ in $(seq 1 60); do
  CT_IP="$(pct exec "$CTID" -- sh -c "ip -4 -o addr show scope global 2>/dev/null | awk '{ split(\$4, a, \"/\"); print a[1]; exit }'" 2>/dev/null || true)"
  if [[ -n "$CT_IP" ]] && pct exec "$CTID" -- getent hosts deb.debian.org >/dev/null 2>&1; then
    break
  fi
  sleep 2
done
[[ -n "$CT_IP" ]] || die "The container did not get an IPv4 address (check bridge/DHCP or use --ip/--gateway)"
pct exec "$CTID" -- getent hosts deb.debian.org >/dev/null 2>&1 || die "The container has no working DNS/internet access"
ok "Container IP: ${CT_IP}"

CT_EXEC=(pct exec "$CTID" -- env LANG=C.UTF-8 LC_ALL=C.UTF-8 DEBIAN_FRONTEND=noninteractive)
if [[ $UPGRADE -eq 1 ]]; then
  info "Upgrading container packages"
  "${CT_EXEC[@]}" sh -c 'apt-get update -qq && apt-get -y -qq -o Dpkg::Options::=--force-confold dist-upgrade >/dev/null'
fi

info "Running the SpoolHero installer inside the container"
pct push "$CTID" "$INSTALLER_FILE" /root/spoolhero-install.sh --perms 0755
INSTALL_ARGS=(--port "$APP_PORT")
[[ -n "$APP_VERSION" ]] && INSTALL_ARGS+=(--version "$APP_VERSION")
"${CT_EXEC[@]}" bash /root/spoolhero-install.sh "${INSTALL_ARGS[@]}"

echo
printf '%sDone.%s SpoolHero runs in container %s (%s).\n' "$C_BOLD" "$C_RESET" "$CTID" "$CT_HOSTNAME"
echo "  Open:     http://${CT_IP}:${APP_PORT}"
echo "  Login:    admin@localhost / admin  (change the password right after the first login!)"
echo "  Shell:    pct enter ${CTID}"
echo "  Update:   pct exec ${CTID} -- spoolhero-update"
if [[ "$IP" == "dhcp" ]]; then
  echo "  Tip:      the IP comes from DHCP - reserve it in your router or use --ip for a static address."
fi
echo "  HTTPS:    needed for NFC in the browser - put a reverse proxy with a certificate in front of it."
