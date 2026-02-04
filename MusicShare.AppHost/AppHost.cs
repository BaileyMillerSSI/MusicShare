var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var mongodb = builder.AddMongoDB("mongodb");

var messagingUsername = builder.AddParameter("rabbitmq-username", secret: true);
var messagingPassword = builder.AddParameter("rabbitmq-password", secret: true);

var rabbitmq = builder.AddRabbitMQ("rabbitmq", messagingUsername, messagingPassword);

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
    .WaitFor(mongodb)
    .WaitFor(rabbitmq);

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
    .WithEnvironment("RevalidationSecret", revalidationSecret)
    .WaitFor(mongodb)
    .WaitFor(rabbitmq)
    .WaitFor(frontend);

// Dev tooling only when running locally
if (!builder.ExecutionContext.IsPublishMode)
{
    mongodb.WithMongoExpress();
    rabbitmq.WithManagementPlugin();
}

builder.Build().Run();
