namespace DataFlow.Infrastructure.Persistence.Seed;

public class ClientSeedOptions
{
    public IList<ClientSeedEntry> Clients { get; set; } = new List<ClientSeedEntry>();
}

public class ClientSeedEntry
{
    public string Name { get; set; } = string.Empty;
    public string ClientIdentifier { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

