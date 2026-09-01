var builder = DistributedApplication.CreateBuilder(args);

// The API owns the daily UTC-midnight metrics refresh, so published deployments need one replica
// available even when the frontend is idle. The frontend may still scale to zero.
var apiMinReplicas = Math.Max(1, int.TryParse(Environment.GetEnvironmentVariable("AZURE_API_MIN_REPLICAS"), out var apiMin) ? apiMin : 1);
var apiMaxReplicas = Math.Max(apiMinReplicas, int.TryParse(Environment.GetEnvironmentVariable("AZURE_API_MAX_REPLICAS"), out var apiMax) ? apiMax : 1);
var frontendMinReplicas = int.TryParse(Environment.GetEnvironmentVariable("AZURE_FRONTEND_MIN_REPLICAS"), out var feMin) ? feMin : 0;
var frontendMaxReplicas = int.TryParse(Environment.GetEnvironmentVariable("AZURE_FRONTEND_MAX_REPLICAS"), out var feMax) ? feMax : 1;

// Infrastructure
var mongodb = ConfigureMongoDb();
var rabbitmq = ConfigureRabbitMQ();

// Spotify credentials
var spotifyClientId = builder.AddParameter("spotify-clientid", secret: true);
var spotifyClientSecret = builder.AddParameter("spotify-clientsecret", secret: true);

// Shared secret for the Next.js revalidation endpoint
var revalidationSecret = builder.AddParameter("revalidation-secret", secret: true);
var maintenanceSecret = builder.AddParameter("maintenance-secret", secret: true);

var corsAllowedOrigin = builder.ExecutionContext.IsPublishMode
    ? builder.AddParameter("frontend-origin")
    : null;
var resumeCorsAllowedOrigin = builder.ExecutionContext.IsPublishMode
    ? builder.AddParameter("resume-origin")
    : null;

// Backend services
var api = builder.AddProject<Projects.MusicShare_Api>("api")
    .WithReference(mongodb)
    .WithReference(rabbitmq)
    .WithEnvironment("Spotify__ClientId", spotifyClientId)
    .WithEnvironment("Spotify__ClientSecret", spotifyClientSecret)
    .WithEnvironment("Frontend__RevalidationSecret", revalidationSecret)
    .WithEnvironment("Maintenance__Secret", maintenanceSecret)
    .WaitFor(mongodb)
    .WaitFor(rabbitmq)
    .PublishAsAzureContainerApp((module, app) =>
    {
        app.Template.Scale.MinReplicas = apiMinReplicas;
        app.Template.Scale.MaxReplicas = apiMaxReplicas;
    });

// Frontend (Next.js)
var frontend = builder.AddJavaScriptApp("frontend", "../MusicShare.Frontend")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("REVALIDATION_SECRET", revalidationSecret)
    .WithEnvironment("MAINTENANCE_SECRET", maintenanceSecret)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

if (corsAllowedOrigin != null && resumeCorsAllowedOrigin != null)
{
    api.WithEnvironment("Cors__AllowedOrigins__0", corsAllowedOrigin);
    api.WithEnvironment("Cors__AllowedOrigins__1", resumeCorsAllowedOrigin);
}

// API references frontend for ISR revalidation
api.WithReference(frontend);

// Dev tooling only when running locally
if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureContainerAppEnvironment("aca-env")
        .WithAzdResourceNaming();

    IResourceBuilder<ParameterResource> customDomain = builder.AddParameter("custom-domain");
    IResourceBuilder<ParameterResource> certificateName = null!;

    var azureCertName = Environment.GetEnvironmentVariable("AZURE_CERTIFICATE_NAME");
    if (!string.IsNullOrWhiteSpace(azureCertName))
    {
        certificateName = builder.AddParameter("certificate-name", azureCertName);
    }

    frontend = frontend.PublishAsDockerFile()
        .PublishAsAzureContainerApp((module, app) =>
        {
            if (!string.IsNullOrWhiteSpace(azureCertName) && certificateName != null)
            {
                app.ConfigureCustomDomain(customDomain, certificateName);
            }
            app.Template.Scale.MinReplicas = frontendMinReplicas;
            app.Template.Scale.MaxReplicas = frontendMaxReplicas;
        });
}

builder.Build().Run();

IResourceBuilder<IResourceWithConnectionString> ConfigureMongoDb()
{
    if (!builder.ExecutionContext.IsPublishMode)
    {
        return builder.AddMongoDB("mongodb").WithMongoExpress();
    }
    else
    {
        return builder.AddConnectionString("mongodb");
    }
}

IResourceBuilder<IResourceWithConnectionString> ConfigureRabbitMQ()
{
    var messagingUsername = builder.AddParameter("rabbitmq-username", secret: true);
    var messagingPassword = builder.AddParameter("rabbitmq-password", secret: true);

    if (!builder.ExecutionContext.IsPublishMode)
    {
        return builder.AddRabbitMQ("rabbitmq", messagingUsername, messagingPassword);
    }
    else
    {
        return builder.AddConnectionString("rabbitmq");
    }
}
