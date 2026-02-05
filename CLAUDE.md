# CLAUDE.md - MusicShare Codebase Guide

This document provides guidance for AI assistants working on the MusicShare codebase.

## Project Overview

MusicShare is a full-stack web application that allows users to share music URLs across different streaming platforms (Spotify, Apple Music, YouTube Music). When a user submits a song link from one service, the application resolves the same song across all supported platforms.

## Claude Code Instructions

- GitHub repo: https://github.com/BaileyMillerSSI/MusicShare
- Default base branch: main
- Create feature branches as: feat/issue-<number>-<short-name>
- Keep commits small and logical
- Follow existing coding conventions
- Do not refactor unrelated code
- If requirements are unclear, make reasonable assumptions and document them in the PR
- Always open a PR targeting develop

## Architecture

**Stack:**
- **Backend**: .NET 10 with ASP.NET Core API and background Worker
- **Frontend**: Next.js 16 + React 19 + TypeScript
- **Database**: MongoDB
- **Messaging**: RabbitMQ (via MassTransit)
- **Orchestration**: .NET Aspire (local dev) / Azure Container Apps (production)

**Key Patterns**:
- CQRS with MediatR for API
- Saga pattern with MassTransit for async processing
- Repository pattern for data access
- PWA with Web Share Target API support

## Project Structure

```
MusicShare/
├── MusicShare.Api/              # REST API (Controllers, Commands, Queries, Services)
│   ├── Controllers/             # ShareController.cs - REST endpoints
│   ├── Commands/                # CQRS commands (SubmitShareRequest.cs)
│   ├── Queries/                 # CQRS queries (GetShareResultQuery.cs)
│   ├── Services/                # ShareRequestService.cs
│   ├── Models/                  # Response DTOs
│   └── Program.cs               # Service configuration
├── MusicShare.Worker/           # Background processor (Consumers, Sagas)
│   ├── Sagas/                   # ShareRequestSaga.cs - state machine orchestrator
│   ├── Consumers/               # Service-specific message consumers
│   └── Program.cs               # Worker configuration
├── MusicShare.Frontend/         # Next.js SPA
│   ├── src/
│   │   ├── app/                 # Next.js App Router
│   │   │   ├── api/revalidate/  # ISR revalidation endpoint
│   │   │   ├── share/[shareId]/ # Dynamic share result routes
│   │   │   ├── layout.tsx       # Root layout
│   │   │   └── page.tsx         # Home page
│   │   ├── components/          # React components
│   │   │   ├── MusicLinks/      # Service-specific link components
│   │   │   ├── ShareForm.tsx    # URL submission form
│   │   │   ├── ResultPoller.tsx # Polling component
│   │   │   └── QueryClientWrapper.tsx
│   │   └── lib/api.ts           # Centralized API client
│   ├── public/                  # Static assets, PWA manifest, icons
│   ├── Dockerfile               # 3-stage production build
│   ├── next.config.ts           # Next.js configuration
│   └── package.json
├── MusicShare.Persistence/      # Data layer (Entities, Repositories, MongoDB context)
│   ├── Entities/                # Song.cs, ShareRequest.cs, SongServiceLink.cs
│   ├── Repositories/            # Repository implementations
│   ├── MusicShareDbContext.cs   # MongoDB context
│   └── DependencyInjection.cs   # DI registration
├── MusicShare.MusicAdapters/    # Music service integrations
│   ├── Services/Music/
│   │   ├── Spotify/             # SpotifyMusicService.cs
│   │   ├── YouTube/             # YouTubeMusicAdapter.cs
│   │   └── Apple/               # AppleMusicMockAdapter.cs
│   ├── Configuration/           # Service-specific settings
│   └── Services/MusicServiceResolver.cs  # URL detection and routing
├── MusicShare.Contracts/        # Shared types, enums, and message contracts
│   ├── Messages/                # Event and command definitions
│   ├── ServiceType.cs
│   ├── ShareStatus.cs
│   └── SongStatus.cs
├── MusicShare.ServiceDefaults/  # Shared infrastructure (OpenTelemetry, health checks)
├── MusicShare.AppHost/          # .NET Aspire orchestrator for local development
│   └── AppHost.cs               # Local dev infrastructure + Azure config
├── .github/workflows/ci.yml     # CI/CD pipeline
├── azure.yaml                   # Azure Developer CLI configuration
└── MusicShare.slnx              # Solution file
```

## Development Commands

### Frontend (run from `MusicShare.Frontend/`)

```bash
npm install          # Install dependencies
npm run dev          # Start Next.js dev server
npm run build        # TypeScript check + production build
npm start            # Start production server
npm run lint         # Run ESLint
npm run lint:fix     # Auto-fix lint issues
```

### Backend (run from root)

```bash
dotnet restore MusicShare.slnx                         # Restore packages
dotnet build MusicShare.slnx                           # Build all projects
dotnet build MusicShare.slnx --configuration Release   # Release build
dotnet test MusicShare.slnx                            # Run tests
```

