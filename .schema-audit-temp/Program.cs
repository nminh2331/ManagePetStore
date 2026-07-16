using System.Text.Json;
using System.Text.RegularExpressions;
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: SchemaAudit <database.sql>");
    return 2;
}

var sql = File.ReadAllText(args[0]);
var sqlTables = ParseSqlTables(sql);
var options = new DbContextOptionsBuilder<PetStoreManagementContext>()
    .UseSqlServer("Server=(local);Database=SchemaAudit;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;
using var context = new PetStoreManagementContext(options);

var missingTables = new List<string>();
var missingColumns = new List<object>();
var typeMismatches = new List<object>();
var missingIndexes = new List<object>();
var missingForeignKeys = new List<object>();

foreach (var entity in context.Model.GetEntityTypes().Where(entity => entity.GetTableName() != null))
{
    var table = entity.GetTableName()!;
    var schema = entity.GetSchema() ?? "dbo";
    var store = StoreObjectIdentifier.Table(table, schema);
    if (!sqlTables.TryGetValue(table, out var sqlColumns))
    {
        missingTables.Add(table);
        continue;
    }

    foreach (var property in entity.GetProperties())
    {
        var column = property.GetColumnName(store);
        if (column == null) continue;
        if (!sqlColumns.TryGetValue(column, out var sqlType))
        {
            missingColumns.Add(new { Table = table, Column = column, Entity = entity.ClrType.Name });
            continue;
        }

        var modelType = NormalizeType(property.GetColumnType());
        if (modelType.Length > 0 && sqlType.Length > 0 && modelType != sqlType)
        {
            typeMismatches.Add(new { Table = table, Column = column, ModelType = modelType, SqlType = sqlType });
        }
    }

    foreach (var index in entity.GetIndexes())
    {
        var name = index.GetDatabaseName();
        if (!string.IsNullOrWhiteSpace(name) && !sql.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            missingIndexes.Add(new
            {
                Table = table,
                Name = name,
                index.IsUnique,
                Filter = index.GetFilter(),
                Columns = index.Properties.Select(property => property.GetColumnName(store)).ToArray()
            });
        }
    }

    foreach (var foreignKey in entity.GetForeignKeys())
    {
        var name = foreignKey.GetConstraintName();
        if (!string.IsNullOrWhiteSpace(name) && !sql.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            missingForeignKeys.Add(new
            {
                Table = table,
                Name = name,
                Columns = foreignKey.Properties.Select(property => property.GetColumnName(store)).ToArray(),
                PrincipalTable = foreignKey.PrincipalEntityType.GetTableName()
            });
        }
    }
}

var result = new
{
    ModelTableCount = context.Model.GetEntityTypes().Count(entity => entity.GetTableName() != null),
    ParsedSqlTableCount = sqlTables.Count,
    MissingTables = missingTables.Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(),
    MissingColumns = missingColumns,
    TypeMismatches = typeMismatches,
    MissingIndexes = missingIndexes,
    MissingForeignKeys = missingForeignKeys
};
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
return 0;

static Dictionary<string, Dictionary<string, string>> ParseSqlTables(string sql)
{
    var tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    var createRegex = new Regex(@"CREATE\s+TABLE\s+(?<table>(?:\[?dbo\]?\.)?\[?[A-Za-z_][A-Za-z0-9_]*\]?)\s*\(", RegexOptions.IgnoreCase);
    foreach (Match match in createRegex.Matches(sql))
    {
        var table = CleanIdentifier(match.Groups["table"].Value.Split('.').Last());
        if (!tables.TryGetValue(table, out var columns))
        {
            columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            tables[table] = columns;
        }

        var openIndex = match.Index + match.Length - 1;
        var closeIndex = FindClosingParenthesis(sql, openIndex);
        if (closeIndex < 0) continue;
        var body = sql[(openIndex + 1)..closeIndex];
        foreach (var line in body.Split('\n'))
        {
            var columnMatch = Regex.Match(line,
                @"^\s*\[?(?<column>[A-Za-z_][A-Za-z0-9_]*)\]?\s+(?<type>(?:BIGINT|INT|SMALLINT|TINYINT|BIT|DECIMAL|NUMERIC|MONEY|FLOAT|REAL|DATETIME2?|DATE|TIME|NVARCHAR|VARCHAR|NCHAR|CHAR|VARBINARY|BINARY|UNIQUEIDENTIFIER|XML)(?:\s*\([^\)]*\))?)\b",
                RegexOptions.IgnoreCase);
            if (columnMatch.Success)
            {
                columns[CleanIdentifier(columnMatch.Groups["column"].Value)] = NormalizeType(columnMatch.Groups["type"].Value);
            }
        }
    }

    var alterRegex = new Regex(
        @"ALTER\s+TABLE\s+(?<table>(?:\[?dbo\]?\.)?\[?[A-Za-z_][A-Za-z0-9_]*\]?)\s+ADD\s+\[?(?<column>[A-Za-z_][A-Za-z0-9_]*)\]?\s+(?<type>(?:BIGINT|INT|SMALLINT|TINYINT|BIT|DECIMAL|NUMERIC|MONEY|FLOAT|REAL|DATETIME2?|DATE|TIME|NVARCHAR|VARCHAR|NCHAR|CHAR|VARBINARY|BINARY|UNIQUEIDENTIFIER|XML)(?:\s*\([^\)]*\))?)\b",
        RegexOptions.IgnoreCase);
    foreach (Match match in alterRegex.Matches(sql))
    {
        var table = CleanIdentifier(match.Groups["table"].Value.Split('.').Last());
        if (!tables.TryGetValue(table, out var columns))
        {
            columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            tables[table] = columns;
        }
        columns[CleanIdentifier(match.Groups["column"].Value)] = NormalizeType(match.Groups["type"].Value);
    }
    return tables;
}

static int FindClosingParenthesis(string text, int openIndex)
{
    var depth = 0;
    var inString = false;
    for (var index = openIndex; index < text.Length; index++)
    {
        var character = text[index];
        if (character == '\'' && (index + 1 >= text.Length || text[index + 1] != '\''))
        {
            inString = !inString;
            continue;
        }
        if (inString) continue;
        if (character == '(') depth++;
        if (character == ')' && --depth == 0) return index;
    }
    return -1;
}

static string CleanIdentifier(string value) => value.Trim().Trim('[', ']');

static string NormalizeType(string? value) => Regex.Replace(value?.ToLowerInvariant() ?? string.Empty, @"\s+", string.Empty);
