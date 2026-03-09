using MassTransit;
using MongoDB.Driver;
using MusicShare.Api.Sagas.ShareRequest;
using MusicShare.Persistence;
using MusicShare.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Add services to the container
builder.Services.AddControllers();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

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

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();
