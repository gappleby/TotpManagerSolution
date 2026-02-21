using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ZXing;
using ZXing.Common;

namespace TotpManager.Core;

public static class QrCodeDecoder
{
    /// <summary>
    /// Reads a QR code from an image file and returns the decoded text.
    /// Tries multiple scales to handle high-DPI phone screenshots and small images.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static string DecodeQrFromFile(string imagePath)
    {
        using var original = new Bitmap(imagePath);

        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder   = true,
                TryInverted = true,
            }
        };

        // Try two binarizer modes:
        //   PureBarcode=false → HybridBinarizer  (better for natural-scene photos / phone screenshots)
        //   PureBarcode=true  → GlobalHistogramBinarizer (better for clean digital QR images)
        // For each mode, try the raw original then canvas-rendered copies at several scales.
        foreach (var pure in new[] { false, true })
        {
            reader.Options.PureBarcode = pure;

            // Raw original (no conversion — maximum fidelity).
            var t0 = TryRead(reader, original);
            if (t0 is not null) return t0;

            // Canvas renders: normalise pixel format, composite onto white, vary scale.
            // Scale 1:1 uses NearestNeighbor; others use high-quality bicubic.
            foreach (var scale in new[] { 1.0, 0.5, 0.75, 1.5, 2.0 })
            {
                var w = Math.Max(1, (int)(original.Width  * scale));
                var h = Math.Max(1, (int)(original.Height * scale));

                using var canvas = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(canvas))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = scale == 1.0
                        ? InterpolationMode.NearestNeighbor
                        : InterpolationMode.HighQualityBicubic;
                    g.DrawImage(original,
                        new Rectangle(0, 0, w, h),
                        new Rectangle(0, 0, original.Width, original.Height),
                        GraphicsUnit.Pixel);
                }

                var t = TryRead(reader, canvas);
                if (t is not null) return t;
            }
        }

        throw new InvalidOperationException($"No QR code found in '{imagePath}'.");
    }

    [SupportedOSPlatform("windows")]
    private static string? TryRead(BarcodeReaderGeneric reader, Bitmap bmp)
    {
        var rect    = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] pixelBytes;
        try
        {
            // Copy row-by-row, stripping any stride padding, so ZXing receives a
            // tightly-packed BGRA32 buffer (stride may exceed width * 4 on some bitmaps).
            var rowBytes = bmp.Width * 4;
            pixelBytes   = new byte[rowBytes * bmp.Height];
            for (int y = 0; y < bmp.Height; y++)
                Marshal.Copy(IntPtr.Add(bmpData.Scan0, y * bmpData.Stride),
                             pixelBytes, y * rowBytes, rowBytes);
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        return reader.Decode(pixelBytes, bmp.Width, bmp.Height,
                             RGBLuminanceSource.BitmapFormat.BGRA32)?.Text;
    }
}
