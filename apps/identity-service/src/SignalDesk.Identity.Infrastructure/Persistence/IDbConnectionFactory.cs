using System.Data;
using Npgsql;

namespace SignalDesk.Identity.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
