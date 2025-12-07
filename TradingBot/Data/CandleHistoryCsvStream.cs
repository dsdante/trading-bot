using Npgsql;
using System.Diagnostics;

namespace TradingBot.Data;

/// <summary> A binary stream accepting ASCII CSV data with candle history </summary>
/// <remarks>Can be implicitly cast to <see cref="Stream"/>.</remarks>
/// <example><code>
///   await using var file = File.OpenRead("candles.csv");
///   await using var dbStream = await CandleHistoryCsvStream.OpenAsync(connectionString, cancellationToken);
///   await file.CopyToAsync(dbStream);
///   await dbStream.CommitAsync(cancellationToken);
/// </code></example>
internal class CandleHistoryCsvStream : IAsyncDisposable
{
    /// <summary> The underlying stream </summary>
    public Stream BaseStream => writer.BaseStream;

    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private readonly StreamWriter writer;
    private readonly string tempTableName;
    private readonly ILoggerFactory? loggerFactory;
    private bool commited;

    /// <summary> Begin a database transaction and open a data import stream </summary>
    public static async Task<CandleHistoryCsvStream> OpenAsync(string connectionString, ILoggerFactory? loggerFactory, CancellationToken cancellation)
    {
        Debug.Assert(connectionString != null);
        cancellation.ThrowIfCancellationRequested();
        var connection = new NpgsqlConnection(connectionString);
        NpgsqlTransaction? transaction = null;

        try
        {
            await connection.OpenAsync(cancellation);
            transaction = await connection.BeginTransactionAsync(cancellation);
            var tempTableName = $"candle_{Guid.NewGuid():N}";

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = $"CREATE TEMP TABLE {tempTableName} (LIKE candle) ON COMMIT DROP;";
                await command.ExecuteNonQueryAsync(cancellation);
            }

            var writer = await connection.BeginTextImportAsync(
                // The column order is OCHLV, not OHLCV.
                $"COPY {tempTableName} (instrument, timestamp, open, close, high, low, volume) FROM STDIN CSV DELIMITER ';' ENCODING 'SQL_ASCII';",
                cancellation);
            Debug.Assert(writer is StreamWriter);

            return new CandleHistoryCsvStream(connection, transaction, (StreamWriter)writer, tempTableName, loggerFactory);
        }
        catch
        {
            if (transaction != null)
                await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public static implicit operator Stream(CandleHistoryCsvStream self) => self.BaseStream;

    private CandleHistoryCsvStream(NpgsqlConnection connection, NpgsqlTransaction transaction, StreamWriter writer, string tempTableName, ILoggerFactory? loggerFactory)
    {
        this.connection = connection;
        this.transaction = transaction;
        this.writer = writer;
        this.tempTableName = tempTableName;
        this.loggerFactory = loggerFactory;
    }

    /// <summary> Commit the changes to the database and close the stream </summary>
    /// <remarks>Futher writes are not possible.</remarks>
    /// <returns>The number of rows added or -1 if unknown</returns>
    public async Task<int> CommitAsync(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();

        int addedRowCount;
        await writer.DisposeAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"INSERT INTO candle SELECT * FROM {tempTableName} ON CONFLICT DO NOTHING;";
            addedRowCount = await command.ExecuteNonQueryAsync(cancellation);
        }
        await transaction.CommitAsync(cancellation);
        commited = true;
        return addedRowCount;
    }

    /// <summary> Roll back any uncommited changes and close the database connection </summary>
    public async ValueTask DisposeAsync()
    {
        if (!commited)
            loggerFactory?.CreateLogger<CandleHistoryCsvStream>().LogWarning("Candle history not commited.");
        await using (connection)
            await transaction.DisposeAsync();
    }
}
