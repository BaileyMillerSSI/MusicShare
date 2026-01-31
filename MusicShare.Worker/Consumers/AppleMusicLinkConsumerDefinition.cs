using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;

namespace MusicShare.Worker.Consumers;

public class AppleMusicLinkConsumerDefinition : ConsumerDefinition<AppleMusicLinkConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<AppleMusicLinkConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbitMq)
        {
            rabbitMq.Bind<ResolveServiceLink>(b =>
            {
                b.ExchangeType = "direct";
                b.RoutingKey = ServiceType.AppleMusic.ToRoutingKey();
            });
        }
    }
}
