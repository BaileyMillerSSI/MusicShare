using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MusicShare.Persistence.Configuration;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence;

public class MusicShareDbContext(IMongoClient client, IOptions<MongoDbSettings> settings)
{
    private IMongoDatabase Database =>
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
