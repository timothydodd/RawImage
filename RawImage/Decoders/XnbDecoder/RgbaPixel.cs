namespace RawImage.Decoders.XnbDecoder;

public class RgbaPixel : BasePixel
{
    public override int DataSize => 4;

    public RgbaPixel(byte[] pixelData)
    {
        Data = pixelData;
    }

    public override BasePixel ToRgba(bool bigEndian)
    {
        if (bigEndian)
        {
            byte tmp = Data[0];
            Data[0] = Data[3];
            Data[3] = tmp;
            tmp = Data[1];
            Data[1] = Data[2];
            Data[2] = tmp;
        }
        else
        {
            byte tmp = Data[0];
            Data[0] = Data[2];
            Data[2] = tmp;
        }

        return this;
    }
}
