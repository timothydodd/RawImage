namespace RawImage.Decoders.XnbDecoder;

public class ImageData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Pixels { get; set; } = Array.Empty<byte>();
}
