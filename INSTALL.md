# Installation Guide – Ubuntu 24.04 with Apache and Let's Encrypt

## Requirements

- Ubuntu 24.04 LTS
- A domain pointing to your server
- Root or sudo access

---

## 1. Install .NET 8 Runtime

```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
```

Verify:
```bash
dotnet --version
```

---

## 2. Install MySQL

```bash
sudo apt install -y mysql-server
sudo mysql_secure_installation
```

Create a database and user:
```sql
sudo mysql

CREATE DATABASE SpoolManager CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'spoolmanager'@'localhost' IDENTIFIED BY 'YOUR_DB_PASSWORD';
GRANT ALL PRIVILEGES ON SpoolManager.* TO 'spoolmanager'@'localhost';
FLUSH PRIVILEGES;
EXIT;
```

---

## 3. Deploy SpoolHero

Download the latest release zip from https://github.com/Fewiel/SpoolHero/releases and extract it:

```bash
sudo mkdir -p /opt/spoolhero
wget https://github.com/Fewiel/SpoolHero/releases/latest/download/spoolhero-VERSION.zip -O /tmp/spoolhero.zip
sudo unzip /tmp/spoolhero.zip -d /opt/spoolhero
rm /tmp/spoolhero.zip
```

Replace `spoolhero-VERSION.zip` with the actual filename from the releases page.

---

## 4. Configure appsettings.json

On the server, create the configuration file:
```bash
nano /opt/spoolhero/appsettings.json
```

Minimum configuration:
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=SpoolManager;User=spoolmanager;Password=YOUR_DB_PASSWORD;"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://127.0.0.1:5000"
      }
    }
  },
  "Jwt": {
    "SecretKey": "replace-with-a-random-string-of-at-least-32-characters",
    "Issuer": "SpoolHero",
    "Audience": "SpoolHero",
    "ExpiresInHours": 24
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Note: When running behind a reverse proxy, HTTPS is handled by Apache, so Kestrel only needs to listen on HTTP locally.

---

## 5. Create a systemd Service

```bash
sudo nano /etc/systemd/system/spoolhero.service
```

```ini
[Unit]
Description=SpoolHero
After=network.target mysql.service

[Service]
WorkingDirectory=/opt/spoolhero
ExecStart=/usr/bin/dotnet /opt/spoolhero/SpoolManager.Server.dll
Restart=always
RestartSec=10
User=www-data
Group=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Set permissions and start the service:
```bash
sudo chown -R www-data:www-data /opt/spoolhero
sudo systemctl daemon-reload
sudo systemctl enable spoolhero
sudo systemctl start spoolhero
sudo systemctl status spoolhero
```

---

## 6. Install Apache

```bash
sudo apt install -y apache2
sudo a2enmod proxy proxy_http proxy_wstunnel rewrite headers ssl
sudo systemctl restart apache2
```

---

## 7. Configure Apache Virtual Host

```bash
sudo nano /etc/apache2/sites-available/spoolhero.conf
```

```apache
<VirtualHost *:80>
    ServerName yourdomain.com

    ProxyPreserveHost On
    ProxyPass / http://127.0.0.1:5000/
    ProxyPassReverse / http://127.0.0.1:5000/

    RewriteEngine On
    RewriteCond %{HTTP:Upgrade} websocket [NC]
    RewriteCond %{HTTP:Connection} upgrade [NC]
    RewriteRule ^/?(.*) ws://127.0.0.1:5000/$1 [P,L]
</VirtualHost>
```

Enable the site:
```bash
sudo a2ensite spoolhero.conf
sudo a2dissite 000-default.conf
sudo systemctl reload apache2
```

---

## 8. Set Up Let's Encrypt (HTTPS)

```bash
sudo apt install -y certbot python3-certbot-apache
sudo certbot --apache -d yourdomain.com
```

Certbot will automatically modify the Apache configuration and set up certificate renewal.

Test automatic renewal:
```bash
sudo certbot renew --dry-run
```

---

## 9. First Login

Open your browser and navigate to `https://yourdomain.com`.

Default credentials:
- Email: `admin@localhost`
- Password: `admin`

Change the password immediately under Admin > Users.

---

## Updating

To update to a newer version:

1. Build and publish the new version on your development machine
2. Stop the service: `sudo systemctl stop spoolhero`
3. Copy the new files to `/opt/spoolhero` (overwrite existing files)
4. Set permissions: `sudo chown -R www-data:www-data /opt/spoolhero`
5. Start the service: `sudo systemctl start spoolhero`

Database migrations run automatically on startup.
