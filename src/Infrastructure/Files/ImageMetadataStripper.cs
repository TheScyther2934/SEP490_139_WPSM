namespace WPSM.Infrastructure.Files;

/// <summary>Removes EXIF/GPS and text metadata without re-encoding the image (SDS security design).</summary>
internal static class ImageMetadataStripper
{
    private const byte JpegApp1 = 0xE1, JpegApp13 = 0xED, JpegComment = 0xFE, JpegStartOfScan = 0xDA;
    private static readonly string[] PngMetadataChunks = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];

    internal static byte[] StripJpeg(byte[] source)
    {
        using var output = new MemoryStream(source.Length);
        output.Write(source, 0, 2);
        var position = 2;
        while (true)
        {
            if (position + 4 > source.Length || source[position] != 0xFF) throw Invalid();
            var marker = source[position + 1];
            if (marker == JpegStartOfScan)
            {
                output.Write(source, position, source.Length - position);
                return output.ToArray();
            }
            var length = (source[position + 2] << 8) | source[position + 3];
            if (length < 2 || position + 2 + length > source.Length) throw Invalid();
            if (marker is not (JpegApp1 or JpegApp13 or JpegComment)) output.Write(source, position, 2 + length);
            position += 2 + length;
        }
    }

    internal static byte[] StripPng(byte[] source)
    {
        const int signatureLength = 8, chunkOverhead = 12;
        using var output = new MemoryStream(source.Length);
        output.Write(source, 0, signatureLength);
        var position = signatureLength;
        while (true)
        {
            if (position + chunkOverhead > source.Length) throw Invalid();
            var dataLength = (long)((uint)source[position] << 24 | (uint)source[position + 1] << 16 | (uint)source[position + 2] << 8 | source[position + 3]);
            var type = System.Text.Encoding.ASCII.GetString(source, position + 4, 4);
            if (position + chunkOverhead + dataLength > source.Length) throw Invalid();
            var total = chunkOverhead + (int)dataLength;
            if (!PngMetadataChunks.Contains(type)) output.Write(source, position, total);
            position += total;
            if (type == "IEND") return output.ToArray();
        }
    }

    private static InvalidOperationException Invalid() => new("Tệp ảnh bị lỗi hoặc không đúng định dạng (MSG17).");
}
