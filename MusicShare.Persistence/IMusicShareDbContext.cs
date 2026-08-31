using MongoDB.Driver;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence;

public interface IMusicShareDbContext
{
    IMongoDatabase Database { get; }
    IMongoCollection<ShareRequest> ShareRequests { get; }
    IMongoCollection<Song> Songs { get; }
    IMongoCollection<SongServiceLink> SongServiceLinks { get; }
    IMongoCollection<WorkflowState> WorkflowStates { get; }
    IMongoCollection<PublicMetricsSnapshot> PublicMetricsSnapshots { get; }
}
