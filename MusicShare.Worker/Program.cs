using MassTransit;
using MongoDB.Driver;
using MusicShare.Persistence;
using MusicShare.ServiceDefaults;
using MusicShare.Worker.Sagas.ShareRequest;
using MusicShare.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHttpClient<IFrontendRevalidateService, FrontendRevalidateService>((svp, client) =>
{
    var configuration = svp.GetRequiredService<IConfiguration>();

    client.DefaultRequestHeaders.TryAddWithoutValidation("Bearer", configuration["RevalidateSecret"]);
    client.BaseAddress = new Uri(configuration["services__frontend__https__0"] ?? configuration["services__frontend__http__0"] ?? string.Empty);
});

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
