using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MusicShare.Persistence.Configuration;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Persistence;

public static class DependencyInjection
{
    public static TBuilder AddPersistence<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Register database context
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        builder.AddMongoDBClient("mongodb");

        builder.Services.AddOptions<MongoDbSettings>()
            .Bind(builder.Configuration.GetSection(MongoDbSettings.SectionName));

        builder.Services.AddSingleton<MusicShareDbContext>();

        // Register repositories
        builder.Services.AddScoped<ISongRepository, SongRepository>();
        builder.Services.AddScoped<ISongServiceLinkRepository, SongServiceLinkRepository>();
        builder.Services.AddScoped<IShareRequestRepository, ShareRequestRepository>();
        builder.Services.AddScoped<IWorkflowStateRepository, WorkflowStateRepository>();

        return builder;
    }
}
