using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.3 -- pure, no I/O. GSM-7 is 160 characters per single segment, 153 per
/// segment once concatenated; UCS-2 (any character outside the GSM 03.38 default alphabet) is
/// 70/67. The renderer never truncates -- this is measurement only, shown on the preview.</summary>
public static class SmsSegments
{
    private const string Gsm7Basic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

    private static readonly HashSet<char> Gsm7Chars = [.. Gsm7Basic];

    public static (SmsEncoding Encoding, int Characters, int Segments) Measure(string text)
    {
        var isGsm7 = text.All(Gsm7Chars.Contains);
        var encoding = isGsm7 ? SmsEncoding.Gsm7 : SmsEncoding.Ucs2;
        var length = text.Length;

        var singleSegmentLimit = isGsm7 ? 160 : 70;
        var multiSegmentLimit = isGsm7 ? 153 : 67;

        var segments = length switch
        {
            0 => 0,
            _ when length <= singleSegmentLimit => 1,
            _ => (int)Math.Ceiling(length / (double)multiSegmentLimit)
        };

        return (encoding, length, segments);
    }
}
