using Npgsql;
using Renci.SshNet;

const string DefaultTarget =
    "Host=localhost;Port=5432;Database=family_expenses;Username=family_expenses;Password=family_expenses";
const int DefaultRemotePostgresPort = 5432;

var options = SyncOptions.Parse(args);

if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

using var tunnel = ShouldOpenTunnel(options)
    ? StartVpsTunnel(options)
    : null;

var sourceConnectionString = options.SourceConnectionString ?? Environment.GetEnvironmentVariable("VPS_POSTGRES")
    ?? BuildDefaultSourceConnectionString(options.TunnelPort);
var targetConnectionString = options.TargetConnectionString ?? Environment.GetEnvironmentVariable("LOCAL_POSTGRES")
    ?? DefaultTarget;

if (!options.Force)
{
    EnsureTargetLooksLocal(targetConnectionString);
}

EnsureDifferentDatabases(sourceConnectionString, targetConnectionString);

await using var source = new NpgsqlConnection(sourceConnectionString);
await using var target = new NpgsqlConnection(targetConnectionString);

await source.OpenAsync();
await target.OpenAsync();

await EnsureConnected(source, "source");
await EnsureConnected(target, "target");

var sourceTables = await LoadTables(source);
var targetTables = await LoadTables(target);
var targetTableNames = targetTables.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
var tables = sourceTables.Where(x => targetTableNames.Contains(x.Name)).ToList();
var missingTargetTables = sourceTables.Where(x => !targetTableNames.Contains(x.Name)).Select(x => x.Name).ToList();

if (missingTargetTables.Count > 0)
{
    throw new InvalidOperationException(
        $"Target database does not contain source table(s): {string.Join(", ", missingTargetTables)}");
}

if (tables.Count == 0)
{
    throw new InvalidOperationException("No public tables found to copy.");
}

foreach (var table in tables)
{
    await EnsureSameColumns(source, target, table);
}

Console.WriteLine("Source: " + MaskPassword(sourceConnectionString));
Console.WriteLine("Target: " + MaskPassword(targetConnectionString));
Console.WriteLine();
Console.WriteLine("Target public tables will be truncated and replaced with source data.");

if (!options.Yes && !Confirm())
{
    Console.WriteLine("Cancelled.");
    return 1;
}

var orderedTables = OrderTablesByForeignKeys(tables, await LoadForeignKeys(source, tables));
await TruncateTables(target, orderedTables);

foreach (var table in orderedTables)
{
    var copied = await CopyTable(source, target, table);
    Console.WriteLine($"{table.Name}: {copied} row(s)");
}

await ResetSequences(target, orderedTables);

Console.WriteLine();
Console.WriteLine("Done. Local database now mirrors the VPS database.");
return 0;

static bool ShouldOpenTunnel(SyncOptions options) =>
    options.OpenTunnel
    && options.SourceConnectionString is null
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VPS_POSTGRES"));

static SshTunnel StartVpsTunnel(SyncOptions options)
{
    var password = Environment.GetEnvironmentVariable("FIRSTBYTE_PASSWORD");
    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "FIRSTBYTE_PASSWORD environment variable is required to open the VPS SSH tunnel.");
    }

    Console.WriteLine($"Opening SSH tunnel via {options.SshUser}@{options.VpsHost}...");

    var ssh = new SshClient(options.VpsHost, options.SshUser, password);
    try
    {
        ssh.Connect();
        var remotePostgresIp = ResolveRemotePostgresIp(ssh, options.RemotePostgresContainer);
        var port = new ForwardedPortLocal(
            "127.0.0.1",
            (uint)options.TunnelPort,
            remotePostgresIp,
            (uint)DefaultRemotePostgresPort);

        ssh.AddForwardedPort(port);
        port.Start();

        Console.WriteLine(
            $"SSH tunnel ready: 127.0.0.1:{options.TunnelPort} -> {remotePostgresIp}:{DefaultRemotePostgresPort}");

        return new SshTunnel(ssh, port);
    }
    catch
    {
        ssh.Dispose();
        throw;
    }
}

static string ResolveRemotePostgresIp(SshClient ssh, string containerName)
{
    var command = ssh.RunCommand(
        $"docker inspect -f '{{{{range.NetworkSettings.Networks}}}}{{{{.IPAddress}}}}{{{{end}}}}' {ShellQuote(containerName)}");

    if (command.ExitStatus != 0 || string.IsNullOrWhiteSpace(command.Result))
    {
        throw new InvalidOperationException(
            $"Could not resolve remote Postgres container IP for {containerName}. {command.Error}".Trim());
    }

    return command.Result.Trim();
}

