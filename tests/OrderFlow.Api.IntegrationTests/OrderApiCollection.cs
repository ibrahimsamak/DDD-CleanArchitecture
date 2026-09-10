namespace OrderFlow.Api.IntegrationTests;

using Xunit;

/// <summary>
/// Shares one container and one host across every integration test class. A class fixture would
/// start SQL Server again for each class, and the container costs ~20 seconds.
/// </summary>
[CollectionDefinition(nameof(OrderApiCollection))]
public sealed class OrderApiCollection : ICollectionFixture<OrderApiFactory>;
