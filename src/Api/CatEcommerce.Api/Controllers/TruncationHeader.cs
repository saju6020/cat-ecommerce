using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Http;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// Gate fix 2026-09-26. The six lists <see cref="AdministrationListCaps.MaxUnboundedListSize"/>
/// hard-caps (rather than fully paginating) signal a possible truncation to the frontend with a
/// response header, not a body-shape change -- adding a field to the JSON array these endpoints
/// return would not be "byte-identical" for existing consumers. A returned count exactly equal to
/// the cap sets <c>X-Truncated: true</c>; this is a cheap heuristic (one extra comparison, no extra
/// query) that can rarely false-positive when the real total happens to equal the cap exactly, but
/// never false-negatives.
/// </summary>
internal static class TruncationHeader
{
    public const string HeaderName = "X-Truncated";

    public static void ApplyIfAtCap(HttpResponse response, int itemCount)
    {
        if (itemCount == AdministrationListCaps.MaxUnboundedListSize)
            response.Headers[HeaderName] = "true";
    }
}
