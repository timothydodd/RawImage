namespace RawImage.Decoders.XnbDecoder;

public abstract class BasePixel
{
    private byte[] _data = Array.Empty<byte>();

    public byte[] Data
    {
        get => _data;
        set
        {
            if (value.Length == DataSize)
                _data = value;
        }
    }

    public abstract int DataSize { get; }

    protected BasePixel()
    {
        _data = new byte[DataSize];
    }

    public abstract BasePixel ToRgba(bool bigEndian);
}
