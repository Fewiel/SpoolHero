# SpoolHero

SpoolHero is a self-hosted filament management tool for 3D printing. It allows you to track spools, materials, storage locations, and drying cycles across multiple printers and projects. NFC tags (OpenSpool standard) can be written and read directly from the browser on Android.

## Features

- Spool tracking with weight, color, material, and location
- NFC tag support via OpenSpool standard (NTAG215/216)
- Multi-user projects with invitation codes
- Shared material database with import/export
- Dryer monitoring with notifications
- Inventory scanning via NFC
- Klipper/Moonraker integration through a Spoolman-compatible API
- Admin panel with branding, SMTP, user management, and support tickets
- Email notifications for spool levels, dryer cycles, and ticket replies
- Self-hostable, open source, no external dependencies

## Quick Start with Docker

The easiest way to run SpoolHero is with Docker Compose:

```bash
git clone https://github.com/Fewiel/SpoolHero.git
cd SpoolHero
docker compose up -d
```

SpoolHero will be available at `http://localhost:5000`. A MySQL 8 database is included and configured automatically. The database port is not exposed to the host by default.

To update a Docker installation:

```bash
git pull
docker compose up -d --build
```

**Default admin account:**
- Email: `admin@localhost`
- Password: `admin`

Change the password immediately after first login.

### Configuration

Set environment variables in a `.env` file next to `docker-compose.yml`:

```env
MYSQL_ROOT_PASSWORD=YourSecureDatabasePassword
JWT_SECRET_KEY=YourRandomStringAtLeast32Characters
```

### HTTPS (required for NFC on Android)

SpoolHero supports NFC tag reading and writing via the Web NFC API. **Web NFC only works over HTTPS** (except on localhost). If you want to use NFC features from your phone, you must put a reverse proxy with SSL in front of SpoolHero.

Example with **nginx** and Let's Encrypt:

```nginx
server {
    listen 443 ssl;
    server_name spoolhero.example.com;

    ssl_certificate /etc/letsencrypt/live/spoolhero.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/spoolhero.example.com/privkey.pem;

    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Alternatively, you can use [Caddy](https://caddyserver.com/) which handles SSL certificates automatically:

```
spoolhero.example.com {
    reverse_proxy localhost:5000
}
```

## Proxmox (LXC)

On a [Proxmox VE](https://www.proxmox.com/) host, a single command creates an unprivileged Debian 12 container and installs SpoolHero in it. Run it in the shell of your Proxmox node (as root):

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/Fewiel/SpoolHero/main/deploy/proxmox-create-lxc.sh)"
```

Defaults: next free container ID, hostname `spoolhero`, 1 core, 1024 MB RAM, 8 GB disk, bridge `vmbr0` with DHCP, port 5000. Everything can be changed with options, for example:

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/Fewiel/SpoolHero/main/deploy/proxmox-create-lxc.sh)" _ \
  --ctid 120 --memory 2048 --storage local-zfs --ip 192.168.1.50/24 --gateway 192.168.1.1 --yes
```

Run it with `--help` for all options. When it finishes, it prints the container's address (`http://<container-ip>:5000`). The default admin account is `admin@localhost` / `admin`.

To update later: `pct exec <ctid> -- spoolhero-update`

### Install script for Debian / Ubuntu (existing container or VM)

If you already have a container or VM running Debian 12/13 or Ubuntu 22.04/24.04, run the installer inside it as root:

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/Fewiel/SpoolHero/main/deploy/install.sh)"
```

It installs the ASP.NET Core 10 runtime, MariaDB, the latest release in `/opt/spoolhero` and a `spoolhero` systemd service. The database password and JWT secret are generated randomly. Running it again is safe: existing settings and secrets are kept.

- Update: `spoolhero-update` (or run the installer with `--update`). It backs up the database to `/var/backups/spoolhero`, keeps the previous version in `/opt/spoolhero.prev` and rolls back if the new version does not start.
- Options: `--version X.Y.Z`, `--port N`, `--zip <file>` (install from a downloaded release zip), `--help` for the full list.

For NFC, you still need HTTPS in front of the container, see [HTTPS (required for NFC on Android)](#https-required-for-nfc-on-android).

## Manual Setup (without Docker)

### Requirements

- .NET 10.0 Runtime
- MySQL 8.0 or MariaDB 10.6+
- A reverse proxy (Apache or nginx recommended)

### Installation

Download the latest release zip from the [Releases](https://github.com/Fewiel/SpoolHero/releases) page and extract it to your server. Then create an `appsettings.json` based on the included `appsettings.example.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=SpoolManager;User=spoolmanager;Password=YOUR_DB_PASSWORD;"
  },
  "Jwt": {
    "SecretKey": "at-least-32-characters-random-string"
  }
}
```

The database schema is created automatically on first start via FluentMigrator.

A default admin account is created on first start:
- Email: `admin@localhost`
- Password: `admin`

Change the password immediately after first login.

For a full installation guide including Apache reverse proxy and Let's Encrypt, see [INSTALL.md](INSTALL.md).

## Contributing

Pull requests are welcome. Please keep changes focused and describe what you changed and why.

For larger features, open an issue first to discuss the approach.

## Klipper / Moonraker (Spoolman API)

SpoolHero exposes a Spoolman-compatible API, so Moonraker/Klipper and other Spoolman clients (e.g. nfc2klipper, SpoolPainter) can use it in place of a Spoolman server. Used filament is booked automatically and updates the spool's remaining weight.

1. Open **Projects → Project Settings → Klipper / Moonraker** and create an API key (project admins and managers only).
2. Copy the server URL shown there into your `moonraker.conf`:

```ini
[spoolman]
server: https://spoolhero.example.com/<API_KEY>
sync_rate: 5
```

3. Restart Moonraker and select a spool with `SET_ACTIVE_SPOOL ID=<id>`. The ID is the "Spoolman ID" shown on the spool detail page; NFC tags written by SpoolHero also contain it (`SPOOL:<id>` text record for nfc2klipper).

The API key is part of the URL, so treat the URL like a password. If you use a reverse proxy, make sure it forwards WebSocket connections (nginx: `proxy_http_version 1.1; proxy_set_header Upgrade $http_upgrade; proxy_set_header Connection "upgrade";`).

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

Free to use, modify, and distribute. Any modifications — including running a modified version as a hosted service — must be made available under the same license. See [LICENSE](LICENSE) for details.
