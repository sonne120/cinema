namespace Cinema.Persistence.Read;

public class MongoDbSettings
{
    public const string SectionName = "MongoDbSettings";

    public string ConnectionString { get; set; } = "mongodb://root:password123@localhost:27017";
    public string DatabaseName { get; set; } = "CinemaReadDb";
}
