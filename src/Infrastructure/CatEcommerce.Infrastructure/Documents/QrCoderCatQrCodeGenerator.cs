using QRCoder;

namespace CatEcommerce.Infrastructure.Documents;

/// <summary>
/// Uses QRCoder's <see cref="PngByteQRCode"/> renderer, which is pure managed code with no
/// System.Drawing dependency -- keeps this cross-platform even though QRCoder's package brings
/// in System.Drawing.Common transitively for its other renderers, which we do not use.
/// </summary>
public sealed class QrCoderCatQrCodeGenerator : ICatQrCodeGenerator
{
    public byte[] Generate(string profileUrl)
    {
        using var generator = new QRCodeGenerator();
        using var qrCodeData = generator.CreateQrCode(profileUrl, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        return pngQrCode.GetGraphic(20);
    }
}
