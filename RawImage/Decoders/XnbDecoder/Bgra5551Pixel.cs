namespace RawImage.Decoders.XnbDecoder;

public class Bgra5551Pixel : BasePixel
{
    public override int DataSize => 2;

    public Bgra5551Pixel(byte[] pixelData)
    {
        Data = pixelData;
    }

    public override BasePixel ToRgba(bool bigEndian)
    {
        byte upperByte = bigEndian ? Data[0] : Data[1];
        byte lowerByte = bigEndian ? Data[1] : Data[0];

        byte[] newData = new byte[4];
        newData[0] = (byte)((lowerByte & 0x1f) << 3);
        newData[1] = (byte)((upperByte & 0x03) << 6 | (lowerByte & 0xe0) >> 2);
        newData[2] = (byte)((upperByte & 0x7c) << 1);
        newData[3] = (byte)((upperByte & 0x80) != 0x00 ? 0xff : 0x00);

        return new RgbaPixel(newData);
    }
}
