using System.Reflection;

namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Base class for rich enumerations. Ships in NO Platform.Infrastructure.* package (verified,
/// HO-CAT-001 §7 F5) — this solution owns it. See ADR-022.
/// </summary>
public abstract class Enumeration(int value, string displayName) : IComparable
{
    public int Value { get; } = value;
    public string DisplayName { get; } = displayName;

    public override string ToString() => DisplayName;

    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .Cast<T>();

    public static T FromValue<T>(int value) where T : Enumeration =>
        GetAll<T>().FirstOrDefault(e => e.Value == value)
        ?? throw new InvalidOperationException(
            $"'{value}' is not a valid value for {typeof(T).Name}.");

    public static T FromDisplayName<T>(string displayName) where T : Enumeration =>
        GetAll<T>().FirstOrDefault(e => e.DisplayName == displayName)
        ?? throw new InvalidOperationException(
            $"'{displayName}' is not a valid display name for {typeof(T).Name}.");

    public override bool Equals(object? obj) =>
        obj is Enumeration other && other.GetType() == GetType() && other.Value == Value;

    public override int GetHashCode() => HashCode.Combine(GetType(), Value);

    public int CompareTo(object? obj) => Value.CompareTo((obj as Enumeration)?.Value);

    public static bool operator ==(Enumeration? left, Enumeration? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Enumeration? left, Enumeration? right) => !(left == right);
}
