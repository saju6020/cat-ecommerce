using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.QueryHandlers.Portal;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.Tests.QueryHandlers.Portal;

/// <summary>HO-POR-001 §15 group A -- the boundary rules that fail the build rather than rely on review (ADR-257, 258, 259, 262), plus the
/// read-side ADR-012 guard the Billing area added as <c>BillingConventionTests.U_X6</c>.</summary>
public sealed class PortalConventionTests
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(PortalSectionComposer).Assembly,                                  // Read
        typeof(CatEcommerceDbContext).Assembly,                                  // Infrastructure
        typeof(CatEcommerce.Shared.Common.Constants.PortalRules).Assembly,       // Shared
        typeof(CatEcommerce.Domain.Aggregates.Cat.CatAggregate).Assembly,        // Domain
        typeof(CatEcommerce.Application.Commands.Cat.RegisterCatCommand).Assembly, // Application
    ];

    private static IEnumerable<Type> PortalTypes() =>
        ProductionAssemblies.SelectMany(a => a.GetTypes()).Where(t => t.Namespace is not null && t.Namespace.Contains(".Portal", StringComparison.Ordinal));

    // U-A1 -- ADR-257
    [Fact]
    public void U_A1_NoPortalTypeIsAnAggregate_AnEvent_AConfiguration_OrAnEventHandler()
    {
        var types = PortalTypes().ToList();
        Assert.NotEmpty(types);

        var offenders = types.Where(t =>
                typeof(AggregateRoot).IsAssignableFrom(t)
                || typeof(Event).IsAssignableFrom(t)
                || t.GetInterfaces().Any(i => i.IsGenericType && (
                    i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
                    || i.GetGenericTypeDefinition() == typeof(IEventHandlerAsync<>))))
            .Select(t => t.FullName).ToList();

        Assert.True(offenders.Count == 0, "The Portal context owns no aggregate, event, configuration or event handler: " + string.Join(", ", offenders));
    }

    [Fact]
    public void U_A1_ThereIsNoPortalSchemaTableOrDbSet()
    {
        Assert.DoesNotContain(typeof(CatEcommerceDbContext).GetProperties(), p => p.Name.StartsWith("Portal", StringComparison.Ordinal));
        var configurations = typeof(CatEcommerceDbContext).Assembly.GetTypes().Where(t => t.Name.StartsWith("Portal", StringComparison.Ordinal));
        Assert.Empty(configurations);
    }

    // U-A2 -- ADR-258
    [Fact]
    public void U_A2_NoPortalQueryHandlerOrReaderTakesADbContext_ADispatcher_OrARepository()
    {
        var types = typeof(PortalSectionComposer).Assembly.GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("CatEcommerce.Read.QueryHandlers.Portal", StringComparison.Ordinal)
                        && !t.IsAbstract && !t.IsNested && !(t.Name.StartsWith('<')))
            .ToList();
        Assert.True(types.Count >= 8, "Expected the composer, handler and readers; found " + types.Count);

        var offenders = new List<string>();
        foreach (var type in types)
            foreach (var constructor in type.GetConstructors())
                foreach (var parameter in constructor.GetParameters())
                {
                    var parameterType = parameter.ParameterType;
                    var name = parameterType.Name;
                    var generic = parameterType.IsGenericType ? parameterType.GetGenericTypeDefinition().Name : name;
                    if (typeof(DbContext).IsAssignableFrom(parameterType)
                        || name.Contains("Dispatcher", StringComparison.Ordinal)
                        || generic.StartsWith("IAggregateRootRepository", StringComparison.Ordinal)
                        || name.Contains("Repository", StringComparison.Ordinal)
                        || name.Contains("DbContext", StringComparison.Ordinal))
                        offenders.Add($"{type.Name}({name} {parameter.Name})");
                }

        Assert.True(offenders.Count == 0, "Portal composers take upstream query HANDLERS only (ADR-258): " + string.Join(", ", offenders));
    }

    // U-A3 -- ADR-259: the portal's own shapes hold no other module's ViewModel
    [Fact]
    public void U_A3_NoPortalViewModelHoldsAnotherModulesViewModel_Recursively()
    {
        var portalViewModels = typeof(PortalSection<>).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(PortalSection<>).Namespace && t.Name.StartsWith("Portal", StringComparison.Ordinal))
            .ToList();
        Assert.True(portalViewModels.Count >= 10);

        var offenders = new List<string>();
        var seen = new HashSet<Type>();
        foreach (var type in portalViewModels) Visit(type, type.Name, offenders, seen);

        Assert.True(offenders.Count == 0, "A Portal ViewModel exposes a type it does not own: " + string.Join(", ", offenders));
    }

    private static void Visit(Type type, string path, List<string> offenders, HashSet<Type> seen)
    {
        if (!seen.Add(type)) return;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            foreach (var leaf in Leaves(property.PropertyType).Where(l => !l.IsGenericParameter))
            {
                var label = $"{path}.{property.Name}:{leaf.Name}";
                if (leaf.Namespace == typeof(PortalSection<>).Namespace)
                {
                    if (!leaf.Name.StartsWith("Portal", StringComparison.Ordinal)) offenders.Add(label);
                    else Visit(leaf, $"{path}.{property.Name}", offenders, seen);
                }
                else if (leaf.Namespace is not null && leaf.Namespace.StartsWith("CatEcommerce.", StringComparison.Ordinal)
                         && leaf.Namespace != "CatEcommerce.Shared.Common.Enums")
                {
                    offenders.Add(label);
                }
            }
    }

    private static IEnumerable<Type> Leaves(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                foreach (var leaf in Leaves(argument)) yield return leaf;
            if (type.Namespace == typeof(PortalSection<>).Namespace) yield return type.GetGenericTypeDefinition();
        }
        else if (type.IsArray) { foreach (var leaf in Leaves(type.GetElementType()!)) yield return leaf; }
        else yield return type;
    }

    // ADR-262 -- no clinical vocabulary anywhere in a Portal ViewModel (U-A5 is the exact allow-list for the visits and, later, prescriptions types)
    [Fact]
    public void U_A5_NoPortalViewModelPropertyIsNamedLikeClinicalContent()
    {
        string[] forbidden =
            ["Diagnosis", "Soap", "Dose", "Frequency", "Strength", "Instruction", "Allerg", "Condition", "Medication", "Medicine", "Handling", "Note", "Lab", "Treatment"];

        var offenders = typeof(PortalSection<>).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(PortalSection<>).Namespace && t.Name.StartsWith("Portal", StringComparison.Ordinal))
            .SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}"))
            .Where(label => forbidden.Any(word => label.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(offenders.Count == 0, "Clinical content on the dashboard (ADR-262): " + string.Join(", ", offenders));
    }

    // ADR-012 / BillingConventionTests.U_X6 -- the read side never rehydrates an aggregate; the portal read side touches no database at all
    [Fact]
    public void U_X6_ThePortalReadSide_TouchesNoDatabase_NeverLoadsAnAggregate_AndNeverFansOutInParallel()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "Read", "CatEcommerce.Read", "QueryHandlers", "Portal");
        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).ToList();
        Assert.True(files.Count >= 8);

        var wholeEntity = new Regex(@"AsNoTracking\(\)\s*\.\s*(FirstOrDefaultAsync|SingleOrDefaultAsync|SingleAsync|FirstAsync)\s*\(");
        string[] forbiddenText =
            ["CatEcommerceDbContext", "DbSet", ".ToDictionaryAsync", ".AsNoTracking", "Task.WhenAll", "Task.WhenAny", "Parallel.", "IInMemoryDispatcher", "IAggregateRootRepository"];

        var offenders = files
            .Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .Where(f => wholeEntity.IsMatch(f.Text) || forbiddenText.Any(f.Text.Contains))
            .Select(f => f.File).ToList();

        Assert.True(offenders.Count == 0, "These Portal read files touch a database, rehydrate an aggregate or fan out: " + string.Join(", ", offenders));
    }

    // ADR-260 -- the dashboard path has one catch site (plus the per-cat vaccination read the handover requires to degrade one cat)
    [Fact]
    public void ADR_260_OnlyTheComposerAndThePerCatVaccinationReadCatchExceptions()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "Read", "CatEcommerce.Read", "QueryHandlers", "Portal");
        var catchers = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"catch\s*\(\s*Exception")).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["CatsSectionReader.cs", "PortalSectionComposer.cs"], catchers);
    }

    [Fact]
    public void TheDashboardMessagesAreGuidanceOnly_NoPayLinkAnywhereInThePortalConstants()
    {
        var constants = typeof(CatEcommerce.Shared.Common.Constants.PortalMessages).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => f.Name);
        Assert.DoesNotContain(constants, n => n.Contains("Link", StringComparison.OrdinalIgnoreCase) || n.Contains("Url", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("cash or card, or by bank transfer", CatEcommerce.Shared.Common.Constants.PortalMessages.HowToPayOffline);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CatEcommerce.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("CatEcommerce.sln was not found above the test binaries.");
    }
}
