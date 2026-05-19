namespace RawImage.Decoders.PsdDecoder;

public class HeaderInfo
{
    public short Channels { get; set; } = -1;
    public int Height { get; set; } = -1;
    public int Width { get; set; } = -1;
    public short BitsPerPixel { get; set; } = -1;
    public short ColourMode { get; set; } = -1;
}

public class ColorModeData
{
    public int Length { get; set; } = -1;
    public byte[]? ColourData { get; set; }
}

public class ResolutionInfo
{
    public short HRes { get; set; } = -1;
    public int HResUnit { get; set; } = -1;
    public short WidthUnit { get; set; } = -1;
    public short VRes { get; set; } = -1;
    public int VResUnit { get; set; } = -1;
    public short HeightUnit { get; set; } = -1;
}

public class DisplayInfo
{
    public short ColourSpace { get; set; } = -1;
    public short[] Colour { get; set; } = new short[4];
    public short Opacity { get; set; } = -1;
    public bool Kind { get; set; }
    public byte Padding { get; set; }
}

public class ImageResource
{
    public int Length { get; set; } = -1;
    public byte[] OSType { get; set; } = new byte[4];
    public short Id { get; set; } = -1;
    public byte[]? Name { get; set; }
    public int Size { get; set; } = -1;

    public void Reset()
    {
        Length = -1;
        for (int i = 0; i < 4; i++) OSType[i] = 0x00;
        Id = -1;
        Size = -1;
    }
}
