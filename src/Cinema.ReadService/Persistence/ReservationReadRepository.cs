using Cinema.ReadService.Models;
using MongoDB.Driver;

namespace Cinema.ReadService.Persistence;

public class ReservationReadRepository : IReservationReadRepository
{
    private readonly IMongoCollection<ReservationReadModel> _collection;

    public ReservationReadRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<ReservationReadModel>("reservations");
    }

    public async Task<ReservationReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ReservationReadModel>.Filter.Eq(r => r.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ReservationReadModel>> GetByShowtimeIdAsync(
        Guid showtimeId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ReservationReadModel>.Filter.Eq(r => r.ShowtimeId, showtimeId);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task AddOrUpdateAsync(ReservationReadModel model, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ReservationReadModel>.Filter.Eq(r => r.Id, model.Id);
        var options = new ReplaceOptions { IsUpsert = true };

        await _collection.ReplaceOneAsync(filter, model, options, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ReservationReadModel>.Filter.Eq(r => r.Id, id);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }
}
