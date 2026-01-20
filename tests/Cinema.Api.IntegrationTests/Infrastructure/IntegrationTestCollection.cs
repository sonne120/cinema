using Xunit;

namespace Cinema.Api.IntegrationTests.Infrastructure;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<CinemaWebApplicationFactory>
{
}
