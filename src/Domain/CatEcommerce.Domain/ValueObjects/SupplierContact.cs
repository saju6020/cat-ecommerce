namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-IMP-001 §8.5 -- a supplier's contact block. <c>Phone</c> is free text: international numbers are not
/// HO-ACC-001 <c>MobileNumber</c>s. <c>Email</c> is normalised through <c>EmailAddress</c> by the aggregate.</summary>
public sealed record SupplierContact(string? ContactName, string? Phone, string? Email, string? Address);
