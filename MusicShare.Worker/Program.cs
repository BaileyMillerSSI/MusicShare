using MassTransit;
using MusicShare.Contracts;
using MusicShare.Persistence;
using MusicShare.Worker.Consumers;
using MusicShare.Worker.Services;
using MusicShare.Worker.Services.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// TODO: Add Aspire service defaults for observability and health checks
// builder.AddServiceDefaults();

// Add persistence layer
builder.AddPersistence();

// Register music service adapters

builder.AddSpotifyAccess();
builder.Services.AddSingleton<IMusicServiceAdapter, AppleMusicMockAdapter>();
builder.Services.AddSingleton<IMusicServiceAdapter, YouTubeMusicMockAdapter>();
builder.Services.AddSingleton<MusicServiceResolver>();

// Configure MassTransit with RabbitMQ
builder.AddMessageAccess(typeof(Program).Assembly);

var host = builder.Build();
host.Run();
