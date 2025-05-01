using System.Text;
using Dapper;
using MediatR;
using MySqlConnector;

public record ExecuteInStarRocksResponse(string ResultsInCsv);

public record ExecuteInStarRocksRequest(string Sql) : IRequest<ExecuteInStarRocksResponse>;

internal sealed class ExecuteInStarRocksRequestHandler : IRequestHandler<ExecuteInStarRocksRequest, ExecuteInStarRocksResponse>
{
  private readonly IConfiguration _configuration;
  private readonly ILogger<ExecuteInStarRocksRequestHandler> _logger;

  public ExecuteInStarRocksRequestHandler(IConfiguration configuration, ILogger<ExecuteInStarRocksRequestHandler> logger)
  {
    _configuration = configuration;
    _logger = logger;
  }

  public async Task<ExecuteInStarRocksResponse> Handle(ExecuteInStarRocksRequest request, CancellationToken cancellationToken)
  {
    var connectionString = _configuration.GetValue<string>("StarRocks:ConnectionString");
    using var connection = new MySqlConnection(connectionString);
    var rows = (await connection.QueryAsync(request.Sql, cancellationToken)).ToArray();

    var resultAsCsv = SerializeToCsv(rows);
    
    _logger.LogInformation("Executed SQL in StarRocks. Count of rows={count}, Sql={sql}", rows.Length, request.Sql);
    return new ExecuteInStarRocksResponse(resultAsCsv);
  }

  private string SerializeToCsv(IReadOnlyCollection<dynamic> rows) {
    var sb = new StringBuilder();
    var firstRow = rows.FirstOrDefault();
    if (firstRow == null) {
      return sb.ToString();
    }

    var header = ((IDictionary<string, object>)firstRow).Keys;
    sb.AppendLine(string.Join(",", header)); // CSV header
    // Add all rows
    foreach (var row in rows)
    {
        var values = ((IDictionary<string, object>)row).Values
            .Select(v => EscapeCsv(v?.ToString() ?? ""));
        sb.AppendLine(string.Join(",", values));
    }

    return sb.ToString();
  }

  // Helper to escape commas and quotes
  private string EscapeCsv(string value)
  {
    if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
    {
        value = value.Replace("\"", "\"\"");
        return $"\"{value}\"";
    }
    return value;
  }
}
