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
- Always open a PR targeting main
- When creating a PR from a GitHub issue, reference the issue in the PR description (e.g., "Closes #31") to create an automatic link

## Architecture

**Stack:**
- **Backend**: .NET 10 with ASP.NET Core API
- **Frontend**: Next.js 16 + React 19 + TypeScript
- **Database**: MongoDB
- **Messaging**: RabbitMQ (via MassTransit)
- **Orchestration**: .NET Aspire hosting topology with Azure Container Apps as the production runtime

**Key Patterns**:
- CQRS with MediatR for API
- Saga pattern with MassTransit for async processing
- Repository pattern for data access
- PWA with Web Share Target API support
- Next.js frontend is the only public-facing service; API and infrastructure resources stay Aspire-internal

## Project Structure

```
MusicShare/
├── MusicShare.Api/              # REST API (Controllers, Commands, Queries, Consumers, Sagas)
│   ├── Controllers/             # ShareController.cs - REST endpoints
│   ├── Commands/                # CQRS commands (SubmitShare.cs - static class with nested Request/Handler/Response)
│   ├── Queries/                 # CQRS queries (GetShareResult.cs - static class with nested Query/Handler/Result)
│   ├── Consumers/               # MassTransit consumers (SourceMetadataConsumer, service link consumers)
│   ├── Sagas/ShareRequest/      # ShareRequestSaga.cs, state, activities
│   │   ├── ShareRequestSaga.cs  # State machine orchestrator
│   │   ├── ShareRequestSagaState.cs
│   │   └── Activities/          # CompleteSagaActivity, FailSagaActivity
│   └── Program.cs               # Service configuration, MassTransit + saga setup
├── MusicShare.Services/         # Domain services, music adapters, models
│   ├── Configuration/           # Service-specific settings (Spotify, YouTube, Frontend)
│   ├── Models/                  # Response DTOs (ServiceLink, ShareResultResponse, SongDetails, SubmitShareResponse)
│   ├── Services/                # Domain service interfaces and implementations
│   │   ├── IMusicServiceResolver.cs   # URL detection and adapter routing
│   │   ├── IShareRequestService.cs    # Share request creation and retrieval
│   │   ├── IShareStatusService.cs     # Share status updates
│   │   ├── ISongService.cs            # Song status updates
│   │   ├── MusicServiceResolver.cs
│   │   ├── ShareRequestService.cs
│   │   ├── ShareStatusService.cs
│   │   ├── SongService.cs
│   │   ├── IFrontendRevalidateService.cs  # ISR revalidation interface
│   │   ├── FrontendRevalidateService.cs   # ISR revalidation implementation
│   │   └── Music/               # Music service adapters
│   │       ├── IMusicServiceAdapter.cs
│   │       ├── Spotify/         # SpotifyMusicService, models, auth handler
│   │       ├── YouTube/         # YouTubeMusicAdapter, mock adapter
│   │       └── Apple/           # AppleMusicMockAdapter
│   └── DependencyInjection.cs   # AddDomainServices() + AddFrontendRevalidateService()
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
│   ├── IMusicShareDbContext.cs  # DB context interface
│   └── DependencyInjection.cs   # DI registration
├── MusicShare.Contracts/        # Shared types, enums, and message contracts
│   ├── Messages/                # Event and command definitions
│   ├── ServiceType.cs
│   ├── ShareStatus.cs
│   └── SongStatus.cs
├── MusicShare.ServiceDefaults/  # Shared infrastructure (OpenTelemetry, health checks, DI wiring)
├── MusicShare.AppHost/          # .NET Aspire orchestrator for local development
│   └── AppHost.cs               # Local dev infrastructure + Azure config
├── MusicShare.Tests/            # xUnit test project
│   ├── Unit/                    # Unit tests for handlers, services, business logic
│   ├── Integration/             # Aspire integration tests
│   │   └── AspireIntegrationTestBase.cs  # Base class for integration tests
│   └── GlobalUsings.cs          # Shared using directives for tests
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

This starts MongoDB, RabbitMQ, API, and Frontend with dev tooling (Mongo Express, RabbitMQ Management).

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

## MCP Tools - ALWAYS PREFER

When working with .NET codebase, the following MCP tools (`mcp__vs-mcp__*`) provide semantic analysis via Roslyn and should ALWAYS be used instead of generic tools:

| Instead of | Use MCP Tool | Why |
|------------|--------------|-----|
| `Grep` for symbols | `FindSymbols`, `FindSymbolUsages` | 10x faster, semantic accuracy |
| `Glob` to explore projects | `GetSolutionTree` | Understands project structure |
| Reading files to find code | `FindSymbolDefinition` then `Read` | Navigate directly to definitions |
| Searching for method calls | `GetMethodCallers`, `GetMethodCalls` | Find all references with context |
| Reading file structure | `GetDocumentOutline` | Parse classes, methods, properties |
| Finding inheritance | `GetInheritance` | See base types and derived types |
| Code navigation | `GetSymbolAtLocation` | Jump to symbol definition at cursor |
| Refactoring names | `RenameSymbol` | Semantic rename across solution |
| Building projects | `ExecuteCommand` | Compile projects with Roslyn analysis |

**Example**: To find where `ShareRequestService` is used, use `FindSymbolUsages` with `symbolName: "ShareRequestService"` instead of `Grep`.

## Code Conventions

### C# / .NET

- **Naming**: PascalCase for classes, methods, properties. Interfaces prefix with `I`.
- **File-scoped namespaces**: Use `namespace X;` style.
- **Primary constructors**: Use for dependency injection (e.g., `public class X(IDep dep)`).
- **Nullable**: Enabled globally - use nullable reference types properly.
- **Implicit usings**: Enabled globally.
- **File organization**: One file per class, except for MediatR Commands/Queries which use nested types.
- **MediatR pattern**: Commands and Queries use a static class container with nested types:
  - Static outer class named for the operation (e.g., `SubmitShare`, `GetShareResult`)
  - Nested `Request`/`Query` record implementing `IRequest<Response/Result>`
  - Nested `Handler` class implementing `IRequestHandler<Request/Query, Response/Result>`
  - Nested `Response`/`Result` record with factory methods
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
- **ISR Revalidation**: `POST /api/revalidate` - Triggered by the saga on completion, authenticated via `X-API-KEY` header

## Key Files to Know

| Area | File | Purpose |
|------|------|---------|
| API Entry | `MusicShare.Api/Program.cs` | Service configuration, MediatR, MassTransit + saga setup |
| API Controller | `MusicShare.Api/Controllers/ShareController.cs` | REST endpoints |
| CQRS Command | `MusicShare.Api/Commands/SubmitShare.cs` | Static class with nested Request/Handler/Response |
| CQRS Query | `MusicShare.Api/Queries/GetShareResult.cs` | Static class with nested Query/Handler/Result |
| Saga | `MusicShare.Api/Sagas/ShareRequest/ShareRequestSaga.cs` | Async workflow orchestration |
| Consumers | `MusicShare.Api/Consumers/` | MassTransit message consumers for metadata and service links |
| Frontend Layout | `MusicShare.Frontend/src/app/layout.tsx` | Root layout, metadata, QueryClient |
| Share Page | `MusicShare.Frontend/src/app/share/[shareId]/page.tsx` | Dynamic result page |
| API Client | `MusicShare.Frontend/src/lib/api.ts` | Backend communication |
| Domain Entities | `MusicShare.Persistence/Entities/` | Song, ShareRequest, SongServiceLink |
| Domain Services | `MusicShare.Services/Services/` | ShareRequestService, ShareStatusService, SongService, FrontendRevalidateService |
| Music Adapters | `MusicShare.Services/Services/Music/` | Spotify, Apple, YouTube integrations |
| Service Resolver | `MusicShare.Services/Services/MusicServiceResolver.cs` | URL detection and adapter routing |
| Frontend Config | `MusicShare.Services/Configuration/FrontendSettings.cs` | Frontend URL and revalidation secret settings |
| DI Registration | `MusicShare.Services/DependencyInjection.cs` | AddDomainServices() for all services, adapters + revalidation |
| Service Wiring | `MusicShare.ServiceDefaults/Extensions.cs` | Calls AddPersistence() + AddDomainServices() |
| Orchestration | `MusicShare.AppHost/AppHost.cs` | Local dev infrastructure |
| Message Contracts | `MusicShare.Contracts/Messages/` | Event and command definitions |
| PWA Manifest | `MusicShare.Frontend/public/manifest.json` | PWA + Web Share Target config |
| Test Project | `MusicShare.Tests/MusicShare.Tests.csproj` | xUnit tests with Aspire integration |
| Integration Base | `MusicShare.Tests/Integration/AspireIntegrationTestBase.cs` | Base class for Aspire integration tests |

## Data Flow

1. User submits URL on home page → `POST /api/share`
2. API validates URL, detects service type via `MusicServiceResolver`, creates `ShareRequest`, publishes `SongShareSubmitted` event
3. API's `ShareRequestSaga` orchestrates resolution:
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
| `Frontend__RevalidationSecret` | Shared secret for ISR revalidation (sent as `X-API-KEY` header) |
| `Cors__AllowedOrigins__0` | Production frontend origin allowed to call browser-facing API routes |

### Azure Deployment Variables

| Variable | Description |
|----------|-------------|
| `AZURE_MONGODB_PASSWORD` | MongoDB password |
| `AZURE_RABBITMQ_USERNAME` | RabbitMQ username |
| `AZURE_RABBITMQ_PASSWORD` | RabbitMQ password |
| `AZURE_REVALIDATION_SECRET` | ISR revalidation secret |
| `AZURE_FRONTEND_ORIGIN` | Production frontend origin allowed by API CORS |
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

### Backend Testing

**Test Project**: `MusicShare.Tests/`

**Libraries**:
- **xUnit v3 (3.2.2)**: Test framework
- **FluentAssertions 7.0.0**: Readable assertion syntax
- **Moq 4.20.72**: Mocking framework
- **Autofac.Extras.Moq**: Auto-mocking container via `AutoMock.GetLoose()`
- **Aspire.Hosting.Testing 13.1.0**: Integration testing with full Aspire stack

**Running Tests**:
```bash
dotnet test MusicShare.slnx                            # Run all tests
dotnet test MusicShare.Tests/MusicShare.Tests.csproj   # Run test project only
```

**Test Organization**:
- `Unit/` - Unit tests for handlers, services, and business logic
- `Integration/` - Integration tests using Aspire distributed application

**Writing Tests**:
- Unit tests: Use `AutoMock.GetLoose()` for dependency resolution, FluentAssertions for assertions
- Integration tests: Extend `AspireIntegrationTestBase` to spin up the full Aspire application
- Global usings for xUnit, FluentAssertions, Moq, and Autofac.Extras.Moq are in `GlobalUsings.cs`

**Test Naming Convention**:
- All unit test methods MUST use the `ItWill` prefix (e.g., `ItWillReturnSuccessForValidSpotifyUrl`, `ItWillReturnNullForUnsupportedUrl`)
- Do NOT use the `MethodName_Scenario_Expected` pattern

**AutoMock Pattern** (required for unit tests):
- **No top-level properties**: No `_mocker`, `_sut`, or any class-level fields in test classes
- Each test method creates its own `AutoMock` via `using var mock = AutoMock.GetLoose();`
- SUT created per-test via `var sut = mock.Create<T>();`
- Access mocks inline via `mock.Mock<IFoo>()` for setup and verification
- For concrete 3rd-party dependencies that AutoMock cannot construct, use `mock.Provide(instance)` before `mock.Create<T>()`
- Non-constructor dependencies (e.g., `ConsumeContext<T>`) should be created locally in tests or via helper methods

```csharp
// Standard AutoMock pattern - no top-level properties
public class MyHandlerTests
{
    [Fact]
    public async Task ItWillReturnExpectedResultForValidInput()
    {
        using var mock = AutoMock.GetLoose();
        mock.Mock<IMyService>()
            .Setup(x => x.DoWork())
            .ReturnsAsync("result");

        var sut = mock.Create<MyHandler>();
        var result = await sut.Handle(new MyRequest(), CancellationToken.None);

        result.Should().Be("result");
    }
}
```

**MassTransit Saga State Machine Tests** (uses test harness, NOT AutoMock):
- Use `AddMassTransitTestHarness` with `ServiceCollection` for DI-based setup
- Register saga dependencies and activities as services (not via MassTransit config)
- `MassTransit.Testing` namespace is in the main `MassTransit` package (no separate NuGet)
- `Finalize()` moves saga to `Final` state - check `StateMachine.Final` not custom states
- `ContainsInState` returns the saga state type directly (no `.Saga` wrapper)
- Wait for message consumption before asserting: `(await sagaHarness.Consumed.Any<T>()).Should().BeTrue()`

### Frontend Testing

- Frontend lint: `npm run lint` (no test runner configured yet)
- Always run lint before committing frontend changes

## Common Tasks

### Adding a new music service

1. Create adapter in `MusicShare.Services/Services/Music/{ServiceName}/`
2. Implement `IMusicServiceAdapter` interface
3. Add enum value to `ServiceType` in `MusicShare.Contracts/`
4. Register in DI via `MusicShare.Services/DependencyInjection.cs`
5. Add consumer in `MusicShare.Api/Consumers/` for the new service
6. Update Saga to include the new service in parallel resolution
7. Add frontend link component in `src/components/MusicLinks/`

### Adding a new API endpoint

1. Create a new file in `MusicShare.Api/Commands/` or `MusicShare.Api/Queries/` (e.g., `GetUserHistory.cs`)
2. Define a static class containing:
   - Nested `Query`/`Request` record implementing `IRequest<Result/Response>`
   - Nested `Handler` class with primary constructor for DI, implementing `IRequestHandler<Query/Request, Result/Response>`
   - Nested `Result`/`Response` record with factory methods for success/failure cases
3. Add controller method using `_mediator.Send(new FeatureName.Query(...))`
4. Update frontend `src/lib/api.ts` with new types and fetch method

### Modifying database entities

1. Update entity in `MusicShare.Persistence/Entities/`
2. Ensure `[BsonElement]` attributes match MongoDB field names
3. Update repository if needed
4. Consider migration strategy for existing data

### Adding a new message type

1. Create record type in `MusicShare.Contracts/Messages/`
2. Create consumer in `MusicShare.Api/Consumers/`
3. Consumers are auto-registered via assembly scanning in `MusicShare.Api/Program.cs`
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
