# Architecture Overview

> **Slide talking points:** Show the overall system design and data flow. Good for the "what did you build?" slide.

---

## What Is MusicShare?

- You paste a Spotify link → it gives you the same song on YouTube Music and Apple Music
- Works in reverse too (any platform → all platforms)
- Installable as a **PWA** — works like a native app
- Has a **Web Share Target** — you can "share to MusicShare" from any app on your phone

---

## Tech Stack (Quick Reference)

| Layer | Technology |
|-------|-----------|
| Frontend | Next.js 16 + React 19 + TypeScript + Tailwind CSS |
| API | .NET 10 + ASP.NET Core |
| Messaging | RabbitMQ via MassTransit |
| Orchestration (local) | .NET Aspire |
| Database | MongoDB |
| Cloud | Azure Container Apps |
| CI/CD | GitHub Actions + Azure Developer CLI (azd) |

---

## The Data Flow (What Happens When You Share a Song)

```
1.  User pastes Spotify URL into the web app
         ↓
2.  POST /api/share
    → Detects it's a Spotify URL
    → Creates a ShareRequest in MongoDB (status: Pending)
    → Publishes SongShareSubmitted event to RabbitMQ
         ↓
3.  ShareRequestSaga wakes up
    → Publishes ResolveSourceMetadata command
         ↓
4.  SourceMetadataConsumer handles it
    → Calls Spotify API → gets title, artists, album, artwork, duration
    → Creates a Song entity in MongoDB
    → Publishes SourceMetadataResolved event
         ↓
5.  Saga fans out in PARALLEL:
    → ResolveServiceLink → YouTube Music consumer
    → ResolveServiceLink → Apple Music consumer
         ↓
6.  Each consumer:
    → Searches for the song on its platform
    → ConfidenceAdapter filters results (fuzzy match by title, artist, album, duration)
    → Saves SongServiceLink to MongoDB
    → Publishes ServiceLinkResolved event
         ↓
7.  Saga collects all results
    → All done? → CompleteSagaActivity
    → Updates Song status, ShareRequest status → Completed
    → Triggers ISR cache revalidation on the frontend
         ↓
8.  User's browser (which has been polling every second)
    → Gets status: Completed
    → Renders the result page with all platform links
```

---

## Key Architectural Decisions

### Why RabbitMQ + MassTransit?
- Song resolution across 3 services is I/O-heavy — doing it serially would be slow
- Async messaging lets all 3 services resolve **in parallel**
- The Saga pattern manages state so we know when all 3 are done
- If one service fails, the others still complete (partial results are fine)

### Why MongoDB?
- Flexible schema — song metadata differs by service
- Native support for MassTransit saga state storage
- Easy to run locally with Aspire, easy to scale in Azure

### Why Next.js + ISR?
- Result pages are shareable links — they need to be fast on first load
- ISR means the page is pre-rendered and cached after first resolution
- When deployment happens, CI warms the cache automatically

### Why .NET Aspire?
- One command to spin up the entire stack locally (MongoDB, RabbitMQ, API, Frontend)
- Same configuration drives the Azure Container Apps deployment
- Built-in dashboard for traces, logs, health checks during dev

---

## Project Structure at a Glance

```
MusicShare/
├── MusicShare.Api/          ← REST API + Saga + Consumers + CQRS handlers
├── MusicShare.Services/     ← Domain logic, music adapters, confidence scoring
├── MusicShare.Persistence/  ← MongoDB repositories + entities
├── MusicShare.Contracts/    ← Message contracts, enums (shared types)
├── MusicShare.Frontend/     ← Next.js app (React, Tailwind, PWA)
├── MusicShare.AppHost/      ← .NET Aspire orchestrator
├── MusicShare.ServiceDefaults/ ← Shared infra (OpenTelemetry, health checks)
└── MusicShare.Tests/        ← xUnit tests (unit + integration)
```

---

## What Makes This "Production-Grade"

- **Observability:** OpenTelemetry traces piped to Aspire Dashboard (local) / Azure Monitor (prod)
- **Concurrency:** MongoDB optimistic concurrency on saga state — handles parallel events safely
- **Idempotency:** Consumers check for existing results before re-resolving
- **Duplicate detection:** Same Spotify URL submitted twice → returns existing result instantly
- **Autoscaling:** Azure Container Apps scales to zero when idle, scales up under load
- **Cache warming:** CI warms ISR cache after every deployment
- **Secured endpoints:** Re-indexing API requires API key authentication
