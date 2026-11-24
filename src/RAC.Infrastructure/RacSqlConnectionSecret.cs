namespace RAC.Infrastructure;

public static class RacSqlConnectionSecret
{
    public static string? Read()
    {
        var path = Environment.GetEnvironmentVariable("RAC_SQLSERVER_CONNECTION_FILE");
        if (path is null)
            return Environment.GetEnvironmentVariable("RAC_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("SQL secret file path is empty.");
        var value = File.ReadAllText(path).Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("SQL secret file is empty.");
        return value;
    }
}
