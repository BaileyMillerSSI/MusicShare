using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace MusicShare.Contracts
{
    public static class MessageAccessExtensions
    {
        public static TBuilder AddMessageAccess<TBuilder>(this TBuilder builder, params Assembly[] assemblies) where TBuilder : IHostApplicationBuilder
        {
            builder.AddRabbitMQClient("messaging");

            // Configure MassTransit to use RabbitMQ and scan this assembly for consumers
            builder.Services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();

                // Register consumers from this assembly
                x.AddConsumers(assemblies);

                x.UsingRabbitMq((context, cfg) =>
                {
                    // Use the Aspire-provided RabbitMQ connection settings for the "messaging" instance
                    var connection = builder.Configuration.GetConnectionString("rabbitmq");

                    cfg.Host(connection);
                    cfg.ConfigureEndpoints(context);
                });
            });

            return builder;
        }
    }
}
