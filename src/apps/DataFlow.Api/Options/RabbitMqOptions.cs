namespace DataFlow.Api.Options;

public class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public static class RabbitMqOptionKeys
{
    public const string SectionName = "RabbitMq";
}
