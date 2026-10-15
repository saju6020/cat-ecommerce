using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations.Reporting;

/// <summary>
/// HO-RPT-001 section 12.1 -- every <c>report.rv_*</c> row type is keyless and mapped with <c>ToView(name, "report")</c>: EF reads it, never writes it, and a generated migration contains no
/// <c>CreateTable</c> for it (F-RPT-3). The view itself is hand-written SQL in a Reporting migration. Each row property maps to the snake_case column of the same name, so a row type is
/// declared once and the contract column list reads straight off the class.
/// </summary>
public abstract class ReportViewConfiguration<TRow>(string viewName) : IEntityTypeConfiguration<TRow> where TRow : class
{
    public const string Schema = "report";

    public void Configure(EntityTypeBuilder<TRow> builder)
    {
        builder.HasNoKey();
        builder.ToView(viewName, Schema);

        foreach (var property in typeof(TRow).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            builder.Property(property.Name).HasColumnName(ToSnakeCase(property.Name));
    }

    public static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) builder.Append('_');
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
