# Hemglass ETA System — POC Architecture

**Version:** 1.2
**Date:** 2025-12-21
**Scope:** Proof-of-Concept
**Based on:** [Brainstorming Session Results](./brainstorming-session-results.md)

---

## Overview

A real-time ETA service for ice cream truck routes.

**Core formula:** `ETA = Travel Time (routing API) + Dwell Time (calculated from schedule)`

**Key insight:** Scheduled stop times are estimates. Real-world factors cause drift. GPS data enables real estimates based on actual position.

---

## Architecture Decisions

| Dimension | Decision | Rationale |
|-----------|----------|-----------|
| **Routing API** | OpenRouteService Matrix API | 2000 req/day, 40/min — better limits than alternatives |
| **Route Data** | Iceman API | Real route data from existing system |
| **Data Flow** | Webhook + event-driven | Efficient — recalc only on meaningful changes |
| **ETA Calculation** | 10 stops + delay propagation | Small delays local, 1+ hour delays notify all |
| **Output** | REST + SignalR (stubbed) | Right tool for each consumer type |
| **Dwell Time** | Average from schedule pattern | Extensible for ML prediction later |
| **Caching** | In-memory route cache | Reduces Iceman API calls |

---

## System Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        HEMGLASS ETA SYSTEM                         │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  [GPS Hardware] ──webhook──▶ [ETA Service] ──▶ [Iceman API]        │
│                                   │                                 │
│                                   ▼                                 │
│                         ┌─────────────────┐                        │
│                         │  ETA Calculator │──▶ [OpenRouteService]  │
│                         │ (10 next stops) │                        │
│                         └────────┬────────┘                        │
│                                  │                                  │
│              ┌───────────────────┼───────────────────┐             │
│              ▼                   ▼                   ▼             │
│        [REST API]          [SignalR Hub]      [Push Service]       │
│             │                 (stub)              (future)         │
│             ▼                   ▼                                  │
│       Dispatcher            Customer App                           │
│       Dashboard             (real-time)                            │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Tech Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Runtime |
| ASP.NET Core | 8.0 | REST + SignalR |
| HttpClient | Built-in | External API calls |
| System.Text.Json | Built-in | JSON handling |

---

## Project Structure

```
Hemglass.ETA/
├── src/
│   ├── Hemglass.ETA.Api/
│   │   ├── Hubs/
│   │   │   └── EtaHub.cs              # SignalR stub
│   │   └── Program.cs                 # Endpoints + DI
│   │
│   ├── Hemglass.ETA.Core/
│   │   ├── Services/
│   │   │   ├── EtaCalculator.cs       # Core calculation logic
│   │   │   ├── IRoutingService.cs     # Travel time abstraction
│   │   │   └── IRouteService.cs       # Route data abstraction
│   │   └── Models/
│   │       ├── Stop.cs
│   │       ├── Route.cs
│   │       ├── EtaResult.cs
│   │       ├── StopEta.cs
│   │       └── GeoCoordinate.cs
│   │
│   └── Hemglass.ETA.Infrastructure/
│       ├── OpenRouteService.cs        # Matrix API implementation
│       ├── RouteStore.cs              # In-memory route caching
│       └── Iceman/
│           ├── IcemanRouteService.cs  # Route data from Iceman
│           └── IcemanApiResponse.cs   # DTOs
│
├── tests/
│   └── Hemglass.ETA.Tests/
│       └── EtaCalculatorTests.cs
│
└── Hemglass.ETA.sln
```

---

## OpenRouteService Integration

### Why Matrix API?

Individual route calls exceeded rate limits quickly. Matrix API allows batch requests for multiple point-to-point travel times in a single call.

### Rate Limits

OpenRouteService free tier: **2000 requests/day**, **40 requests/minute**. Much better than alternatives.

### 10-Stop Display

We calculate ETAs for **10 stops** ahead — a reasonable limit for UI display.

```
Points sent: [truck, stop1, stop2, ..., stop10] = 11 points
Travel times: truck→stop1, stop1→stop2, ..., stop9→stop10
```

### Caching

Route data is cached in-memory (`RouteStore`):
- Avoids repeated Iceman API calls for same route
- 30-minute expiration
- Cleared on app restart

---

## Dwell Time Calculation

### Current Approach

Average dwell time is calculated from the route schedule:

```
Average Dwell = Average Schedule Gap - Average Travel Time
```

This uses the planned schedule pattern to estimate how long the truck typically spends at each stop.

### ML Future Option

The `EtaCalculator` can be extended to use ML-predicted dwell times:

| Application | Predicts | Training Data |
|-------------|----------|---------------|
| **Dwell time prediction** | How long at each stop | Historical durations + season, time, location type |
| **Demand forecasting** | Which stops will be busy | Sales history + weather + events |

Every route run generates training data. Today's schedule-based calculation can be replaced with ML predictions when data is available.

---

## Data Models

