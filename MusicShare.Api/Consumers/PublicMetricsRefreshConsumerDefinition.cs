using MassTransit;

namespace MusicShare.Api.Consumers;

public class PublicMetricsRefreshConsumerDefinition : ConsumerDefinition<PublicMetricsRefreshConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PublicMetricsRefreshConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.PrefetchCount = 1;
        consumerConfigurator.ConcurrentMessageLimit = 1;
        endpointConfigurator.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(1)));
    }
}
