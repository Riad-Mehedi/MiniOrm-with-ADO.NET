using Npgsql;

namespace MiniOrm.Data;

/// Abstract base class for all application contexts.
///
/// Responsibilities:
///   1. Owns the NpgsqlConnection and opens it lazily.
///   2. On construction, reflects over the concrete subclass to find every
///      DbSet&lt;T&gt; property and sets it to a new instance — exactly what
///      Entity Framework Core does internally.
public abstract class DbContext : IDisposable
{
    private readonly string _connectionString;
    protected NpgsqlConnection? _connection;

    protected DbContext(string connectionString)
    {
        _connectionString = connectionString;
        InitDbSets();
    }

    /// Returns the open connection, opening it if it is null or closed.
    /// A single connection is reused for the lifetime of the context.
    public NpgsqlConnection GetConnection()
    {
        if (_connection == null ||
            _connection.State == System.Data.ConnectionState.Closed)
        {
            _connection = new NpgsqlConnection(_connectionString);
            _connection.Open();
        }
        return _connection;
    }

    /// Finds every property of type DbSet&lt;T&gt; on the concrete subclass and
    /// constructs an instance of DbSet&lt;T&gt; passing 'this' as the context.
    /// This is why subclass properties do not need to be initialised manually.
    private void InitDbSets()
    {
        var props = GetType().GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

        foreach (var prop in props)
        {
            var entityType = prop.PropertyType.GetGenericArguments()[0];
            var dbSetType  = typeof(DbSet<>).MakeGenericType(entityType);
            var instance   = Activator.CreateInstance(dbSetType, this);
            prop.SetValue(this, instance);
        }
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}