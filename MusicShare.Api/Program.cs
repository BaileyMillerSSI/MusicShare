using MassTransit;
using MongoDB.Driver;
using MusicShare.Api.Sagas.ShareRequest;
using MusicShare.Api.Security;
using MusicShare.Persistence;
using MusicShare.ServiceDefaults;
using MusicShare.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Add services to the container
builder.Services.AddControllers();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var corsPolicyName = builder.Services.AddMusicShareCors(builder.Configuration, builder.Environment);

// Configure MassTransit with RabbitMQ, consumers, and saga
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

builder.Services.AddHostedService<PublicMetricsBootstrapService>();
builder.Services.AddHostedService<PublicMetricsWeeklyRefreshService>();
builder.Services.AddSingleton<PublicMetricsInvalidationRetryService>();
builder.Services.AddSingleton<IPublicMetricsInvalidationRetryService>(provider =>
    provider.GetRequiredService<PublicMetricsInvalidationRetryService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<PublicMetricsInvalidationRetryService>());

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors(corsPolicyName);
app.MapControllers();

app.Run();
