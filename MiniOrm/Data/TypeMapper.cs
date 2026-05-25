using System.Reflection;
using MiniOrm.Attributes;

namespace MiniOrm.Data;

/// <summary>
/// Builds EntityMetadata for a given entity type T using C# reflection.
///
/// Rules:
///   1. The class MUST have [Table("name")].
///   2. Only properties with BOTH [Column] and optionally [PrimaryKey] are mapped.
///      Any property missing [Column] is silently skipped — this prevents
///      navigation properties from becoming columns.
///   3. The primary key property must also have [Column("id")] alongside [PrimaryKey].
///   4. Nullable value types (int?, decimal? …) are detected via
///      Nullable.GetUnderlyingType() and map to NULL columns.
///   5. Nullable reference types (string?) are detected via NullabilityInfoContext
///      (.NET 6+) and also map to NULL columns.
/// </summary>
public static class TypeMapper
{
    // NullabilityInfoContext is not thread-safe — create one per call.
    public static EntityMetadata GetMetadata<T>()
    {
        Type type = typeof(T);

        var tableAttr = type.GetCustomAttribute<TableAttribute>()
            ?? throw new InvalidOperationException(
                $"[Table] attribute is missing on class '{type.Name}'. " +
                "Add [Table(\"your_table_name\")] above the class declaration.");

        var nullCtx  = new NullabilityInfoContext();
        var columns  = new List<ColumnMetadata>();
        ColumnMetadata? pk = null;

        foreach (PropertyInfo prop in type.GetProperties())
        {
            bool isPk    = prop.GetCustomAttribute<PrimaryKeyAttribute>() != null;
            var  colAttr = prop.GetCustomAttribute<ColumnAttribute>();

            // Skip properties that have neither [Column] nor [PrimaryKey].
            if (!isPk && colAttr == null)
                continue;

            // Require [Column] even on PK properties — the column name must be explicit.
            if (colAttr == null)
                throw new InvalidOperationException(
                    $"Property '{prop.Name}' on '{type.Name}' has [PrimaryKey] " +
                    $"but is missing [Column(\"column_name\")]. " +
                    "Both attributes are required on the primary key property.");

            string columnName = colAttr.Name;

            // Resolve Postgres type and nullability from the CLR type.
            (string pgType, bool isNullable) = ResolveType(prop, isPk, nullCtx);

            var meta = new ColumnMetadata
            {
                Property     = prop,
                ColumnName   = columnName,
                IsPrimaryKey = isPk,
                PostgresType = pgType,
                IsNullable   = isNullable,
            };

            columns.Add(meta);
            if (isPk) pk = meta;
        }

        if (pk == null)
            throw new InvalidOperationException(
                $"No [PrimaryKey] attribute found on any property of '{type.Name}'.");

        return new EntityMetadata
        {
            TableName  = tableAttr.Name,
            PrimaryKey = pk,
            Columns    = columns,
        };
    }

    // ── Type resolution ───────────────────────────────────────────────────────

    private static (string pgType, bool isNullable) ResolveType(
        PropertyInfo prop, bool isPk, NullabilityInfoContext nullCtx)
    {
        Type t = prop.PropertyType;

        // Nullable<T> — e.g. int?, decimal?, bool?
        Type? underlying = Nullable.GetUnderlyingType(t);
        bool  isNullable = underlying != null;   // true for value-type T?
        Type  core       = underlying ?? t;

        // int PK → SERIAL (PostgreSQL auto-increment)
        if (isPk && core == typeof(int))
            return ("SERIAL", false);

        string pg = core switch
        {
            _ when core == typeof(int)      => "INTEGER",
            _ when core == typeof(long)     => "BIGINT",
            _ when core == typeof(float)    => "REAL",
            _ when core == typeof(double)   => "DOUBLE PRECISION",
            _ when core == typeof(decimal)  => "NUMERIC",
            _ when core == typeof(bool)     => "BOOLEAN",
            _ when core == typeof(DateTime) => "TIMESTAMP",
            _ when core == typeof(Guid)     => "UUID",
            _ when core == typeof(string)   => "TEXT",
            _ => throw new NotSupportedException(
                $"No Postgres mapping defined for C# type '{core.Name}' " +
                $"(property '{prop.Name}').")
        };

        // For reference types (string), Nullable.GetUnderlyingType is always null
        // because string is already a reference type.  Use NullabilityInfoContext
        // to detect whether the property was declared as string? or string.
        if (!core.IsValueType)
        {
            var info = nullCtx.Create(prop);
            isNullable = info.WriteState == NullabilityState.Nullable;
        }

        return (pg, isNullable);
    }
}