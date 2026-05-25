using Npgsql;
using NpgsqlTypes;

namespace MiniOrm.Data;

/// <summary>
/// Generic repository for entity type T.
///
/// All SQL is built at runtime from EntityMetadata — no table name or column
/// name is ever hardcoded in this class.  The same code handles Product,
/// Order, and any future entity.
///
/// Safety rules enforced throughout:
///   • Every value is passed through NpgsqlParameter — never string-concatenated.
///   • C# null → DBNull.Value when setting parameters.
///   • reader.IsDBNull() is checked before every GetValue() call.
///   • Nullable.GetUnderlyingType() strips the T? wrapper before Convert.ChangeType.
///
/// Npgsql 10 note:
///   Npgsql 10 requires an explicit NpgsqlDbType for numeric types (decimal/NUMERIC).
///   AddTypedParameter() handles this mapping so no runtime type-inference errors occur.
/// </summary>
public class DbSet<T> where T : new()
{
    private readonly DbContext      _ctx;
    private readonly EntityMetadata _meta;

    public DbSet(DbContext ctx)
    {
        _ctx  = ctx;
        _meta = TypeMapper.GetMetadata<T>();
    }

    // ── INSERT ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a parameterised INSERT for all non-PK columns and returns
    /// the new SERIAL id via RETURNING id — one round-trip.
    /// </summary>
    public int Insert(T entity)
    {
        var cols  = _meta.NonPkColumns.ToList();
        var names = string.Join(", ", cols.Select(c => c.ColumnName));
        var prms  = string.Join(", ", cols.Select(c => "@" + c.ColumnName));
        var sql   = $"INSERT INTO {_meta.TableName} ({names}) VALUES ({prms}) RETURNING id";

        using var cmd = new NpgsqlCommand(sql, _ctx.GetConnection());

        foreach (var col in cols)
        {
            var value = col.Property.GetValue(entity);
            AddTypedParameter(cmd, "@" + col.ColumnName, value, col);
        }

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ── FIND BY ID ───────────────────────────────────────────────────────────

    public T? FindById(int id)
    {
        var sql = $"SELECT * FROM {_meta.TableName} WHERE id = @id";
        using var cmd = new NpgsqlCommand(sql, _ctx.GetConnection());
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapRow(reader) : default;
    }

    // ── GET ALL ──────────────────────────────────────────────────────────────

    public IEnumerable<T> GetAll()
    {
        var list = new List<T>();
        using var cmd = new NpgsqlCommand(
            $"SELECT * FROM {_meta.TableName}", _ctx.GetConnection());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(MapRow(reader));
        return list;
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    public void Update(T entity)
    {
        var cols      = _meta.NonPkColumns.ToList();
        var setClause = string.Join(", ",
            cols.Select(c => $"{c.ColumnName} = @{c.ColumnName}"));
        var sql = $"UPDATE {_meta.TableName} SET {setClause} WHERE id = @id";

        using var cmd = new NpgsqlCommand(sql, _ctx.GetConnection());

        foreach (var col in cols)
        {
            var value = col.Property.GetValue(entity);
            AddTypedParameter(cmd, "@" + col.ColumnName, value, col);
        }

        // PK value for WHERE id = @id
        cmd.Parameters.AddWithValue("@id",
            _meta.PrimaryKey.Property.GetValue(entity)!);

        cmd.ExecuteNonQuery();
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    public void Delete(int id)
    {
        using var cmd = new NpgsqlCommand(
            $"DELETE FROM {_meta.TableName} WHERE id = @id",
            _ctx.GetConnection());
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // ── ROW → ENTITY ─────────────────────────────────────────────────────────

    private T MapRow(NpgsqlDataReader reader)
    {
        var entity = new T();

        foreach (var col in _meta.Columns)
        {
            var ordinal = reader.GetOrdinal(col.ColumnName);

            if (reader.IsDBNull(ordinal))
            {
                // SQL NULL → C# null (works for both reference types and T?)
                col.Property.SetValue(entity, null);
                continue;
            }

            var raw = reader.GetValue(ordinal);

            // Strip Nullable<T> wrapper so Convert.ChangeType works correctly.
            // e.g. for decimal? the property type is Nullable<decimal>;
            // Convert.ChangeType cannot target Nullable<decimal> directly.
            var targetType = Nullable.GetUnderlyingType(col.Property.PropertyType)
                             ?? col.Property.PropertyType;

            col.Property.SetValue(entity, Convert.ChangeType(raw, targetType));
        }

        return entity;
    }

    // ── Npgsql 10 typed parameter helper ─────────────────────────────────────

    /// <summary>
    /// Npgsql 10 removed automatic .NET → Postgres type inference for some types
    /// (notably decimal → NUMERIC).  This method maps each ColumnMetadata to its
    /// explicit NpgsqlDbType so parameters are always unambiguous.
    ///
    /// Null values are sent as DBNull.Value regardless of type.
    /// </summary>
    private static void AddTypedParameter(
        NpgsqlCommand cmd, string paramName, object? value, ColumnMetadata col)
    {
        // Resolve the core CLR type (strip Nullable<T> wrapper)
        var propType = col.Property.PropertyType;
        var coreType = Nullable.GetUnderlyingType(propType) ?? propType;

        // Map core CLR type to explicit NpgsqlDbType
        NpgsqlDbType npgsqlType = coreType switch
        {
            _ when coreType == typeof(int)      => NpgsqlDbType.Integer,
            _ when coreType == typeof(long)     => NpgsqlDbType.Bigint,
            _ when coreType == typeof(float)    => NpgsqlDbType.Real,
            _ when coreType == typeof(double)   => NpgsqlDbType.Double,
            _ when coreType == typeof(decimal)  => NpgsqlDbType.Numeric,
            _ when coreType == typeof(bool)     => NpgsqlDbType.Boolean,
            _ when coreType == typeof(DateTime) => NpgsqlDbType.Timestamp,
            _ when coreType == typeof(Guid)     => NpgsqlDbType.Uuid,
            _ when coreType == typeof(string)   => NpgsqlDbType.Text,
            _ => throw new NotSupportedException(
                $"No NpgsqlDbType mapping for '{coreType.Name}' on column '{col.ColumnName}'")
        };

        var param = new NpgsqlParameter(paramName, npgsqlType);

        // C# null or DBNull → SQL NULL
        param.Value = value ?? DBNull.Value;

        cmd.Parameters.Add(param);
    }
}