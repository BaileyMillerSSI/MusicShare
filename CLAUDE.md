# CLAUDE.md - MusicShare Codebase Guide

This document provides guidance for AI assistants working on the MusicShare codebase.

## Project Overview

MusicShare is a full-stack web application that allows users to share music URLs across different streaming platforms (Spotify, Apple Music, YouTube Music). When a user submits a song link from one service, the application resolves the same song across all supported platforms.

## Architecture

**Stack:**
- **Backend**: .NET 10 with ASP.NET Core API and background Worker
- **Frontend**: React 19 + TypeScript + Vite
- **Database**: MongoDB
- **Messaging**: RabbitMQ (via MassTransit)
- **Orchestration**: .NET Aspire (local dev) / Azure Container Apps (production)

**Key Pattern**: CQRS with MediatR for API, Saga pattern with MassTransit for async processing.

## Project Structure

```
MusicShare/
├── MusicShare.Api/           # REST API (Controllers, Commands, Queries, Services)
├── MusicShare.Worker/        # Background processor (Consumers, Sagas)
├── MusicShare.Frontend/      # React SPA
├── MusicShare.Persistence/   # Data layer (Entities, Repositories, MongoDB context)
├── MusicShare.MusicAdapters/ # Music service integrations (Spotify, Apple, YouTube)
├── MusicShare.Contracts/     # Shared types, enums, and message contracts
├── MusicShare.ServiceDefaults/ # Shared infrastructure (OpenTelemetry, health checks)
├── MusicShare.AppHost/       # .NET Aspire orchestrator for local development
└── MusicShare.slnx           # Solution file
```

## Development Commands

### Frontend (run from `MusicShare.Frontend/`)

```bash
npm install          # Install dependencies
npm run dev          # Start dev server (port 5173)
npm run build        # TypeScript check + production build
npm run lint         # Run ESLint
npm run lint:fix     # Auto-fix lint issues
npm run preview      # Preview production build
```

### Backend (run from root)

```bash
dotnet restore MusicShare.slnx                    # Restore packages
dotnet build MusicShare.slnx                      # Build all projects
dotnet build MusicShare.slnx --configuration Release  # Release build
dotnet test MusicShare.slnx                       # Run tests
```

### Running Locally with Aspire

```bash
dotnet run --project MusicShare.AppHost           # Start full stack
```

This starts MongoDB, RabbitMQ, API, Worker, and Frontend with dev tooling (Mongo Express, RabbitMQ Management).

### Azure Deployment

```bash
azd provision     # Provision Azure infrastructure
azd deploy        # Deploy application
azd up            # Both provision and deploy
```

## Code Conventions

### C# / .NET

- **Naming**: PascalCase for classes, methods, properties. Interfaces prefix with `I`.
- **File-scoped namespaces**: Use `namespace X;` style.
- **Primary constructors**: Use for dependency injection (e.g., `public class X(IDep dep)`).
- **MediatR pattern**: Commands implement `IRequest<TResponse>`, handlers implement `IRequestHandler<TRequest, TResponse>`.
- **Repository pattern**: `IRepository<T>` interfaces with MongoDB implementations.
- **Entities**: MongoDB attributes (`[BsonId]`, `[BsonElement]`) for persistence mapping.

### TypeScript / React

- **Functional components**: Use function declarations, not arrow functions for components.
- **React Query**: Use `useQuery` for data fetching with polling where needed.
- **Routing**: React Router v7 with typed route params.
- **Styling**: Tailwind CSS classes inline.
- **API client**: Centralized in `src/lib/api.ts` using fetch.
- **Types**: Define interfaces for API responses in `api.ts`.

### API Design

- **Base path**: `/api/[controller]`
- **Endpoints**:
  - `POST /api/share` - Submit a music URL for resolution
  - `GET /api/share/{shareId}` - Get resolution results

## Key Files to Know

| Area | File | Purpose |
|------|------|---------|
| API Entry | `MusicShare.Api/Program.cs` | Service configuration |
| API Controller | `MusicShare.Api/Controllers/ShareController.cs` | REST endpoints |
| Worker Entry | `MusicShare.Worker/Program.cs` | Background service config |
| Saga | `MusicShare.Worker/Sagas/ShareRequestSaga.cs` | Async workflow orchestration |
| Frontend Entry | `MusicShare.Frontend/src/main.tsx` | React app bootstrap |
| Frontend Routes | `MusicShare.Frontend/src/App.tsx` | Route definitions |
| API Client | `MusicShare.Frontend/src/lib/api.ts` | Backend communication |
| Domain Entities | `MusicShare.Persistence/Entities/` | Song, ShareRequest, etc. |
| Music Adapters | `MusicShare.MusicAdapters/Services/Music/` | Spotify, Apple, YouTube integrations |
| Orchestration | `MusicShare.AppHost/AppHost.cs` | Local dev infrastructure |

## Data Flow

1. User submits URL on `SharePage` -> `POST /api/share`
2. API validates URL, detects service type, creates `ShareRequest`, publishes `SongShareSubmitted` event
3. Worker's `ShareRequestSaga` orchestrates resolution:
   - Extract metadata from source service
   - Search for song on other services (Spotify, Apple Music, YouTube Music)
   - Update `Song` and `SongServiceLink` records
4. Frontend polls `GET /api/share/{shareId}` until status is `Completed`
5. `ResultPage` displays song info with links to all platforms

## Enums

**ServiceType** (in `MusicShare.Contracts/ServiceType.cs`):
- `Spotify = 1`
- `AppleMusic = 2`
- `YouTubeMusic = 3`

**ShareStatus**: `Pending`, `Processing`, `Completed`, `Failed`

**SongStatus**: `Pending`, `Resolved`, `NotFound`, `Error`

## Environment Configuration

Required secrets/environment variables for full functionality:
- `Spotify__ClientId`, `Spotify__ClientSecret` - Spotify API credentials
- `YouTube__GeographicLocation` - YouTube region setting
- MongoDB and RabbitMQ connection details (handled by Aspire locally)

## CI/CD Pipeline

GitHub Actions workflow (`.github/workflows/ci.yml`):
- **frontend job**: Node 20, npm ci, lint, build
- **backend job**: .NET 10, restore, build (Release), test
- **deploy job**: Runs on push to main/develop, provisions and deploys to Azure

## Testing Guidelines

- Backend tests: `dotnet test MusicShare.slnx`
- Frontend lint: `npm run lint` (no test runner configured yet)
- Always run lint before committing frontend changes

## Common Tasks

### Adding a new music service

1. Create adapter in `MusicShare.MusicAdapters/Services/Music/{ServiceName}/`
2. Implement `IMusicServiceAdapter` interface
3. Add enum value to `ServiceType`
4. Register in DI via `MusicAdapterExtensions`
5. Add consumer in Worker for the new service
6. Update Saga to include the new service
7. Add frontend link component in `src/components/MusicLinks/`

### Adding a new API endpoint

1. Add Command/Query in `MusicShare.Api/Commands/` or `MusicShare.Api/Queries/`
2. Create handler implementing `IRequestHandler<TRequest, TResponse>`
3. Add controller method using `_mediator.Send()`
4. Update frontend `api.ts` with new types and fetch method

### Modifying database entities

1. Update entity in `MusicShare.Persistence/Entities/`
2. Ensure `[BsonElement]` attributes match MongoDB field names
3. Update repository if needed
4. Consider migration strategy for existing data
