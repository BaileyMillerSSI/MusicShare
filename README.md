# MusicShare

MusicShare turns a track URL into a reusable page with links to the same song on other streaming services. It is a full-stack .NET Aspire application with a Next.js progressive web app (PWA), an ASP.NET Core API, asynchronous link resolution, and MongoDB persistence.

## What it does

- Accepts pasted links and links shared to the installed PWA.
- Resolves source metadata, then searches other providers in parallel.
- Uses confidence scoring to reject likely mismatches.
- Returns a stable `/share/{shareId}` page that polls while processing and supports native sharing.
- Reuses existing results when the same provider track is submitted again.

### Provider status

| Provider | Source metadata | Target search |
| --- | --- | --- |
| Spotify | Supported; requires API credentials | Supported |
| YouTube Music | Supported | Supported |
| Apple Music | Scaffolding only; no adapter is registered | Scaffolding only |

The public form currently advertises Spotify and YouTube Music. Apple Music contracts, UI components, and a consumer shell exist, but Apple Music links are not operational yet.

## Architecture

```text
Browser / installed PWA
        |
        v
Next.js frontend and API proxy (the only public service)
        |
        v
ASP.NET Core API -- MediatR commands and queries -- MongoDB
        |
        v
RabbitMQ -- MassTransit saga -- provider consumers
        |                              |
        +------------------------------+
                       |
                       v
          Spotify and YouTube Music
```

Submitting a URL creates a short share ID and publishes a message. The MongoDB-backed saga resolves source metadata, fans out searches for the remaining providers, records successful links, and marks the request complete after every search responds. The frontend polls pending requests; completion also triggers protected Next.js cache revalidation.

Aspire owns service discovery, local containers, health checks, resilience, and OpenTelemetry. Backend resources remain on Aspire-internal networking; browser API traffic is routed through the frontend proxy.

## Repository layout

| Path | Responsibility |
| --- | --- |
| `MusicShare.Frontend` | Next.js App Router PWA, frontend API proxy, React Query UI, and Vitest tests |
| `MusicShare.Api` | ASP.NET Core endpoints, MediatR operations, MassTransit consumers, and share saga |
| `MusicShare.Services` | Share workflow services, provider adapters, URL parsing, and confidence scoring |
| `MusicShare.Persistence` | MongoDB context, entities, and repositories |
| `MusicShare.Contracts` | Shared enums and asynchronous message contracts |
| `MusicShare.ServiceDefaults` | Aspire defaults, telemetry, health checks, persistence, and messaging registration |
| `MusicShare.AppHost` | Local orchestration and Azure Container Apps topology |
| `MusicShare.Tests` | Backend unit tests and Aspire integration-test foundation |
| `docs` | Deeper architecture, Aspire, PWA, CI/CD, and project-history notes |

## Run locally

### Prerequisites

- .NET 10 SDK
- Node.js 20 or later with npm
- Docker Desktop or another Docker-compatible container runtime
- A Spotify developer application with a client ID and client secret

Install frontend dependencies:

```bash
cd MusicShare.Frontend
npm ci
cd ..
```

Store the local AppHost parameters with .NET user secrets. Replace the Spotify placeholders; the other values may be any suitably strong local-only credentials shared by the services.

```bash
dotnet user-secrets set --project MusicShare.AppHost "Parameters:spotify-clientid" "<client-id>"
dotnet user-secrets set --project MusicShare.AppHost "Parameters:spotify-clientsecret" "<client-secret>"
dotnet user-secrets set --project MusicShare.AppHost "Parameters:rabbitmq-username" "<local-username>"
dotnet user-secrets set --project MusicShare.AppHost "Parameters:rabbitmq-password" "<local-password>"
dotnet user-secrets set --project MusicShare.AppHost "Parameters:revalidation-secret" "<long-random-value>"
```

Start the complete application:

```bash
dotnet run --project MusicShare.AppHost
```

Aspire starts MongoDB with Mongo Express, RabbitMQ, the API, and the frontend. Open the frontend or Aspire Dashboard URL printed in the terminal; Aspire may assign ports dynamically.

## Build and test

Run backend validation from the repository root:

```bash
dotnet build MusicShare.slnx
dotnet test MusicShare.Tests/MusicShare.Tests.csproj
```

Run frontend validation from `MusicShare.Frontend`:

```bash
npm run lint
npm test
npm run build
```

CI runs the same frontend lint/test/build and backend restore/build/test gates for pull requests to `main`.

## Configuration and deployment

Non-secret provider defaults live in `MusicShare.Api/appsettings.json`. Aspire injects service endpoints, MongoDB and RabbitMQ connection strings, Spotify credentials, the shared cache-revalidation secret, and the explicit production CORS origins. `AZURE_FRONTEND_ORIGIN` preserves the MusicShare origin and `AZURE_RESUME_ORIGIN` allows `https://resume.baileymiller.dev`; both are provisioned as `Cors__AllowedOrigins` entries for the API. Do not commit real credentials.

`azure.yaml` points Azure Developer CLI at `MusicShare.AppHost`, which publishes the frontend to Azure Container Apps with an external endpoint while keeping the API and infrastructure internal. After configuring an `azd` environment and the required production parameters, provision and deploy with:

```bash
azd up
```

Replica counts can be set with `AZURE_API_MIN_REPLICAS`, `AZURE_API_MAX_REPLICAS`, `AZURE_FRONTEND_MIN_REPLICAS`, and `AZURE_FRONTEND_MAX_REPLICAS`.

## Contributing

Keep changes focused and cover backend behavior with xUnit or frontend behavior with colocated Vitest tests. Use short Conventional Commit subjects, target pull requests to `main`, and include screenshots for UI or PWA changes. See `AGENTS.md` for the repository's detailed coding and contribution conventions.
