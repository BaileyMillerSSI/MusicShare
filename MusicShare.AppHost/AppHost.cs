var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var mongodb = builder.AddMongoDB("mongodb")
    .WithMongoExpress();

var messagingUsername = builder.AddParameter("rabbitmq-username", "guest");
var messagingPassword = builder.AddParameter("rabbitmq-password", "guest");

var rabbitmq = builder.AddRabbitMQ("rabbitmq", messagingUsername, messagingPassword)
    .WithManagementPlugin();

// Backend services
var api = builder.AddProject<Projects.MusicShare_Api>("api")
    .WithReference(mongodb)
    .WithReference(rabbitmq);

builder.AddProject<Projects.MusicShare_Worker>("worker")
    .WithReference(mongodb)
    .WithReference(rabbitmq);

// Frontend
builder.AddViteApp("frontend", "../MusicShare.Frontend")
    .WithReference(api)
    .PublishAsDockerFile();

builder.Build().Run();
