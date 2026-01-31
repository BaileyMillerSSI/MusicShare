using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;

namespace MusicShare.Worker.Consumers;

public class YouTubeMusicLinkConsumerDefinition : ConsumerDefinition<YouTubeMusicLinkConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<YouTubeMusicLinkConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbitMq)
        {
            rabbitMq.Bind<ResolveServiceLink>(b =>
            {
                b.ExchangeType = "direct";
                b.RoutingKey = ServiceType.YouTubeMusic.ToRoutingKey();
            });
        }
    }
}
