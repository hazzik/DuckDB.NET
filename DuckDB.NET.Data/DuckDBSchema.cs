using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace DuckDB.NET.Data;

internal static class DuckDBSchema
{
    private static readonly string[] TableRestrictions =
        ["table_catalog", "table_schema", "table_name", "table_type"];

    private static readonly string[] ColumnRestrictions =
        ["table_catalog", "table_schema", "table_name", "column_name"];

    private static readonly string[] ForeignKeyRestrictions =
        ["constraint_catalog", "constraint_schema", "table_name", "constraint_name"];

    private static readonly string[] IndexesRestrictions =
        ["index_catalog", "index_schema", "table_name", "index_name"];

    public static DataTable GetSchema(DuckDBConnection connection, string collectionName, string?[]? restrictionValues) =>
        collectionName.ToUpperInvariant() switch
        {
            "METADATACOLLECTIONS" => GetMetaDataCollections(),
            "RESTRICTIONS" => GetRestrictions(),
            "RESERVEDWORDS" => GetReservedWords(connection),
            "TABLES" => GetTables(connection, restrictionValues),
            "COLUMNS" => GetColumns(connection, restrictionValues),
            "FOREIGNKEYS" => GetForeignKeys(connection, restrictionValues),
            "INDEXES" => GetIndexes(connection, restrictionValues),
            _ => throw new ArgumentOutOfRangeException(nameof(collectionName), collectionName, "Invalid collection name.")
        };

    private static DataTable GetMetaDataCollections() =>
        new(DbMetaDataCollectionNames.MetaDataCollections)
        {
            Columns =
            {
                { DbMetaDataColumnNames.CollectionName, typeof(string) },
                { DbMetaDataColumnNames.NumberOfRestrictions, typeof(int) },
                { DbMetaDataColumnNames.NumberOfIdentifierParts, typeof(int) }
            },
            Rows =
            {
                { DbMetaDataCollectionNames.MetaDataCollections, 0, 0 },
                { DbMetaDataCollectionNames.Restrictions, 0, 0 },
                { DbMetaDataCollectionNames.ReservedWords, 0, 0 },
                { DuckDbMetaDataCollectionNames.Tables, TableRestrictions.Length, 3 },
                { DuckDbMetaDataCollectionNames.Columns, ColumnRestrictions.Length, 4 },
                { DuckDbMetaDataCollectionNames.ForeignKeys, ForeignKeyRestrictions.Length, 3 },
                { DuckDbMetaDataCollectionNames.Indexes, IndexesRestrictions.Length, 3 },
            }
        };

    private static DataTable GetRestrictions() =>
        new DataTable(DbMetaDataCollectionNames.Restrictions)
            {
                Columns =
                {
                    { "CollectionName", typeof(string) },
                    { "RestrictionName", typeof(string) },
                    { "RestrictionDefault", typeof(string) },
                    { "RestrictionNumber", typeof(int) }
                },
            }
            .AddRestrictions(DuckDbMetaDataCollectionNames.Tables, TableRestrictions)
            .AddRestrictions(DuckDbMetaDataCollectionNames.Columns, ColumnRestrictions)
            .AddRestrictions(DuckDbMetaDataCollectionNames.ForeignKeys, ForeignKeyRestrictions)
            .AddRestrictions(DuckDbMetaDataCollectionNames.Indexes, IndexesRestrictions);

    private static DataTable GetReservedWords(DuckDBConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT keyword_name as ReservedWord FROM duckdb_keywords() WHERE keyword_category = 'reserved'";
        command.CommandType = CommandType.Text;
        return GetDataTable(DbMetaDataCollectionNames.ReservedWords, command);
    }

    private static DataTable GetTables(DuckDBConnection connection, string?[]? restrictionValues)
    {
        if (restrictionValues?.Length > TableRestrictions.Length)
            throw new ArgumentException("Too many restrictions", nameof(restrictionValues));

        const string query = "SELECT table_catalog, table_schema, table_name, table_type FROM information_schema.tables";

        using var command = BuildCommand(connection, query, restrictionValues, true, TableRestrictions);

        return GetDataTable(DuckDbMetaDataCollectionNames.Tables, command);
    }

