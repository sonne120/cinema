using Cinema.Application.Common.Interfaces.Queries;
using Cinema.Application.Common.Models;
using MongoDB.Driver;

namespace Cinema.Persistence.Read.Repositories;

public class ShowtimeReadRepository : IShowtimeReadRepository
{
    private const string CollectionName = "showtimes";

    private readonly IMongoCollection<ShowtimeReadModel> _collection;

    public ShowtimeReadRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<ShowtimeReadModel>(CollectionName);
    }

    public async Task<ShowtimeReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(ById(id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ShowtimeReadModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(Builders<ShowtimeReadModel>.Filter.Empty)
            .SortBy(s => s.ScreeningTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ShowtimeReadModel>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ShowtimeReadModel>.Filter.And(
            Builders<ShowtimeReadModel>.Filter.Gte(s => s.ScreeningTime, startDate),
            Builders<ShowtimeReadModel>.Filter.Lte(s => s.ScreeningTime, endDate));

        return await _collection
            .Find(filter)
            .SortBy(s => s.ScreeningTime)
            .ToListAsync(cancellationToken);
    }

    public async Task AddOrUpdateAsync(ShowtimeReadModel model, CancellationToken cancellationToken = default)
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

    private static FilterDefinition<ShowtimeReadModel> ById(Guid id) =>
        Builders<ShowtimeReadModel>.Filter.Eq(s => s.Id, id);
}
