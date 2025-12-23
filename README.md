# Hemglass ETA System

Real-time ETA service for ice cream truck routes.

## Overview

Calculates estimated arrival times for ice cream trucks using GPS data and route information.

**Core formula:** `ETA = Travel Time (routing API) + Dwell Time (calculated from schedule)`

## Tech Stack

**Backend:**
- .NET 8.0
- ASP.NET Core (REST + SignalR)
- OpenRouteService Matrix API (routing)
- Iceman API (route data)

**Frontend:**
- React 18 + TypeScript
- Vite
- Leaflet (interactive maps)

## Project Structure

```
src/
├── Hemglass.ETA.Api/              # REST endpoints + SignalR hub
├── Hemglass.ETA.Core/             # Business logic + models
├── Hemglass.ETA.Infrastructure/   # External API integrations
└── Hemglass.ETA.Web/              # React frontend
    └── src/
        ├── components/
        │   ├── TruckMap.tsx       # Interactive route map
        │   └── StopList.tsx       # ETA stop list
        └── App.tsx
tests/
└── Hemglass.ETA.Tests/            # Unit tests
```

## Getting Started

### Backend

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run API
dotnet run --project src/Hemglass.ETA.Api
```

### Frontend

```bash
cd src/Hemglass.ETA.Web

# Install dependencies
npm install

# Run dev server
npm run dev
```

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/route/{stopId}` | Get route info with all stops |
| `GET /api/eta/{stopId}?lat=X&lon=Y` | Get ETAs for upcoming stops |
| `POST /api/webhook/position` | Receive GPS position updates |

## License

Proprietary
