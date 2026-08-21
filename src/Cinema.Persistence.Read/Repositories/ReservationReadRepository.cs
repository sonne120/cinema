using Cinema.Application.Common.Interfaces.Queries;
using Cinema.Application.Common.Models;
using MongoDB.Driver;

namespace Cinema.Persistence.Read.Repositories;

public class ReservationReadRepository : IReservationReadRepository
{
    private const string CollectionName = "reservations";

    private readonly IMongoCollection<ReservationReadModel> _collection;

    public ReservationReadRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<ReservationReadModel>(CollectionName);
    }

    public async Task<ReservationReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(ById(id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ReservationReadModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(Builders<ReservationReadModel>.Filter.Empty)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ReservationReadModel>> GetByShowtimeIdAsync(
        Guid showtimeId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ReservationReadModel>.Filter.Eq(r => r.ShowtimeId, showtimeId);

        return await _collection
            .Find(filter)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddOrUpdateAsync(ReservationReadModel model, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(
            ById(model.Id),
            model,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(ById(id), cancellationToken);
    }

    private static FilterDefinition<ReservationReadModel> ById(Guid id) =>
        Builders<ReservationReadModel>.Filter.Eq(r => r.Id, id);
}
