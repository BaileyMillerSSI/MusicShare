var builder = DistributedApplication.CreateBuilder(args);

// Scaling configuration (defaults to 0/1 for development, configurable via environment variables for production)
var apiMinReplicas = int.TryParse(Environment.GetEnvironmentVariable("AZURE_API_MIN_REPLICAS"), out var apiMin) ? apiMin : 0;
var apiMaxReplicas = int.TryParse(Environment.GetEnvironmentVariable("AZURE_API_MAX_REPLICAS"), out var apiMax) ? apiMax : 1;
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

// Backend services
var api = builder.AddProject<Projects.MusicShare_Api>("api")
    .WithReference(mongodb)
    .WithReference(rabbitmq)
    .WithEnvironment("Spotify__ClientId", spotifyClientId)
    .WithEnvironment("Spotify__ClientSecret", spotifyClientSecret)
    .WithEnvironment("Frontend__RevalidationSecret", revalidationSecret)
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
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

// Worker — references frontend so Aspire injects its URL; receives the revalidation secret
builder.AddProject<Projects.MusicShare_Worker>("worker")
    .WithReference(mongodb)
    .WithReference(rabbitmq)
    .WithReference(frontend)
    .WithEnvironment("Spotify__ClientId", spotifyClientId)
    .WithEnvironment("Spotify__ClientSecret", spotifyClientSecret)
    .WithEnvironment("Frontend__RevalidationSecret", revalidationSecret)
    .WaitFor(mongodb)
    .WaitFor(rabbitmq)
    .WaitFor(frontend);

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
        return builder.AddConnectionString("mongodb", "AZURE_MONGODB_URI");
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
        return builder.AddConnectionString("rabbitmq", "AZURE_RABBITMQ_URL");
    }
}
