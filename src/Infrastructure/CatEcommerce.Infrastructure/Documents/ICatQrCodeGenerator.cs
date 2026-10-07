namespace CatEcommerce.Infrastructure.Documents;

/// <summary>
/// US-CAT-11 -- printable cage card QR resolving to the cat's profile. Behind an interface so
/// the Api layer never references QRCoder directly (HO-CAT-001 section 12.2).
/// </summary>
public interface ICatQrCodeGenerator
{
    byte[] Generate(string profileUrl);
}
