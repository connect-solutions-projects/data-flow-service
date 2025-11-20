namespace DataFlow.Api.Options;

public class ImportStorageOptions
{
    public const string SectionName = "ImportStorage";
    public string BasePath { get; set; } = "/var/dataflow/imports";
}