### Running Locally with Aspire

```bash
dotnet run --project MusicShare.AppHost           # Start full stack
```

This starts MongoDB, RabbitMQ, API, Worker, and Frontend with dev tooling (Mongo Express, RabbitMQ Management).

**Local endpoints:**
- API: `http://localhost:5078` or `https://localhost:7125`
- Frontend: `http://localhost:3000`
- Aspire Dashboard: Check console output for URL

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
- **Nullable**: Enabled globally - use nullable reference types properly.
- **Implicit usings**: Enabled globally.
- **MediatR pattern**: Commands implement `IRequest<TResponse>`, handlers implement `IRequestHandler<TRequest, TResponse>`.
- **Repository pattern**: `IRepository<T>` interfaces with MongoDB implementations.
- **Entities**: MongoDB attributes (`[BsonId]`, `[BsonElement]`, `[BsonRequired]`, `[BsonRepresentation]`) for persistence mapping.
- **MassTransit messages**: Use record types in `MusicShare.Contracts/Messages/`.

### TypeScript / React

- **Functional components**: Use function declarations with proper TypeScript types.
- **React Query**: Use `useQuery` for data fetching with polling where needed.
- **Routing**: Next.js App Router with dynamic routes (`[shareId]`).
- **Styling**: Tailwind CSS classes inline.
- **API client**: Centralized in `src/lib/api.ts` using fetch.
- **Types**: Define interfaces for API responses in `api.ts`.
- **TypeScript**: Strict mode enabled with `noUnusedLocals` and `noUnusedParameters`.

### API Design

- **Base path**: `/api/[controller]`
- **Endpoints**:
  - `POST /api/share` - Submit a music URL for resolution
  - `GET /api/share/{shareId}` - Get resolution results
- **ISR Revalidation**: `POST /api/revalidate` - Triggered by Worker on completion

## Key Files to Know

| Area | File | Purpose |
|------|------|---------|
| API Entry | `MusicShare.Api/Program.cs` | Service configuration, MediatR, MassTransit setup |
| API Controller | `MusicShare.Api/Controllers/ShareController.cs` | REST endpoints |
| CQRS Command | `MusicShare.Api/Commands/SubmitShareRequest.cs` | Share submission with handler |
| CQRS Query | `MusicShare.Api/Queries/GetShareResultQuery.cs` | Get share result with handler |
| Worker Entry | `MusicShare.Worker/Program.cs` | Background service config with MongoDB state |
| Saga | `MusicShare.Worker/Sagas/ShareRequestSaga.cs` | Async workflow orchestration + ISR trigger |
| Frontend Layout | `MusicShare.Frontend/src/app/layout.tsx` | Root layout, metadata, QueryClient |
| Share Page | `MusicShare.Frontend/src/app/share/[shareId]/page.tsx` | Dynamic result page |
| API Client | `MusicShare.Frontend/src/lib/api.ts` | Backend communication |
| Domain Entities | `MusicShare.Persistence/Entities/` | Song, ShareRequest, SongServiceLink |
| Music Adapters | `MusicShare.MusicAdapters/Services/Music/` | Spotify, Apple, YouTube integrations |
| Service Resolver | `MusicShare.MusicAdapters/Services/MusicServiceResolver.cs` | URL detection |
| Orchestration | `MusicShare.AppHost/AppHost.cs` | Local dev infrastructure |
| Message Contracts | `MusicShare.Contracts/Messages/` | Event and command definitions |
| PWA Manifest | `MusicShare.Frontend/public/manifest.json` | PWA + Web Share Target config |

## Data Flow

1. User submits URL on home page → `POST /api/share`
2. API validates URL, detects service type via `MusicServiceResolver`, creates `ShareRequest`, publishes `SongShareSubmitted` event
3. Worker's `ShareRequestSaga` orchestrates resolution:
   - State machine: `ResolvingMetadata` → `AwaitingServiceLinks` → `Completed`/`Failed`
   - Publishes `ResolveSourceMetadata` to extract metadata from source URL
   - On `SourceMetadataResolved`, publishes parallel `ResolveServiceLink` commands for other services
   - Consumers search for song on target services
   - On completion, triggers ISR revalidation via `/api/revalidate`
4. Frontend polls `GET /api/share/{shareId}` until status is `Completed`
5. Result page displays song info with links to all platforms

## Message Types

| Message | Type | Purpose |
|---------|------|---------|
| `SongShareSubmitted` | Event | Initiates workflow |
| `ResolveSourceMetadata` | Command | Extract metadata from source service |
| `SourceMetadataResolved` | Event | Metadata successfully extracted |
| `SourceMetadataFailed` | Event | Metadata extraction failed |
| `ResolveServiceLink` | Command | Search for song on target service |
| `ServiceLinkResolved` | Event | Song found on service |
| `ServiceLinkFailed` | Event | Song not found or error |