static string BuildDefaultSourceConnectionString(int tunnelPort) =>
    $"Host=localhost;Port={tunnelPort};Database=family_expenses;Username=family_expenses;Password=family_expenses";

static async Task EnsureConnected(NpgsqlConnection connection, string label)
{
    await using var command = new NpgsqlCommand("SELECT current_database(), current_user", connection);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Console.WriteLine($"{label}: database={reader.GetString(0)}, user={reader.GetString(1)}");
}

static void EnsureDifferentDatabases(string sourceConnectionString, string targetConnectionString)
{
    var source = new NpgsqlConnectionStringBuilder(sourceConnectionString);
    var target = new NpgsqlConnectionStringBuilder(targetConnectionString);

    var sameHost = string.Equals(source.Host, target.Host, StringComparison.OrdinalIgnoreCase);
    var samePort = source.Port == target.Port;
    var sameDatabase = string.Equals(source.Database, target.Database, StringComparison.OrdinalIgnoreCase);
    var sameUsername = string.Equals(source.Username, target.Username, StringComparison.OrdinalIgnoreCase);

    if (sameHost && samePort && sameDatabase && sameUsername)
    {
        throw new InvalidOperationException("Source and target point to the same database.");
    }
}

static void EnsureTargetLooksLocal(string targetConnectionString)
{
    var target = new NpgsqlConnectionStringBuilder(targetConnectionString);
    var localHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1"
    };

    if (!localHosts.Contains(target.Host ?? string.Empty) || target.Port != 5432)
    {
        throw new InvalidOperationException(
            "Target database does not look like the local database. Pass --force if this is intentional.");
    }
}

static async Task<List<TableInfo>> LoadTables(NpgsqlConnection connection)
{
    const string sql = """
        SELECT table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_type = 'BASE TABLE'
        ORDER BY table_name
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    var tables = new List<TableInfo>();

    while (await reader.ReadAsync())
    {
        tables.Add(new TableInfo(reader.GetString(0)));
    }

    return tables;
}

static async Task EnsureSameColumns(NpgsqlConnection source, NpgsqlConnection target, TableInfo table)
{
    var sourceColumns = await LoadColumns(source, table);
    var targetColumns = await LoadColumns(target, table);

    if (!sourceColumns.SequenceEqual(targetColumns, StringComparer.Ordinal))
    {
        throw new InvalidOperationException(
            $"Column mismatch for table {table.Name}. Source: {string.Join(", ", sourceColumns)}. Target: {string.Join(", ", targetColumns)}");
    }

    table.Columns.AddRange(sourceColumns);
}

static async Task<List<string>> LoadColumns(NpgsqlConnection connection, TableInfo table)
{
    const string sql = """
        SELECT column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = @table
          AND is_generated = 'NEVER'
        ORDER BY ordinal_position
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("table", table.Name);
    await using var reader = await command.ExecuteReaderAsync();
    var columns = new List<string>();

    while (await reader.ReadAsync())
    {
        columns.Add(reader.GetString(0));
    }

    return columns;
}

static async Task<List<ForeignKeyInfo>> LoadForeignKeys(NpgsqlConnection connection, IReadOnlyCollection<TableInfo> tables)
{
    const string sql = """
        SELECT
            child.relname AS table_name,
            parent.relname AS referenced_table_name
        FROM pg_constraint c
        JOIN pg_class child ON child.oid = c.conrelid
        JOIN pg_namespace child_ns ON child_ns.oid = child.relnamespace
        JOIN pg_class parent ON parent.oid = c.confrelid
        JOIN pg_namespace parent_ns ON parent_ns.oid = parent.relnamespace
        WHERE c.contype = 'f'
          AND child_ns.nspname = 'public'
          AND parent_ns.nspname = 'public'
        """;

    var tableNames = tables.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    var keys = new List<ForeignKeyInfo>();

    while (await reader.ReadAsync())
    {
        var tableName = reader.GetString(0);
        var referencedTableName = reader.GetString(1);

        if (tableNames.Contains(tableName) && tableNames.Contains(referencedTableName))
        {
            keys.Add(new ForeignKeyInfo(tableName, referencedTableName));
        }
    }

    return keys;
}

