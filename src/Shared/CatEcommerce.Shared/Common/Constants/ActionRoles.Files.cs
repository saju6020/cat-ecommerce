namespace CatEcommerce.Shared.Common.Constants;

/// <summary>File Storage's own row in the solution-wide action-to-role table (ADR-036/ADR-341).
/// Every staff role may reach the upload endpoint at all; <see cref="FilePurposeAccess"/> is the
/// finer-grained, per-purpose gate applied inside the controller (see <c>Actions.Files.cs</c>).</summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] FilesRows =
    [
        (Actions.Files.Upload, StaffRoles.All.ToArray()),
    ];
}