## Enums

**ServiceType** (in `MusicShare.Contracts/ServiceType.cs`):
- `Spotify = 1`
- `AppleMusic = 2`
- `YouTubeMusic = 3`

**ShareStatus**: `Pending`, `Processing`, `Completed`, `Failed`

**SongStatus**: `Pending`, `Resolved`, `PartiallyResolved`, `NotFound`, `Failed`, `Error`

## Environment Configuration

### Required for Full Functionality

| Variable | Description |
|----------|-------------|
| `Spotify__ClientId` | Spotify API client ID |
| `Spotify__ClientSecret` | Spotify API client secret |
| `YouTube__GeographicLocation` | YouTube region setting |
| `REVALIDATION_SECRET` | Shared secret for ISR revalidation |

### Azure Deployment Variables

| Variable | Description |
|----------|-------------|
| `AZURE_MONGODB_PASSWORD` | MongoDB password |
| `AZURE_RABBITMQ_USERNAME` | RabbitMQ username |
| `AZURE_RABBITMQ_PASSWORD` | RabbitMQ password |
| `AZURE_REVALIDATION_SECRET` | ISR revalidation secret |
| `AZURE_CUSTOM_DOMAIN` | Custom domain for frontend |
| `AZURE_CERTIFICATE_NAME` | SSL certificate name |

MongoDB and RabbitMQ connection details are handled by Aspire locally.

## CI/CD Pipeline

GitHub Actions workflow (`.github/workflows/ci.yml`):

**Jobs:**

1. **frontend**: Node 20
   - `npm ci` → `npm run lint` → `npm run build`

2. **backend**: .NET 10
   - `dotnet restore` → `dotnet build --configuration Release` → `dotnet test`

3. **deploy**: Conditional (push to main/develop only)
   - Azure login, `azd provision`, `azd deploy`

## PWA Features

MusicShare is a Progressive Web App with:
- **Web Share Target API**: Can receive shared URLs from other apps
- **Service Worker**: Via Serwist for offline support and caching
- **Installable**: Purple theme (#a855f7), standalone display mode
- **Icons**: Multiple sizes for Android/iOS home screen

## Testing Guidelines

- Backend tests: `dotnet test MusicShare.slnx`
- Frontend lint: `npm run lint` (no test runner configured yet)
- Always run lint before committing frontend changes

## Common Tasks

### Adding a new music service

1. Create adapter in `MusicShare.MusicAdapters/Services/Music/{ServiceName}/`
2. Implement `IMusicServiceAdapter` interface
3. Add enum value to `ServiceType` in `MusicShare.Contracts/`
4. Register in DI via `MusicAdapterExtensions`
5. Add consumer in Worker for the new service
6. Update Saga to include the new service in parallel resolution
7. Add frontend link component in `src/components/MusicLinks/`

### Adding a new API endpoint

1. Add Command/Query in `MusicShare.Api/Commands/` or `MusicShare.Api/Queries/`
2. Create handler implementing `IRequestHandler<TRequest, TResponse>`
3. Add controller method using `_mediator.Send()`
4. Update frontend `src/lib/api.ts` with new types and fetch method

### Modifying database entities

1. Update entity in `MusicShare.Persistence/Entities/`
2. Ensure `[BsonElement]` attributes match MongoDB field names
3. Update repository if needed
4. Consider migration strategy for existing data

### Adding a new message type

1. Create record type in `MusicShare.Contracts/Messages/`
2. Create consumer in `MusicShare.Worker/Consumers/`
3. Register consumer in Worker's `Program.cs` MassTransit configuration
4. Update Saga if message is part of workflow

## Observability

OpenTelemetry is configured for:
- ASP.NET Core instrumentation
- HTTP client instrumentation
- Runtime instrumentation
- Export to Aspire Dashboard (local) / Azure Monitor (production)

## Notes

- Apple Music adapter is currently a mock implementation
- Frontend uses ISR (Incremental Static Regeneration) with on-demand revalidation
- Saga state is persisted in MongoDB with concurrency handling

## Project Agents

Use specialized agents for domain-specific tasks:

### react-component-expert
Use for React/Next.js frontend work including:
- Creating or refactoring React components in `MusicShare.Frontend/src/components/`
- Next.js App Router pages and layouts
- Tailwind CSS styling
- React Query integration
- TypeScript types for frontend

### infra-devops-owner
Use for infrastructure and DevOps tasks including:
- .NET Aspire AppHost configuration (`MusicShare.AppHost/`)
- GitHub Actions CI/CD workflows (`.github/workflows/`)
- Service wiring and dependency injection in `Program.cs` files
- Environment variables and secrets management
- Azure Container Apps deployment configuration
- MassTransit/RabbitMQ configuration

### react-native-engineer
Use for React Native mobile app development including:
- Building mobile UI components and screens
- Navigation flows and routing
- Custom hooks and state management
- Mobile-specific styling and animations
- Performance optimization for mobile
