namespace MoneyTransferService.WebAPI.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class WebApiCollection : ICollectionFixture<TestWebApplicationFactory>
{
    public const string Name = "Web API integration";
}
