# Brainstorming Session Results

**Session Date:** 2025-12-16
**Updated:** 2025-12-21
**Facilitator:** Business Analyst
**Topic:** Hemglass ETA System

---

## Executive Summary

**Goal:** Design a real-time ETA system for ice cream truck routes — architecture, edge cases, and extensibility.

**Scope:** Proof-of-concept with path to production.

**Techniques Used:** First Principles, Morphological Analysis, What If Scenarios

**Key Themes Identified:**
- Event-driven efficiency over constant polling
- Delay propagation to all waiting customers
- Two-phase notifications (schedule window → real-time ETA)
- Extensible design (ready for ML, but pragmatic for POC)
- Graceful degradation when external services fail

---

## First Principles Analysis

### Core Problem
> ETA = Travel Time (routing API) + Dwell Time (variable)

**Key Insight:** Scheduled stop times are estimates. Real-world factors (customer demand, traffic, delays) cause drift. GPS data enables real estimates based on actual position.

### Factors Affecting ETA

| Category | Factor | Data Source |
|----------|--------|-------------|
| Route/Travel | Traffic, distance, intersections | Routing API (OpenRouteService) |
| Demand/Dwell | Season, time of day, location type | Schedule pattern / ML (future) |
| Known Inputs | Pre-ordered packages | Order system |

### Consumers
- **Customers** — Push notifications ("Truck arriving in 10 min!")
- **Dispatchers** — Fleet monitoring, restocking, backup planning

---

## Architecture Decisions (Morphological Analysis)

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
│             │                    │                   │             │
│             ▼                    ▼                   ▼             │
│       Dispatcher            Customer App        Mobile Alert       │
│       Dashboard             (real-time)        ("10 min away!")    │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

| Dimension | Decision | Rationale |
|-----------|----------|-----------|
| **Routing API** | OpenRouteService Matrix API | 2000 req/day, 40/min — better limits |
| **Route Data** | Iceman API | Real route data from existing system |
| **Data Flow** | Webhook + event-driven | Efficient — recalc only on meaningful changes |
| **ETA Calculation** | Full route recalc | Delays must propagate to ALL waiting customers |
| **Output** | REST + SignalR + Push | Right tool for each consumer type |

### Why Event-Driven?
> "Do we need constant location updates?" No. ETA recalculates only when:
> - Truck completes a stop
> - Significant position change (500m+)
> - Truck deviates from route
> - Customer requests ETA

At 300 trucks, this saves significant API costs vs constant polling.

### Delay Propagation Strategy

Not all delays need to reach all customers:

| Delay | Customer Distance | Action |
|-------|-------------------|--------|
| < 1 hour | 10+ stops away | No notification (within their time window) |
| < 1 hour | < 10 stops away | Update real-time ETA |
| **1+ hour** | All customers | Push: "Schedule updated to 11:00-12:00" |

**Rationale:** Small delays often recover. A 15-min delay at stop 5 may be absorbed by stop 30. But a 1+ hour delay shifts everyone's time window — they need to know.

### Why 10-Stop Calculation?

UI displays **10 stops** ahead — a reasonable limit without overwhelming users.

**POC scope:** Calculates 10 stops only. Production would track cumulative delay for the "1+ hour" propagation rule.

---

## Dwell Time Strategy

### POC Approach
Average dwell time calculated from schedule pattern:
```
Average Dwell = Average Schedule Gap - Average Travel Time
```

### ML Future Option
Design supports swapping in ML-predicted dwell times:

| Application | Predicts | Training Data |
|-------------|----------|---------------|
| **Dwell time prediction** | How long at each stop | Historical durations + season, time, location type |
| **Demand forecasting** | Which stops will be busy | Sales history + weather + events |
| **Route optimization** | Best stop order | Historical completion times |
| **Anomaly detection** | Something's wrong | GPS patterns, POS activity |

> "Every route run generates training data. Today it's a fallback; tomorrow it trains a model."

---

## Customer Notification Strategy

### Two-Phase Approach

Notifications adapt based on truck proximity — certainty increases as truck gets closer.

| Phase | Distance | Customer Sees | Source |
|-------|----------|---------------|--------|
| **Planning** | 10+ stops away | "Today 10:00-11:00" | Scheduled route times |
| **Tracking** | <10 stops away | "Arriving in 12 min" | Real-time GPS + routing |

