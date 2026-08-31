# .NET Aspire — Local Dev & Cloud Deployment

> **Slide talking points:** Aspire is the thing your team is probably not using yet. This is a good "why would I care?" moment.

---

## What Is .NET Aspire?

- Microsoft's answer to "local dev is a pain when you have multiple services"
- One command (`dotnet run --project MusicShare.AppHost`) starts:
  - MongoDB
  - RabbitMQ (with management UI)
  - Mongo Express (MongoDB admin UI)
  - The .NET API
  - The Next.js frontend
  - The Aspire Dashboard
- **Same AppHost.cs config drives Azure deployment** — no separate Bicep/Terraform needed

---

## The AppHost.cs — One File to Rule Them All

```csharp
// Local dev: spin up full instances
// Production: use connection strings to external services
var mongodb = ConfigureMongoDb();
var rabbitmq = ConfigureRabbitMQ();

var api = builder.AddProject<Projects.MusicShare_Api>("api")
    .WithReference(mongodb)
    .WithReference(rabbitmq)
    .WaitFor(mongodb)     // ← startup ordering built in
    .WaitFor(rabbitmq);

var frontend = builder.AddJavaScriptApp("frontend", "../MusicShare.Frontend")
    .WithReference(api)
    .WaitFor(api);

api.WithReference(frontend);  // ← bidirectional: API needs frontend URL for ISR revalidation
```

---

## Local vs Production — Same Config, Different Behavior

```csharp
private static IResourceBuilder<IResourceWithConnectionString> ConfigureMongoDb()
{
    if (!builder.ExecutionContext.IsPublishMode)
    {
        // Local: full MongoDB + Mongo Express admin UI
        return builder.AddMongoDB("mongodb").WithMongoExpress();
    }
    else
    {
        // Production: just use a connection string
        return builder.AddConnectionString("mongodb");
    }
}
```

**The same pattern applies to RabbitMQ.** Flip between local infra and cloud managed services with `IsPublishMode`.

---

## What You Get Locally

| Tool | What It Is | URL |
|------|-----------|-----|
| Aspire Dashboard | Traces, logs, health checks for all services | auto-detected in console |
| Mongo Express | Browse/edit MongoDB collections | http://localhost:8081 |
| RabbitMQ Management | Monitor queues, messages, consumers | http://localhost:15672 |
| API | The .NET REST API | http://localhost:5078 |
| Frontend | The Next.js app | http://localhost:3000 |

**One command. Everything running. No Docker Compose file.**

---

## Service Discovery — No Hardcoded URLs

- Aspire injects environment variables for service endpoints automatically
- The frontend gets `services__api__https__0` → points to the API
- The API gets the frontend URL → for ISR revalidation callbacks
- In production, Azure Container Apps handles the same DNS resolution

```csharp
// Frontend doesn't hardcode the API URL — Aspire injects it
.WithReference(api)
```

---

## Secrets Management

```csharp
// Secrets declared in AppHost, injected as environment variables
var spotifyClientId = builder.AddParameter("spotify-clientid", secret: true);
var revalidationSecret = builder.AddParameter("revalidation-secret", secret: true);

builder.AddProject<Projects.MusicShare_Api>("api")
    .WithEnvironment("Spotify__ClientId", spotifyClientId)
    .WithEnvironment("Frontend__RevalidationSecret", revalidationSecret);
```

- Local: secrets stored in `appsettings.Development.json` or user secrets
- Production: Azure Key Vault / Container App secrets, same variable names

---

## Azure Deployment — Aspire Handles the Bicep

```csharp
// Autoscaling declared right in the AppHost
api.PublishAsAzureContainerApp((module, app) =>
{
    app.Template.Scale.MinReplicas = apiMinReplicas;  // clamped to at least 1 for the weekly UTC refresh
    app.Template.Scale.MaxReplicas = apiMaxReplicas;  // clamped to at least the API minimum
});

// Custom domain + SSL certificate
frontend.PublishAsAzureContainerApp((module, app) =>
{
    app.ConfigureCustomDomain(customDomain, certificateName);
    app.Template.Scale.MinReplicas = frontendMinReplicas;
    app.Template.Scale.MaxReplicas = frontendMaxReplicas;
});
```

- No hand-written Bicep or ARM templates
- Aspire generates the infrastructure as code from the AppHost
- `azd provision` deploys everything to Azure Container Apps
- The API remains private but keeps one replica running for the Sunday 00:00 UTC public-metrics refresh. `AZURE_API_MIN_REPLICAS=0` is clamped to one, which adds a small baseline production cost. The public frontend's replica settings are unchanged and can still scale to zero.

---

## The Aspire Dashboard (Underrated)

When running locally, the Aspire Dashboard gives you:
- **Structured logs** from all services in one place (no switching terminal windows)
- **Distributed traces** — see the full saga execution as a trace waterfall
- **Health checks** — green/red status per service
- **Resource graph** — which services depend on which

This is what you'd normally need Jaeger, Grafana, and a log aggregator to get.

---

## Why This Matters for the Team

- **New dev onboarding:** `dotnet run --project MusicShare.AppHost` — that's it
- **No "works on my machine":** infra is defined in code, same for everyone
- **Production parity:** local and prod use the same orchestration config
- **No Bicep expertise needed:** Aspire generates the Azure infrastructure
