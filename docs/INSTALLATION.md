# Installation Guide

## Prerequisites

- .NET 8.0 SDK
- Node.js 18+ (for web console)
- Windows 10/11, macOS, or Linux

## Building from Source

### Build All Projects

```bash
dotnet build NIGHTFRAME.sln
```

### Build Individual Components

```bash
# Shared library
dotnet build Shared/Shared.csproj

# Orchestrator
dotnet build Orchestrator/Orchestrator.csproj

# Drone
dotnet build Drone/Drone.csproj

# Watchdog
dotnet build Watchdog/Watchdog.csproj
```

## Running

### Genesis Mode (Single Machine Testing)

```bash
dotnet run --project Orchestrator -- --mode genesis
```

This starts the Orchestrator with an embedded Drone on a single machine for development.

### Production Mode

```bash
# Start Orchestrator
dotnet run --project Orchestrator

# Start Drone (on same or different machine)
dotnet run --project Drone
```

### Drone with Cellular Support

```bash
# Auto-detect modem
dotnet run --project Drone

# Specify modem port
dotnet run --project Drone -- --modem COM3

# List available modems
dotnet run --project Drone -- --list-modems
```

## Web Console

```bash
cd gamma1-web
npm install
npm run dev
```

Access at `http://localhost:3000`

## Android Scout

```bash
cd DroneAndroid
dotnet build -c Release
dotnet publish -c Release -f net8.0-android
```

---

Copyright © 2024 Malachi Hooper. All rights reserved.
