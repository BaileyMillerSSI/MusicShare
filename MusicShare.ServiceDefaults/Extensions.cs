using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts.Configuration;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Reflection;
using MusicShare.Contracts;

namespace Microsoft.Extensions.Hosting
{
    // Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
    // This project should be referenced by each service project in your solution.
    // To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
    public static class Extensions
    {
        private const string HealthEndpointPath = "/health";
        private const string AlivenessEndpointPath = "/alive";

        public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.ConfigureOpenTelemetry();

            builder.AddDefaultHealthChecks();

            builder.Services.AddServiceDiscovery();

            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                // Turn on resilience by default
                http.AddStandardResilienceHandler();

                // Turn on service discovery by default
                http.AddServiceDiscovery();
            });

            // Uncomment the following to restrict the allowed schemes for service discovery.
            // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
            // {
            //     options.AllowedSchemes = ["https"];
            // });

            builder.AddPersistence();

            return builder;
        }

        public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();
                })
                .WithTracing(tracing =>
                {
                    tracing.AddSource(builder.Environment.ApplicationName)
                        .AddAspNetCoreInstrumentation(tracing =>
                            // Exclude health check requests from tracing
                            tracing.Filter = context =>
                                !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                                && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                        )
                        // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                        //.AddGrpcClientInstrumentation()
                        .AddHttpClientInstrumentation();
                });

            builder.AddOpenTelemetryExporters();

            return builder;
        }

        private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            if (useOtlpExporter)
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }

            // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
            //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            //{
            //    builder.Services.AddOpenTelemetry()
            //       .UseAzureMonitor();
            //}

            return builder;
        }

        public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.Services.AddHealthChecks()
                // Add a default liveness check to ensure app is responsive
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

            return builder;
        }

        public static WebApplication MapDefaultEndpoints(this WebApplication app)
        {
            // Adding health checks endpoints to applications in non-development environments has security implications.
            // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
            if (app.Environment.IsDevelopment())
            {
                // All health checks must pass for app to be considered ready to accept traffic after starting
                app.MapHealthChecks(HealthEndpointPath);

                // Only health checks tagged with the "live" tag must pass for app to be considered alive
                app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
                {
                    Predicate = r => r.Tags.Contains("live")
                });
            }

            return app;
        }

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
}
