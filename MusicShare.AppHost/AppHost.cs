using Aspire.Hosting.Yarp.Transforms;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var mongodb = builder.AddMongoDB("mongodb");

var messagingUsername = builder.AddParameter("rabbitmq-username", secret: true);
var messagingPassword = builder.AddParameter("rabbitmq-password", secret: true);

var rabbitmq = builder.AddRabbitMQ("rabbitmq", messagingUsername, messagingPassword);

// Spotify credentials
var spotifyClientId = builder.AddParameter("spotify-clientid", secret: true);
var spotifyClientSecret = builder.AddParameter("spotify-clientsecret", secret: true);

// Backend services
var api = builder.AddProject<Projects.MusicShare_Api>("api")
    .WithReference(mongodb)
    .WithReference(rabbitmq)
    .WithEnvironment("Spotify__ClientId", spotifyClientId)
    .WithEnvironment("Spotify__ClientSecret", spotifyClientSecret)
    .WaitFor(mongodb)
    .WaitFor(rabbitmq);

builder.AddProject<Projects.MusicShare_Worker>("worker")
    .WithReference(mongodb)
    .WithReference(rabbitmq)
    .WithEnvironment("Spotify__ClientId", spotifyClientId)
    .WithEnvironment("Spotify__ClientSecret", spotifyClientSecret)
    .WaitFor(mongodb)
    .WaitFor(rabbitmq);

// Frontend
var frontend = builder.AddViteApp("frontend", "../MusicShare.Frontend")
    .WithReference(api)
    .WaitFor(api)
    .WithEndpoint(endpointName: "http", endpoint =>
    {
        endpoint.Port = builder.ExecutionContext.IsRunMode ?
                        5173 : null;
    });

// Add dev tooling only when running locally (not publishing)
if (!builder.ExecutionContext.IsPublishMode)
{
    mongodb.WithMongoExpress();
    rabbitmq.WithManagementPlugin();
}else
{
    var yarp = builder.AddYarp("frontend-server")
        .WithExternalHttpEndpoints()
        .PublishWithStaticFiles(frontend);

    yarp.WithConfiguration(c =>
    {
        // Always proxy /api requests to backend
        c.AddRoute("api/{**catch-all}", api);
        // SPA fallback - route all other requests to root for client-side routing
        c.AddRoute("{**catch-all}", yarp).WithTransformPathSet("/");
    });
}

builder.Build().Run();
