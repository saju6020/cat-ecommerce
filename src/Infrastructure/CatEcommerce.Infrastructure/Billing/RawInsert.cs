using System.Data.Common;
using System.Text;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// <c>INSERT … ON CONFLICT (…) DO NOTHING</c> for one root, built from the EF model so every column
/// (including the platform base columns) is written exactly as <c>SaveChanges</c> would. Used where a
/// row must be created <b>outside</b> the caller's unit of work and a concurrent creator is expected
/// (HO-BIL-001 §8.7: the document series row -- a row with <c>next_sequence = 1</c> creates no gap).
/// </summary>
internal static class RawInsert
{
    public static async Task InsertIgnoringConflictAsync<T>(
        CatEcommerceDbContext db, T entity, IReadOnlyList<string> conflictColumns, CancellationToken ct) where T : class
    {
        var entityType = db.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException($"{typeof(T).Name} is not part of the model.");
        var table = entityType.GetTableName()!;
        var schema = entityType.GetSchema();
        var store = StoreObjectIdentifier.Table(table, schema);
        var entry = db.Entry(entity);

        using var host = db.Database.GetDbConnection().CreateCommand();
        var columns = new List<string>();
        var placeholders = new List<string>();
        var parameters = new List<DbParameter>();

        foreach (var property in entityType.GetProperties())
        {
            var column = property.GetColumnName(store);
            // xmin is a system column maintained by PostgreSQL, never written.
            if (column is null || property.IsConcurrencyToken) continue;

            var name = "@p" + parameters.Count;
            var value = entry.Property(property.Name).CurrentValue;
            parameters.Add(property.GetRelationalTypeMapping().CreateParameter(host, name, value, property.IsNullable));
            columns.Add("\"" + column + "\"");
            placeholders.Add(name);
        }

        var sql = new StringBuilder()
            .Append("INSERT INTO ").Append(schema is null ? string.Empty : $"\"{schema}\".").Append('"').Append(table).Append("\" (")
            .Append(string.Join(", ", columns)).Append(") VALUES (").Append(string.Join(", ", placeholders))
            .Append(") ON CONFLICT (").Append(string.Join(", ", conflictColumns.Select(c => "\"" + c + "\""))).Append(") DO NOTHING")
            .ToString();

        await db.Database.ExecuteSqlRawAsync(sql, parameters.Cast<object>().ToArray(), ct);
    }
}
