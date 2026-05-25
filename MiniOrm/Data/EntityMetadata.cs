using System.Reflection;

namespace MiniOrm.Data;

/// <summary>
/// Describes a single mapped column — its CLR property, Postgres column name,
/// DDL type, and whether it is nullable.  Built once by TypeMapper at startup.
/// </summary>
public class ColumnMetadata
{
    public PropertyInfo Property     { get; init; } = null!;
    public string       ColumnName   { get; init; } = null!;
    public bool         IsPrimaryKey { get; init; }
    public string       PostgresType { get; init; } = null!;
    public bool         IsNullable   { get; init; }
}

/// <summary>
/// Full runtime description of one entity type: table name, all mapped columns,
/// and a direct reference to the primary-key column metadata.
/// </summary>
public class EntityMetadata
{
    public string               TableName  { get; init; } = null!;
    public ColumnMetadata       PrimaryKey { get; init; } = null!;
    public List<ColumnMetadata> Columns    { get; init; } = [];

    /// <summary>
    /// Every column except the PK.  Used to build INSERT / UPDATE statements
    /// so the SERIAL primary key is never included in the parameter list.
    /// </summary>
    public IEnumerable<ColumnMetadata> NonPkColumns =>
        Columns.Where(c => !c.IsPrimaryKey);
}