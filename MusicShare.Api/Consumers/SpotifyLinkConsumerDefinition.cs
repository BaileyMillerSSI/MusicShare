using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;

namespace MusicShare.Api.Consumers;

public class SpotifyLinkConsumerDefinition : ConsumerDefinition<SpotifyLinkConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SpotifyLinkConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbitMq)
        {
            rabbitMq.Bind<ResolveServiceLink>(b =>
            {
                b.ExchangeType = "direct";
                b.RoutingKey = ServiceType.Spotify.ToRoutingKey();
            });
        }
    }
}
