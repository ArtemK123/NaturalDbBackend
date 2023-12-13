using System.Text;
using Google.Cloud.BigQuery.V2;
using MediatR;
using WebApi.Options;

namespace WebApi.UseCases.BigQuery;

public record ExecuteSqlInBigQueryRequest(string Sql) : IRequest<ExecuteSqlInBigQueryResponse>;

public record ExecuteSqlInBigQueryResponse(string ResultsInCsv);

public class ExecuteSqlInBigQueryHandler : IRequestHandler<ExecuteSqlInBigQueryRequest, ExecuteSqlInBigQueryResponse>
{
    private readonly ILogger<ExecuteSqlInBigQueryHandler> logger;
    private readonly BigQueryOptions bigQueryOptions;

    public ExecuteSqlInBigQueryHandler(ILogger<ExecuteSqlInBigQueryHandler> logger, BigQueryOptions bigQueryOptions)
    {
        this.logger = logger;
        this.bigQueryOptions = bigQueryOptions;
    }

    public async Task<ExecuteSqlInBigQueryResponse> Handle(ExecuteSqlInBigQueryRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing Sql in BigQuery. Sql - {sql}, BigQuery projectId - {projectId}", request.Sql, bigQueryOptions.ProjectId);

        // TODO: Investigate authorization of big query
        BigQueryClient client = await BigQueryClient.CreateAsync(bigQueryOptions.ProjectId);
        BigQueryResults resultsEnumerable = await client.ExecuteQueryAsync(request.Sql, Enumerable.Empty<BigQueryParameter>());

        IReadOnlyCollection<BigQueryRow> resultsArray = resultsEnumerable.ToArray();

        logger.LogInformation(
            "Executed Sql in BigQuery. Count of received rows - {countOfRows}, Sql - {sql}, BigQuery projectId - {projectId}",
            resultsArray.Count,
            request.Sql,
            bigQueryOptions.ProjectId);

        var resultsInCsv = ConvertResultsToCsv(resultsArray);
        return new ExecuteSqlInBigQueryResponse(resultsInCsv);
    }

    private string ConvertResultsToCsv(IReadOnlyCollection<BigQueryRow> results)
    {
        StringBuilder csvResult = new();
        var separator = ',';

        if (results.Count != 0)
        {
            var firstResult = results.First();
            var fieldNames = firstResult.Schema.Fields.Select(f => f.Name);
            var header = string.Join(separator, fieldNames);
            csvResult.AppendLine(header);
        }

        foreach (BigQueryRow row in results)
        {
            var line = string.Join(separator, row.RawRow.F.Select(field => field.V));
            csvResult.AppendLine(line);
        }

        return csvResult.ToString();
    }
}
