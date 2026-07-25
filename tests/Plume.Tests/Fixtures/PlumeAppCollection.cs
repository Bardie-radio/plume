using Xunit;

namespace Plume.Tests.Fixtures;

/// <summary>
/// One host per collection — parallel WebApplicationFactory instances flaked Razor routes.
/// </summary>
[CollectionDefinition("PlumeApp")]
public sealed class PlumeAppCollection : ICollectionFixture<PlumeWebApplicationFactory>;
