namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-NTF-001 §12.4 -- masks a destination for a list view (full value only in the staff detail
/// view). A mobile keeps its first 6 and last 3 characters (<c>+88017•••••678</c>); an email keeps
/// only its first local-part character, always followed by a <b>fixed</b> three-bullet run (never
/// proportional to the local part's real length -- a one-letter local part is masked exactly the
/// same as a ten-letter one, so the mask itself never discloses the length).
/// </summary>
public static class DestinationMask
{
    private const char Bullet = '•';

    public static string Mask(string destination)
    {
        if (string.IsNullOrEmpty(destination)) return destination;

        var atIndex = destination.IndexOf('@');
        if (atIndex > 0)
        {
            var domain = destination[atIndex..];
            return $"{destination[0]}{new string(Bullet, 3)}{domain}";
        }

        if (destination.Length <= 9) return new string(Bullet, destination.Length);

        var prefix = destination[..6];
        var suffix = destination[^3..];
        var middleLength = destination.Length - 9;
        return $"{prefix}{new string(Bullet, middleLength)}{suffix}";
    }
}
