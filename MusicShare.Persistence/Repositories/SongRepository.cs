using MongoDB.Driver;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class SongRepository : ISongRepository
{
    private readonly IMongoCollection<Song> _songs;

    public SongRepository(IMusicShareDbContext context)
    {
        _songs = context.Songs;
    }

    public async Task<Song?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Song>.Filter.Eq(s => s.Id, id);
        return await _songs.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Song> InsertAsync(Song song, CancellationToken cancellationToken = default)
    {
        song.CreatedAt = DateTime.UtcNow;
        song.UpdatedAt = DateTime.UtcNow;
        await _songs.InsertOneAsync(song, cancellationToken: cancellationToken);
        return song;
    }

    public async Task<Song> UpsertAsync(Song song, CancellationToken cancellationToken = default)
    {
        song.UpdatedAt = DateTime.UtcNow;

        var filter = Builders<Song>.Filter.Eq(s => s.Id, song.Id);
        var options = new ReplaceOptions { IsUpsert = true };

        await _songs.ReplaceOneAsync(filter, song, options, cancellationToken);
        return song;
    }

    public async Task UpdateAsync(Song song, CancellationToken cancellationToken = default)
    {
        song.UpdatedAt = DateTime.UtcNow;

        var filter = Builders<Song>.Filter.Eq(s => s.Id, song.Id);
        await _songs.ReplaceOneAsync(filter, song, cancellationToken: cancellationToken);
    }
}