### Why Two Phases?

- **Far away:** Too many variables (traffic, dwell variance, weather). A time window sets realistic expectations without false precision.
- **Close:** Fewer unknowns. Real-time ETA is accurate enough to trust and act on.

### Example Flow

```
Customer at Stop 45:

Truck at Stop 10 → "Expected 10:00-11:00"
Truck at Stop 35 → Push: "Ice cream truck is nearby!"
                   App switches to live tracking mode
Truck at Stop 40 → "Arriving in ~15 min"
Truck at Stop 44 → "Arriving in ~3 min"
Truck at Stop 45 → "Truck is here!"
```

### Trigger Point

The "10 stops away" threshold aligns with the POC's calculation limit — once within range, we have real routing data.

> **POC Status:** Not implemented. Current system calculates ETA on-demand. This strategy would be part of the push notification feature.

---

## Edge Cases (What If Scenarios)

| Scenario | Solution |
|----------|----------|
| **GPS silence** | 10-15 min timeout + cross-check POS activity |
| **Stop inaccessible** | Driver marks "stop failed" + reason → notify registered customers |
| **Routing API down** | Fallback: return 0 travel times, show "unavailable" |
| **Traffic/construction** | Handled by OpenRouteService routing data |

### GPS Silence Detection

Don't rely on GPS alone. Cross-reference multiple signals:

| Signal | Source | Indicates |
|--------|--------|-----------|
| GPS | Fleet hardware | Location, movement |
| POS | Sales system | Driver actively selling |
| Driver app | Mobile check-ins | Stop completions |
| Schedule | Route plan | Expected position |

**Decision matrix:**

| GPS | POS | Likely Status | Action |
|-----|-----|---------------|--------|
| ✓ Active | ✓ Active | Normal | — |
| ✗ Silent | ✓ Active | Bad reception (tunnel, garage) | Trust POS, don't alert |
| ✓ Active | ✗ Silent | Between stops, driving | Normal |
| ✗ Silent | ✗ Silent | Potential problem | Alert dispatcher after 15 min |

**Principle:** One silent signal = maybe a glitch. Multiple silent signals = likely a real problem.

### Historical Data as Fallback
> "Last Tuesday at 2pm, this segment took 12 min." Same route data that powers fallback becomes ML training data later.

---

## POC Scope

### Built
- Core ETA calculation logic with dwell time from schedule
- OpenRouteService Matrix API integration
- Iceman API integration for real route data
- REST API endpoints for route and ETA queries
- Webhook receiver for GPS updates (stub)
- React frontend with interactive map
- In-memory route caching

### Stubbed
- SignalR hub setup

### POC Limitations
- **Today's routes only**: Schedule times from Iceman API are combined with today's date. No support for querying future or past routes.

### Future
- Event-driven triggers (recalc on stop completion, 500m movement)
- Push notifications (Firebase for mobile)
- ML dwell time prediction
- POS cross-referencing

---

## Project Structure

3-layer architecture + React frontend:

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **Presentation** | `.Api` | HTTP endpoints, SignalR hub |
| **Business Logic** | `.Core` | ETA calculation, interfaces, models |
| **Data Access** | `.Infrastructure` | External APIs, caching |
| **Frontend** | `.Web` | React UI, map display |

```
Hemglass.ETA/
├── src/
│   ├── Hemglass.ETA.Api/
│   │   ├── Hubs/EtaHub.cs
│   │   └── Program.cs
│   │
│   ├── Hemglass.ETA.Core/
│   │   ├── Services/
│   │   │   ├── EtaCalculator.cs
│   │   │   ├── IRoutingService.cs
│   │   │   └── IRouteService.cs
│   │   └── Models/
│   │
│   ├── Hemglass.ETA.Infrastructure/
│   │   ├── OpenRouteService.cs
│   │   ├── RouteStore.cs
│   │   └── Iceman/
│   │
│   └── Hemglass.ETA.Web/          # React frontend
│       └── src/
│           ├── App.tsx
│           └── components/
│
└── docs/
```

---

*Session facilitated using the BMAD-METHOD brainstorming framework*
*Updated 2025-12-21 to reflect OpenRouteService and React frontend*
