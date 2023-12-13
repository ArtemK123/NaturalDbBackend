namespace WebApi.Options;

public class BigQueryOptions
{
    public const string SectionPath = "BigQuery";

    public string ProjectId { get; set; } = null!;
}