static List<TableInfo> OrderTablesByForeignKeys(IReadOnlyCollection<TableInfo> tables, IReadOnlyCollection<ForeignKeyInfo> foreignKeys)
{
    var byName = tables.ToDictionary(x => x.Name, StringComparer.Ordinal);
    var dependencies = tables.ToDictionary(x => x.Name, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

    foreach (var key in foreignKeys)
    {
        if (!string.Equals(key.TableName, key.ReferencedTableName, StringComparison.Ordinal))
        {
            dependencies[key.TableName].Add(key.ReferencedTableName);
        }
    }

    var ordered = new List<TableInfo>();
    var remaining = new HashSet<string>(byName.Keys, StringComparer.Ordinal);

    while (remaining.Count > 0)
    {
        var ready = remaining
            .Where(table => dependencies[table].All(dependency => !remaining.Contains(dependency)))
            .OrderBy(table => table, StringComparer.Ordinal)
            .ToList();

        if (ready.Count == 0)
        {
            ready = remaining.OrderBy(table => table, StringComparer.Ordinal).ToList();
        }

        foreach (var table in ready)
        {
            ordered.Add(byName[table]);
            remaining.Remove(table);
        }
    }

    return ordered;
}

static async Task TruncateTables(NpgsqlConnection target, IReadOnlyCollection<TableInfo> tables)
{
    var tableList = string.Join(", ", tables.Select(x => QuoteQualified("public", x.Name)));
    await using var command = new NpgsqlCommand($"TRUNCATE TABLE {tableList} RESTART IDENTITY CASCADE", target);
    await command.ExecuteNonQueryAsync();
    Console.WriteLine("Local tables truncated.");
}

static async Task<long> CopyTable(NpgsqlConnection source, NpgsqlConnection target, TableInfo table)
{
    if (table.Columns.Count == 0)
    {
        return 0;
    }

    var columns = string.Join(", ", table.Columns.Select(QuoteIdentifier));
    var qualifiedTable = QuoteQualified("public", table.Name);
    var copySql = $"COPY {qualifiedTable} ({columns})";
    var copied = 0L;

    using var exporter = await source.BeginTextExportAsync($"{copySql} TO STDOUT (FORMAT CSV)");
    await using var importer = await target.BeginTextImportAsync($"{copySql} FROM STDIN (FORMAT CSV)");

    string? line;
    while ((line = await exporter.ReadLineAsync()) is not null)
    {
        await importer.WriteLineAsync(line);
        copied++;
    }

    return copied;
}

static async Task ResetSequences(NpgsqlConnection target, IReadOnlyCollection<TableInfo> tables)
{
    const string sql = """
        SELECT column_name, pg_get_serial_sequence(format('%I.%I', table_schema, table_name), column_name)
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = @table
          AND pg_get_serial_sequence(format('%I.%I', table_schema, table_name), column_name) IS NOT NULL
        ORDER BY ordinal_position
        """;

    foreach (var table in tables)
    {
        await using var command = new NpgsqlCommand(sql, target);
        command.Parameters.AddWithValue("table", table.Name);
        await using var reader = await command.ExecuteReaderAsync();
        var sequenceColumns = new List<(string Column, string Sequence)>();

        while (await reader.ReadAsync())
        {
            sequenceColumns.Add((reader.GetString(0), reader.GetString(1)));
        }

        await reader.CloseAsync();

        foreach (var (column, sequence) in sequenceColumns)
        {
            await ResetSequence(target, table, column, sequence);
        }
    }
}

static async Task ResetSequence(NpgsqlConnection target, TableInfo table, string column, string sequence)
{
    var sql = $"""
        SELECT setval(
            @sequence::regclass,
            COALESCE((SELECT MAX({QuoteIdentifier(column)}) FROM {QuoteQualified("public", table.Name)}), 1),
            (SELECT COUNT({QuoteIdentifier(column)}) > 0 FROM {QuoteQualified("public", table.Name)})
        )
        """;

    await using var command = new NpgsqlCommand(sql, target);
    command.Parameters.AddWithValue("sequence", sequence);
    await command.ExecuteNonQueryAsync();
}

static bool Confirm()
{
    Console.Write("Type YES to overwrite local database: ");
    return string.Equals(Console.ReadLine(), "YES", StringComparison.Ordinal);
}

static string QuoteQualified(string schema, string name) => $"{QuoteIdentifier(schema)}.{QuoteIdentifier(name)}";

static string QuoteIdentifier(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

static string MaskPassword(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    if (!string.IsNullOrEmpty(builder.Password))
    {
        builder.Password = "***";
    }

    return builder.ConnectionString;
}

static void PrintHelp()
{
    Console.WriteLine("""
        Copies the VPS Postgres database into the local Postgres database.

        Defaults:
          opens SSH tunnel: 127.0.0.1:15432 -> VPS family-expenses-postgres:5432
          source: Host=localhost;Port=15432;Database=family_expenses;Username=family_expenses;Password=***
          target: Host=localhost;Port=5432;Database=family_expenses;Username=family_expenses;Password=***

        Usage:
          dotnet run --project .\utils\DatabaseSync\FamilyExpenses.DatabaseSync.csproj -- --yes

        Options:
          --source "<connection string>"  Source/VPS Postgres connection string.
          --target "<connection string>"  Target/local Postgres connection string.
          --no-tunnel                     Use source connection as-is; do not open SSH tunnel.
          --vps-host "<host>"             VPS SSH host. Default: 132.243.21.254.
          --ssh-user "<user>"             VPS SSH user. Default: root.
          --tunnel-port <port>            Local tunnel port. Default: 15432.
          --remote-container "<name>"     VPS Postgres container. Default: family-expenses-postgres.
          --yes                           Do not ask for confirmation.
          --force                         Allow non-default target host/port.
          --help                          Show help.

        Environment:
          VPS_POSTGRES                    Source connection string override.
          LOCAL_POSTGRES                  Target connection string override.
          FIRSTBYTE_PASSWORD              VPS SSH password for automatic tunnel.
        """);
}

static string ShellQuote(string value) => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";

sealed record ForeignKeyInfo(string TableName, string ReferencedTableName);

sealed record TableInfo(string Name)
{
    public List<string> Columns { get; } = [];
}

sealed record SyncOptions(
    string? SourceConnectionString,
    string? TargetConnectionString,
    bool OpenTunnel,
    string VpsHost,
    string SshUser,
    int TunnelPort,
    string RemotePostgresContainer,
    bool Yes,
    bool Force,
    bool ShowHelp)
{
    public static SyncOptions Parse(string[] args)
    {
        string? source = null;
        string? target = null;
        var openTunnel = true;
        var vpsHost = Defaults.VpsHost;
        var sshUser = Defaults.SshUser;
        var tunnelPort = Defaults.TunnelPort;
        var remotePostgresContainer = Defaults.RemotePostgresContainer;
        var yes = false;
        var force = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--source":
                    source = ReadValue(args, ref i, "--source");
                    break;
                case "--target":
                    target = ReadValue(args, ref i, "--target");
                    break;
                case "--no-tunnel":
                    openTunnel = false;
                    break;
                case "--vps-host":
                    vpsHost = ReadValue(args, ref i, "--vps-host");
                    break;
                case "--ssh-user":
                    sshUser = ReadValue(args, ref i, "--ssh-user");
                    break;
                case "--tunnel-port":
                    tunnelPort = ReadPort(ReadValue(args, ref i, "--tunnel-port"), "--tunnel-port");
                    break;
                case "--remote-container":
                    remotePostgresContainer = ReadValue(args, ref i, "--remote-container");
                    break;
                case "--yes":
                case "-y":
                    yes = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return new SyncOptions(
            source,
            target,
            openTunnel,
            vpsHost,
            sshUser,
            tunnelPort,
            remotePostgresContainer,
            yes,
            force,
            showHelp);
    }

    private static string ReadValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{name} requires a value.");
        }

        index++;
        return args[index];
    }

    private static int ReadPort(string value, string name)
    {
        if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
        {
            throw new ArgumentException($"{name} must be a TCP port from 1 to 65535.");
        }

        return port;
    }
}

static class Defaults
{
    public const string VpsHost = "132.243.21.254";
    public const string SshUser = "root";
    public const int TunnelPort = 15432;
    public const string RemotePostgresContainer = "family-expenses-postgres";
}

sealed class SshTunnel(SshClient client, ForwardedPortLocal port) : IDisposable
{
    public void Dispose()
    {
        if (port.IsStarted)
        {
            port.Stop();
        }

        port.Dispose();

        if (client.IsConnected)
        {
            client.Disconnect();
        }

        client.Dispose();
    }
}
