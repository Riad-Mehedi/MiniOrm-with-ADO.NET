using Npgsql;
using MiniOrm.Data;
using MiniOrm.Models;

namespace MiniOrm.Migrations.Commands;

/// <summary>
/// Implements the four migration CLI commands.
///
/// add      — generates a timestamped .sql file (up + down sections).
///            Reads entity metadata from TypeMapper. Does NOT touch the database.
/// apply    — runs every pending .sql file in order, records each in __migrations.
/// list     — prints [applied] or [pending] for every migration file.
/// rollback — executes the -- down section of the last applied migration.
/// </summary>
public class MigrationRunner
{
    private readonly string _connStr;
    private readonly string _dir;

    public MigrationRunner(string connStr, string dir)
    {
        _connStr = connStr;
        _dir     = dir;
        Directory.CreateDirectory(dir);
    }

    // ── ADD ──────────────────────────────────────────────────────────────────

    public void Add(string name)
    {
        // Build CREATE TABLE DDL from reflection metadata — same TypeMapper
        // that the ORM uses, so the SQL always matches the C# models.
        var productMeta = TypeMapper.GetMetadata<Product>();
        var orderMeta   = TypeMapper.GetMetadata<Order>();

        string up =
            GenerateCreate(productMeta) + "\n\n" +
            GenerateCreate(orderMeta);

        string down =
            $"DROP TABLE IF EXISTS {productMeta.TableName};\n" +
            $"DROP TABLE IF EXISTS {orderMeta.TableName};";

        var ts   = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var path = Path.Combine(_dir, $"{ts}_{name}.sql");

        File.WriteAllText(path, $"-- up\n{up}\n\n-- down\n{down}");
        Console.WriteLine($"✓ Created migration: {Path.GetFileName(path)}");
    }

    // ── APPLY ────────────────────────────────────────────────────────────────

    public void Apply()
    {
        EnsureMigrationsTable();

        var applied = GetApplied();
        var pending = GetFiles()
            .Where(f => !applied.Contains(Path.GetFileName(f)))
            .OrderBy(f => f)
            .ToList();

        if (!pending.Any())
        {
            Console.WriteLine("Nothing to apply — database is up to date.");
            return;
        }

        using var conn = Open();
        foreach (var file in pending)
        {
            // Extract the -- up section (everything between -- up and -- down)
            var upSql = Extract(File.ReadAllText(file), "-- up", "-- down");
            new NpgsqlCommand(upSql, conn).ExecuteNonQuery();

            // Record this migration as applied
            var ins = new NpgsqlCommand(
                "INSERT INTO __migrations (name, applied_at) VALUES (@n, @t)", conn);
            ins.Parameters.AddWithValue("@n", Path.GetFileName(file));
            ins.Parameters.AddWithValue("@t", DateTime.UtcNow);
            ins.ExecuteNonQuery();

            Console.WriteLine($"✓ Applied: {Path.GetFileName(file)}");
        }
    }

    // ── LIST ─────────────────────────────────────────────────────────────────

    public void List()
    {
        EnsureMigrationsTable();

        var applied = GetApplied();
        var files   = GetFiles().OrderBy(f => f).ToList();

        if (!files.Any())
        {
            Console.WriteLine("No migration files found. Run: dotnet run -- migrations add <Name>");
            return;
        }

        foreach (var f in files)
        {
            var n      = Path.GetFileName(f);
            var status = applied.Contains(n) ? "[applied]" : "[pending]";
            Console.WriteLine($"  {status}  {n}");
        }
    }

    // ── ROLLBACK ─────────────────────────────────────────────────────────────

    public void Rollback()
    {
        EnsureMigrationsTable();

        var last = GetApplied().LastOrDefault();
        if (last == null)
        {
            Console.WriteLine("Nothing to rollback.");
            return;
        }

        var path = Path.Combine(_dir, last);
        if (!File.Exists(path))
        {
            Console.WriteLine($"Migration file not found: {path}");
            return;
        }

        // Extract the -- down section (everything after -- down)
        var down = Extract(File.ReadAllText(path), "-- down", null);

        using var conn = Open();
        new NpgsqlCommand(down, conn).ExecuteNonQuery();

        var del = new NpgsqlCommand(
            "DELETE FROM __migrations WHERE name = @n", conn);
        del.Parameters.AddWithValue("@n", last);
        del.ExecuteNonQuery();

        Console.WriteLine($"✓ Rolled back: {last}");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void EnsureMigrationsTable()
    {
        using var conn = Open();
        new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS __migrations (
                id         SERIAL    PRIMARY KEY,
                name       TEXT      NOT NULL,
                applied_at TIMESTAMP NOT NULL
            )", conn).ExecuteNonQuery();
    }

    private List<string> GetApplied()
    {
        var list = new List<string>();
        try
        {
            using var conn = Open();
            using var r    = new NpgsqlCommand(
                "SELECT name FROM __migrations ORDER BY id", conn).ExecuteReader();
            while (r.Read())
                list.Add(r.GetString(0));
        }
        catch { /* __migrations may not exist yet on very first run */ }
        return list;
    }

    private NpgsqlConnection Open()
    {
        var c = new NpgsqlConnection(_connStr);
        c.Open();
        return c;
    }

    private IEnumerable<string> GetFiles()
        => Directory.GetFiles(_dir, "*.sql");

    private static string Extract(string content, string start, string? end)
    {
        int s = content.IndexOf(start) + start.Length;
        int e = end != null ? content.IndexOf(end) : content.Length;
        return content[s..e].Trim();
    }

    /// <summary>
    /// Generates a CREATE TABLE IF NOT EXISTS statement from EntityMetadata.
    /// Uses PostgresType and IsNullable from TypeMapper to match the C# model exactly.
    /// </summary>
    private static string GenerateCreate(EntityMetadata meta)
    {
        var colDefs = meta.Columns.Select(c =>
        {
            string def = $"  {c.ColumnName} {c.PostgresType}";
            if (c.IsPrimaryKey)
                def += " PRIMARY KEY";
            else
                def += c.IsNullable ? " NULL" : " NOT NULL";
            return def;
        });

        return $"CREATE TABLE IF NOT EXISTS {meta.TableName} (\n"
               + string.Join(",\n", colDefs)
               + "\n);";
    }
}