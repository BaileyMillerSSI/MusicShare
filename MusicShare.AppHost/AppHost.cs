var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var mongodb = builder.AddMongoDB("mongodb");

var messagingUsername = builder.AddParameter("rabbitmq-username", secret: true);
var messagingPassword = builder.AddParameter("rabbitmq-password", secret: true);

var rabbitmq = builder.AddRabbitMQ("rabbitmq", messagingUsername, messagingPassword);

// Add dev tooling only when running locally (not publishing)
if (!builder.ExecutionContext.IsPublishMode)
{
    mongodb.WithMongoExpress();
    rabbitmq.WithManagementPlugin();
}

// Backend services
var api = builder.AddProject<Projects.MusicShare_Api>("api")
    .WithReference(mongodb)
    .WithReference(rabbitmq)
    .WaitFor(mongodb)
    .WaitFor(rabbitmq);

builder.AddProject<Projects.MusicShare_Worker>("worker")
    .WithReference(mongodb)
    .WithReference(rabbitmq)
    .WaitFor(mongodb)
    .WaitFor(rabbitmq);

// Frontend
builder.AddViteApp("frontend", "../MusicShare.Frontend")
    .WithReference(api)
    .WaitFor(api)
    .PublishAsDockerFile();

builder.Build().Run();