```csharp
public record GeoCoordinate(double Latitude, double Longitude);

public record Stop(
    int StopId,
    string Name,
    GeoCoordinate Position,
    int Sequence,
    DateTime PlannedArrival
);

public record Route(
    int RouteId,
    List<Stop> Stops
);

public record StopEta(
    int StopId,
    string Name,
    double Latitude,
    double Longitude,
    DateTime EstimatedArrival,
    int MinutesFromNow,
    int TravelMinutes
);

public record EtaResult(
    int RouteId,
    DateTime CalculatedAt,
    string CurrentStopAddress,
    int RemainingStopsCount,
    int AverageDwellMinutes,
    List<StopEta> Stops
);

public record TruckPosition(
    string TruckId,
    GeoCoordinate Position,
    double Speed,
    double Heading,
    double FreezerTemp,
    DateTime Timestamp
);
```

---

## Key Interfaces

### IRoutingService

```csharp
public interface IRoutingService
{
    Task<int> GetTravelTimeMinutes(GeoCoordinate from, GeoCoordinate to);
    Task<int[]> GetSequentialTravelTimesMinutes(IReadOnlyList<GeoCoordinate> points);
}
```

**Implementation:** `OpenRouteService` using Matrix API.

### IRouteService

```csharp
public interface IRouteService
{
    Task<Route?> GetRouteByStopAsync(int stopId);
}
```

**Implementation:** `IcemanRouteService` fetches route data from Iceman API.

---

## API Endpoints

### GET /api/route/{stopId}

Get route info with all stops for map display.

**Response:**
```json
{
  "routeId": 3535180,
  "stops": [
    { "stopId": 101, "name": "Storgatan 15", "latitude": 59.33, "longitude": 18.07 },
    { "stopId": 102, "name": "Vasagatan 8", "latitude": 59.34, "longitude": 18.08 }
  ]
}
```

### GET /api/eta/{stopId}

Get ETAs for stops after the truck's current position.

**Query params:**
- `lat`, `lon` — truck's current position
- `fromStopId` (optional) — show stops after this one

**Response:**
```json
{
  "routeId": 3535180,
  "calculatedAt": "2025-12-20T14:32:00Z",
  "currentStopAddress": "Kungsgatan 10",
  "remainingStopsCount": 45,
  "averageDwellMinutes": 5,
  "stops": [
    {
      "stopId": 101,
      "name": "Storgatan 15",
      "latitude": 59.33,
      "longitude": 18.07,
      "estimatedArrival": "2025-12-20T14:38:00Z",
      "minutesFromNow": 6,
      "travelMinutes": 6
    }
  ]
}
```

### POST /api/webhook/position

Receive GPS position updates from truck hardware.

**Request:**
```json
{
  "truckId": "TRUCK-042",
  "latitude": 59.3293,
  "longitude": 18.0686,
  "speed": 35.5,
  "heading": 180.0,
  "freezerTemp": -18.5,
  "timestamp": "2025-12-20T14:32:10Z"
}
```

### SignalR Hub: /eta-hub (Stub)

**Client methods:** `JoinRoute(routeId)`, `LeaveRoute(routeId)`
**Server events:** `EtaUpdated(etaResult)`

---

## Customer Notification Strategy

Two-phase approach based on truck proximity:

| Phase | Distance | Customer Sees | Source |
|-------|----------|---------------|--------|
| **Planning** | 10+ stops away | "Today 10:00-11:00" | Scheduled route |
| **Tracking** | <10 stops away | "Arriving in 12 min" | Real-time GPS |

**Rationale:** Far away = many variables, low certainty → show time window. Close = fewer unknowns → show precise ETA.

**Trigger:** When truck is 10 stops away, push "Truck is nearby!" and switch to live tracking mode.

> **POC Status:** Not implemented. Would be part of push notification feature.

---

## Edge Cases

| Scenario | Solution |
|----------|----------|
| **GPS silence** | Cross-check POS: if sales active, trust POS; if both silent 15+ min, alert dispatcher |
| **Stop inaccessible** | Driver marks "stop failed" + reason → notify customers |
| **Routing API down** | Fallback: return 0 travel times, show "unavailable" |
| **Large delay (1+ hour)** | Notify all customers, shift time windows |

---

## POC Scope

### Built
- Core ETA calculation logic
- OpenRouteService Matrix API integration with caching
- Iceman API integration for real route data
- REST API endpoints for route and ETA queries
- Webhook receiver for GPS updates
- React frontend with interactive map
- Clean project structure

### Stubbed
- SignalR hub setup

### POC Limitations
- **Today's routes only**: Schedule times from Iceman API are combined with today's date. No support for querying future or past routes.

### Future
- Two-phase customer notifications (schedule window → real-time ETA)
- Event-driven triggers (recalc on stop completion, 500m movement)
- Push notifications (Firebase for mobile)
- ML dwell time prediction
- POS cross-referencing for GPS silence detection

---

*POC Architecture — Updated 2025-12-21*
