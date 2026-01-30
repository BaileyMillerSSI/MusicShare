using MassTransit;
using MongoDB.Driver;
using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services.Extensions;
using MusicShare.Persistence;
using MusicShare.Worker.Sagas;

var builder = Host.CreateApplicationBuilder(args);

// TODO: Add Aspire service defaults for observability and health checks
// builder.AddServiceDefaults();

// Add persistence layer (registers IMongoClient via Aspire)
builder.AddPersistence();

// Register music service adapters
builder.AddMusicServices();

// Configure MassTransit with saga support and MongoDB outbox
builder.AddMessageAccess(
    assemblies: [typeof(Program).Assembly],
    configureSagas: (busConfig, hostBuilder) =>
    {
        // Register the saga state machine with MongoDB persistence
        busConfig.AddSagaStateMachine<ShareRequestSaga, ShareRequestSagaState>(cfg =>
            {
                // Add retry policy for optimistic concurrency conflicts
                // This handles the case when multiple ServiceLinkResolved events
                // arrive simultaneously and try to update the saga state
                cfg.UseMessageRetry(r =>
                {
                    r.Handle<MongoDbConcurrencyException>();
                    r.Interval(5, TimeSpan.FromMilliseconds(100));
                });
            })
            .MongoDbRepository(r =>
            {
                // Use existing IMongoClient from Aspire
                r.ClientFactory(provider => provider.GetRequiredService<IMongoClient>());
                r.DatabaseFactory(provider => provider.GetRequiredService<IMusicShareDbContext>().Database);
                r.CollectionName = "shareRequestSagas";
            });
    });

var host = builder.Build();
host.Run();
