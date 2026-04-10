# Seq Logging Setup

## Overview

This project uses **Serilog** with **Seq** for structured logging. Seq provides a beautiful web-based UI for viewing, searching, and analyzing application logs in real-time.

## Access Seq Web UI

- **URL**: http://localhost:5341
- **Username**: `admin`
- **Password**: `admin`

## What is Seq?

Seq is a structured logging server that:

- Provides a web-based UI for viewing logs
- Supports real-time log streaming
- Allows filtering and searching logs with SQL-like queries
- Shows log properties in a structured format
- Supports log correlation and tracing

## How It Works

1. **Serilog** is configured in `Program.cs` to send logs to:
   - Console (stdout) - for Docker logs
   - Seq server - for web UI

2. **Seq** runs in a Docker container and receives logs from the API

3. All application logs (from `ILogger<T>`) are automatically sent to Seq

## Viewing Logs

1. Open http://localhost:5341 in your browser
2. Log in with `admin` / `admin`
3. You'll see all application logs in real-time
4. Use the search bar to filter logs (e.g., `Level = 'Error'`)

## Example Log Queries in Seq

- `Level = 'Error'` - Show only errors
- `Message like '%OAuth2%'` - Show logs containing "OAuth2"
- `@Properties.UserId = 'user123'` - Show logs for specific user
- `@Timestamp > ago(5m)` - Show logs from last 5 minutes

## Configuration

Seq configuration is in `docker-compose.dev.yml`:

- Port 5341: Web UI
- Port 5342: Log ingestion (for external services)

Serilog configuration is in:

- `appsettings.json` - Base configuration
- `Program.cs` - Additional setup with Seq sink

## Stopping Seq

Seq will stop automatically when you run `./stop-dev.sh`. To stop only Seq:

```bash
docker-compose -f docker-compose.dev.yml stop seq
```

## Restarting Seq

```bash
docker-compose -f docker-compose.dev.yml restart seq
```
