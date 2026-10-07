namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// The action-name catalogue behind <c>[RequireAction(Actions.X)]</c> (HO-ACC-001 §12.8, ADR-036).
/// Each action is a nested static class of <c>const string</c> members, e.g.
/// <c>Actions.Cats.View = "Cats.View"</c> (HO-ADM-001 §12.3's pattern).
///
/// <c>static partial</c> so each functional area (ADR-341) adds its own nested class in its own
/// <c>Actions.[Area].cs</c> file instead of editing this one. This is the empty shell only (Step 1
/// of the contracts pass, docs/handover/README.md). HO-ACC-001 builds the filter that reads these
/// action names (<c>RequireActionAttribute</c> / <c>RequireActionFilter</c>) in wave A; every other
/// area only ever appends its own nested class here, never edits another area's.
/// </summary>
public static partial class Actions
{
}
