using QRCoder;

namespace TotpManager.Core;

public static class QrCodeGenerator
{
    /// <summary>
    /// Generates a QR code PNG for the given URI and returns the raw PNG bytes.
    /// </summary>
    /// <param name="uri">The otpauth:// URI to encode.</param>
    /// <param name="pixelsPerModule">Size of each QR module in pixels (default 10).</param>
    public static byte[] GenerateQrPng(string uri, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
