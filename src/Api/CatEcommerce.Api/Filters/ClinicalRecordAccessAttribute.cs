using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-EMR-001 §8.11 / ADR-129 -- marks an action that returns clinical data. Resolved per action (never
/// a global filter): <see cref="ClinicalRecordAccessFilter"/> writes one <c>ClinicalRecordAccess</c> audit
/// row before the action runs and fails closed (NFR-SEC-8).
///
/// An attribute argument must be a compile-time constant and <see cref="ClinicalRecordKind"/> is an
/// <see cref="Enumeration"/> class, so the argument is the member's name:
/// <c>[ClinicalRecordAccess(nameof(ClinicalRecordKind.Visit))]</c>. An unknown name throws when the
/// attribute is constructed (the first request, or the U-X5 reflection test), never silently logs nothing.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ClinicalRecordAccessAttribute : TypeFilterAttribute
{
    public ClinicalRecordAccessAttribute(string kind) : base(typeof(ClinicalRecordAccessFilter))
    {
        if (!EnumerationParsing.TryParse<ClinicalRecordKind>(kind, out var parsed) || parsed is null)
            throw new ArgumentException($"'{kind}' is not a ClinicalRecordKind.", nameof(kind));

        Kind = parsed;
        Arguments = [parsed];
    }

    public ClinicalRecordKind Kind { get; }
}
