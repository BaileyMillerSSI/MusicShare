using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using MusicShare.Contracts.Configuration;
using MusicShare.Contracts.Messages;
using System.Reflection;

namespace MusicShare.Contracts;

public static class MessageAccessExtensions
{
    /// <summary>
    /// Configures MassTransit with RabbitMQ, MongoDB outbox, and optional saga support.
    /// </summary>
    /// <param name="builder">The host application builder</param>
    /// <param name="assemblies">Assemblies to scan for consumers</param>
    /// <param name="configureSagas">Optional callback to register sagas</param>
    public static TBuilder AddMessageAccess<TBuilder>(
        this TBuilder builder,
        Assembly[] assemblies,
        Action<IBusRegistrationConfigurator, IHostApplicationBuilder>? configureSagas = null)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddRabbitMQClient("messaging");

        // Get MongoDB settings for outbox configuration
        var mongoSettings = builder.Configuration
            .GetSection(MongoDbSettings.SectionName)
            .Get<MongoDbSettings>() ?? new MongoDbSettings();

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            // Register consumers from assemblies
            x.AddConsumers(assemblies);

            // Allow caller to register sagas
            configureSagas?.Invoke(x, builder);

            // Configure MongoDB outbox for reliable message delivery
            x.AddInMemoryInboxOutbox();

            x.UsingRabbitMq((context, cfg) =>
            {
                var connection = builder.Configuration.GetConnectionString("rabbitmq");
                cfg.Host(connection);

                // Configure ResolveServiceLink to use direct exchange with routing keys
                cfg.Message<ResolveServiceLink>(m => m.SetEntityName("resolve-service-link"));

                cfg.Publish<ResolveServiceLink>(p => p.ExchangeType = "direct");

                cfg.Send<ResolveServiceLink>(s =>
                    s.UseRoutingKeyFormatter(ctx => ctx.Message.TargetService.ToRoutingKey()));

                cfg.ConfigureEndpoints(context);
            });
        });

        return builder;
    }

    /// <summary>
    /// Configures MassTransit with RabbitMQ and MongoDB outbox.
    /// </summary>
    public static TBuilder AddMessageAccess<TBuilder>(
        this TBuilder builder,
        params Assembly[] assemblies)
        where TBuilder : IHostApplicationBuilder
    {
        return builder.AddMessageAccess(assemblies, configureSagas: null);
    }
}
