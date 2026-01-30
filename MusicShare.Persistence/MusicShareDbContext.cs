using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MusicShare.Contracts.Configuration;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence;

public interface IMusicShareDbContext
{
    IMongoDatabase Database { get; }
    IMongoCollection<ShareRequest> ShareRequests { get; }
    IMongoCollection<Song> Songs { get; }
    IMongoCollection<SongServiceLink> SongServiceLinks { get; }
    IMongoCollection<WorkflowState> WorkflowStates { get; }
}

public class MusicShareDbContext(IMongoClient client, IOptions<MongoDbSettings> settings) : IMusicShareDbContext
{
    public IMongoDatabase Database =>
        client.GetDatabase(settings.Value.DatabaseName);

    public IMongoCollection<Song> Songs =>
        Database.GetCollection<Song>("songs");

    public IMongoCollection<SongServiceLink> SongServiceLinks =>
        Database.GetCollection<SongServiceLink>("songServiceLinks");

    public IMongoCollection<ShareRequest> ShareRequests =>
        Database.GetCollection<ShareRequest>("shareRequests");

    public IMongoCollection<WorkflowState> WorkflowStates =>
        Database.GetCollection<WorkflowState>("workflowStates");
}