    private static DataTable GetColumns(DuckDBConnection connection, string?[]? restrictionValues)
    {
        if (restrictionValues?.Length > ColumnRestrictions.Length)
            throw new ArgumentException("Too many restrictions", nameof(restrictionValues));

        const string query =
            """
            SELECT
                table_catalog, table_schema, table_name, column_name,
                ordinal_position, column_default, is_nullable, data_type,
                character_maximum_length, character_octet_length,
                numeric_precision, numeric_precision_radix,
                numeric_scale, datetime_precision,
                character_set_catalog, character_set_schema, character_set_name, collation_catalog 
            FROM information_schema.columns 
            """;
        using var command = BuildCommand(connection, query, restrictionValues, true, ColumnRestrictions);
        
        return GetDataTable(DuckDbMetaDataCollectionNames.Columns, command);
    } 
    
    private static DataTable GetForeignKeys(DuckDBConnection connection, string?[]? restrictionValues)
    {
        if (restrictionValues?.Length > ForeignKeyRestrictions.Length)
            throw new ArgumentException("Too many restrictions", nameof(restrictionValues));

        const string query =
            """
            SELECT 
                constraint_catalog, constraint_schema, constraint_name, 
                table_catalog, table_schema, table_name, constraint_type, 
                is_deferrable, initially_deferred 
            FROM information_schema.table_constraints
            WHERE constraint_type = 'FOREIGN KEY'
            """;
        using var command = BuildCommand(connection, query, restrictionValues, false, ForeignKeyRestrictions);
        
        return GetDataTable(DuckDbMetaDataCollectionNames.ForeignKeys, command);
    }
    
    private static DataTable GetIndexes(DuckDBConnection connection, string?[]? restrictionValues)
    {
        if (restrictionValues?.Length > IndexesRestrictions.Length)
            throw new ArgumentException("Too many restrictions", nameof(restrictionValues));

        const string query =
            """
            SELECT
            	database_name as index_catalog,
            	schema_name as index_schema,
            	index_name,
            	table_name,
            	is_unique,
            	is_primary
            FROM duckdb_indexes()
            """;
        using var command = BuildCommand(connection, query, restrictionValues, true, IndexesRestrictions);
        
        return GetDataTable(DuckDbMetaDataCollectionNames.Indexes, command);
    }

    private static DuckDBCommand BuildCommand(DuckDBConnection connection, string query, string?[]? restrictions,
        bool addWhere, string[]? restrictionNames)
    {
        var command = connection.CreateCommand();
        if (restrictions is not { Length: > 0 } || restrictionNames == null)
        {
            command.CommandText = query;
            return command;
        }

        var builder = new StringBuilder(query);
        foreach (var (name, restriction) in restrictionNames.Zip(restrictions, Tuple.Create))
        {
            if (restriction?.Length > 0)
            {
                if (addWhere)
                {
                    builder.Append(" WHERE ");
                    addWhere = false;
                }
                else
                {
                    builder.Append(" AND ");
                }

                builder.Append($"{name} = ${name}");
                command.Parameters.Add(new DuckDBParameter(name, restriction));
            }
        }

        command.CommandText = builder.ToString();
        return command;
    }

    private static DataTable GetDataTable(string tableName, DuckDBCommand command)
    {
        var table = new DataTable(tableName);
        try
        {
            using var reader = command.ExecuteReader();
            table.BeginLoadData();
            table.Load(reader);
        }
        finally
        {
            table.EndLoadData();
        }

        return table;
    }

    private static DataTable AddRestrictions(this DataTable table, string collectionName, string[] restrictions)
    {
        for (var index = 0; index < restrictions.Length; index++)
            table.Rows.Add(collectionName, restrictions[index], restrictions[index], index + 1);
        return table;
    }
}