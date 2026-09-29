# 🚀 Deployment Guide — NewsWebApp (.NET 10 MVC)

This guide explains how to deploy **NewsWebApp** to modern hosting environments with ease.

---

## 📋 Table of Contents
1. [Prerequisites](#prerequisites)
2. [Option 1: Deploy with Docker & Docker Compose (Recommended)](#option-1-deploy-with-docker--docker-compose-recommended)
3. [Option 2: Deploy to Free Cloud Providers (Render / Railway)](#option-2-deploy-to-free-cloud-providers-render--railway)
4. [Option 3: Deploy to Azure App Service](#option-3-deploy-to-azure-app-service)
5. [Option 4: Deploy to Linux VPS (Ubuntu / Debian + Nginx)](#option-4-deploy-to-linux-vps-ubuntu--debian--nginx)
6. [Environment Variables Reference](#environment-variables-reference)

---

## 🛠️ Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for local testing/building)
- [Docker](https://www.docker.com/) (for containerized deployment)
- Git

---

## 🐳 Option 1: Deploy with Docker & Docker Compose (Recommended)

The easiest way to run the production application anywhere (local server, cloud VM, DigitalOcean Droplet, AWS EC2):

### 1. Clone & Build
```bash
git clone https://github.com/Akshit-22-06/NewsWebApp-.git
cd NewsWebApp-
```

### 2. Launch with Docker Compose
```bash
docker compose up --build -d
```

### 3. Verify
- Open your browser to `http://localhost:5235` (or `http://YOUR_SERVER_IP:5235`).
- The database is automatically created, seeded, and persistent in Docker volume `newsdata`.
- Default Admin Credentials:
  - **Email:** `admin@newswebapp.com`
  - **Password:** `Admin@123456`

### 4. Check Container Status & Logs
```bash
docker compose ps
docker compose logs -f newswebapp
```

### 5. Stop Container
```bash
docker compose down
```

---

## ☁️ Option 2: Deploy to Free Cloud Providers (Render / Railway)

### Deploying to Render (render.com):
1. Create a free account on [Render.com](https://render.com).
2. Click **New +** &rarr; **Web Service**.
3. Connect your GitHub repository: `https://github.com/Akshit-22-06/NewsWebApp-.git`.
4. Configure service settings:
   - **Name:** `news-webapp`
   - **Environment:** `Docker`
   - **Dockerfile Path:** `./NewsWebApp/Dockerfile`
   - **Docker Context:** `./NewsWebApp`
   - **Plan:** Free
5. In **Environment Variables**, add:
   - `ASPNETCORE_ENVIRONMENT`: `Production`
   - `DatabaseProvider`: `Sqlite`
   - `ConnectionStrings__DefaultConnection`: `Data Source=/app/data/newswebapp.db`
6. Click **Create Web Service**. Render builds and deploys your container automatically with a free HTTPS URL!

---

## ☁️ Option 3: Deploy to Azure App Service

### 1. Build and Publish Locally
```bash
cd NewsWebApp
dotnet publish -c Release -o ./publish
```

### 2. Deploy using Azure CLI
```bash
# Login to Azure
az login

# Create a Resource Group
az group create --name NewsWebAppRG --location eastus

# Create an App Service Plan (Linux)
az appservice plan create --name NewsWebAppPlan --resource-group NewsWebAppRG --sku B1 --is-linux

# Create the Web App with .NET 10 runtime
az webapp create --resource-group NewsWebAppRG --plan NewsWebAppPlan --name mynewswebapp --runtime "DOTNETCORE:10.0"

# Deploy published files
az webapp deploy --resource-group NewsWebAppRG --name mynewswebapp --src-path ./publish.zip --type zip
```

---

## 🖥️ Option 4: Deploy to Linux VPS (Ubuntu / Debian + Nginx)

### 1. Install .NET 10 Runtime
```bash
sudo apt-get update
sudo apt-get install -y dotnet-aspnetcore-runtime-10.0
```

### 2. Copy Published Files
```bash
# On development machine:
dotnet publish -c Release -o /tmp/newswebapp
scp -r /tmp/newswebapp user@your-server-ip:/var/www/newswebapp
```

### 3. Setup Systemd Service
Create `/etc/systemd/system/newswebapp.service`:
```ini
[Unit]
Description=NewsWebApp ASP.NET Core Application
After=network.target

[Service]
WorkingDirectory=/var/www/newswebapp
ExecStart=/usr/bin/dotnet /var/www/newswebapp/NewsWebApp.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=newswebapp
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=DatabaseProvider=Sqlite
Environment=ConnectionStrings__DefaultConnection=Data Source=/var/www/newswebapp/newswebapp.db

[Install]
WantedBy=multi-user.target
```

Enable and start the service:
```bash
sudo systemctl enable newswebapp.service
sudo systemctl start newswebapp.service
```

### 4. Setup Nginx Reverse Proxy
Create `/etc/nginx/sites-available/newswebapp`:
```nginx
server {
    listen 80;
    server_name your-domain.com www.your-domain.com;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```
Enable the site and reload Nginx:
```bash
sudo ln -s /etc/nginx/sites-available/newswebapp /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

---

## ⚙️ Environment Variables Reference

| Variable | Description | Default |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | Application environment (`Development`, `Production`) | `Production` |
| `ASPNETCORE_URLS` | URLs and ports the app listens on | `http://+:8080` |
| `DatabaseProvider` | Database engine (`Sqlite`, `SqlServer`) | `Sqlite` |
| `ConnectionStrings__DefaultConnection` | Database connection string | `Data Source=/app/data/newswebapp.db` |
| `LiveNews__AutoSyncEnabled` | Enables background news poller | `true` |
| `LiveNews__SyncIntervalMinutes` | Background poller frequency in minutes | `60` |

---

## 🔒 Security Best Practices
- Always change default passwords after first deployment.
- If switching to SQL Server in production, pass the connection string via secret environment variables rather than hardcoding in `appsettings.json`.
- Enable SSL/HTTPS using Let's Encrypt (`certbot --nginx`).
