using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Npgsql;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
namespace Velqora.IntegrationTests;

// Defaults to disposable containers. Explicit external mode still uses real services,
// with a new PostgreSQL database and RabbitMQ vhost for every test, never the app database/queue.
internal sealed class IntegrationServices : IAsyncDisposable
{
    private PostgreSqlContainer? postgres;
    private RabbitMqContainer? rabbit;
    private RedisContainer? redis;
    private string? adminPostgres;
    private HttpClient? management;
    private bool databaseCreated, vhostCreated;
    public string Name { get; } = "velqora_test_" + Guid.NewGuid().ToString("N");
    public string Postgres { get; private set; } = "";
    public string Rabbit { get; private set; } = "";
    public string Redis { get; private set; } = "";

    public static async Task<IntegrationServices> StartAsync(bool pipeline, CancellationToken ct)
    {
        var services = new IntegrationServices();
        try { await services.StartCoreAsync(pipeline, ct); return services; }
        catch { await services.DisposeAsync(); throw; }
    }

    private async Task StartCoreAsync(bool pipeline, CancellationToken ct)
    {
        var external = Environment.GetEnvironmentVariable("VELQORA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(external))
        {
            postgres = new PostgreSqlBuilder("postgres:17.6-alpine").Build();
            await postgres.StartAsync(ct); Postgres = postgres.GetConnectionString();
            if (pipeline)
            {
                rabbit = new RabbitMqBuilder("rabbitmq:4.1.4-management-alpine").Build();
                redis = new RedisBuilder("redis:7.4.6-alpine").Build();
                await Task.WhenAll(rabbit.StartAsync(ct), redis.StartAsync(ct));
                Rabbit = rabbit.GetConnectionString(); Redis = redis.GetConnectionString();
            }
            return;
        }
        var builder = new NpgsqlConnectionStringBuilder(external) { Database = "postgres" };
        adminPostgres = builder.ConnectionString;
        await using (var connection = new NpgsqlConnection(adminPostgres))
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{Name}\"", connection);
            await command.ExecuteNonQueryAsync(ct); databaseCreated = true;
        }
        builder.Database = Name; Postgres = builder.ConnectionString;
        if (!pipeline) return;
        var uri = new Uri(Environment.GetEnvironmentVariable("VELQORA_TEST_RABBITMQ")
            ?? throw new InvalidOperationException("External mode requires VELQORA_TEST_RABBITMQ."));
        Redis = Environment.GetEnvironmentVariable("VELQORA_TEST_REDIS")
            ?? throw new InvalidOperationException("External mode requires VELQORA_TEST_REDIS.");
        var credentials = uri.UserInfo.Split(':', 2).Select(Uri.UnescapeDataString).ToArray();
        management = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("VELQORA_TEST_RABBIT_MANAGEMENT") ?? "http://localhost:15672"), Timeout = TimeSpan.FromSeconds(15) };
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials[0] + ":" + credentials[1])));
        using (var response = await management.PutAsJsonAsync("/api/vhosts/" + Name, new { }, ct))
        { response.EnsureSuccessStatusCode(); vhostCreated = true; }
        using (var response = await management.PutAsJsonAsync("/api/permissions/" + Name + "/" + Uri.EscapeDataString(credentials[0]),
            new { configure = ".*", write = ".*", read = ".*" }, ct)) response.EnsureSuccessStatusCode();
        Rabbit = new UriBuilder(uri) { Path = Name }.Uri.AbsoluteUri;
    }

    public async ValueTask DisposeAsync()
    {
        if (management != null)
        {
            if (vhostCreated) { using var response = await management.DeleteAsync("/api/vhosts/" + Name); response.EnsureSuccessStatusCode(); }
            management.Dispose();
        }
        if (databaseCreated && adminPostgres != null)
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(adminPostgres);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{Name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
        if (postgres != null) await postgres.DisposeAsync();
        if (rabbit != null) await rabbit.DisposeAsync();
        if (redis != null) await redis.DisposeAsync();
    }
}
