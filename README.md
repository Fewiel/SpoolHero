# SpoolHero

SpoolHero is a self-hosted filament management tool for 3D printing. It allows you to track spools, materials, storage locations, and drying cycles across multiple printers and projects. NFC tags (OpenSpool standard) can be written and read directly from the browser on Android.

## Features

- Spool tracking with weight, color, material, and location
- NFC tag support via OpenSpool standard (NTAG215/216)
- Multi-user projects with invitation codes
- Shared material database with import/export
- Dryer monitoring with notifications
- Inventory scanning via NFC
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

SpoolHero will be available at `http://localhost:5000`. A MySQL 8 database is included and configured automatically.

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

## Roadmap

- Klipper integration (read/write spool data directly from Klipper via Moonraker API)

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

Free to use, modify, and distribute. Any modifications — including running a modified version as a hosted service — must be made available under the same license. See [LICENSE](LICENSE) for details.
