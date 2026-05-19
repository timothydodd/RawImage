using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.Text;

namespace RawImage.Decoders.PsdDecoder;

/// <summary>
/// PSD file decoder - refactored to use managed bitmap operations instead of Win32 GDI.
/// </summary>
public class PsdDecoder
{
    private HeaderInfo _headerInfo = new();
    private ColorModeData _colorModeData = new();
    private ImageResource _imageResource = new();
    private ResolutionInfo _resolutionInfo = new();
    private DisplayInfo _displayInfo = new();

    private bool _resolutionInfoFilled;
    private bool _thumbnailFilled;
    private bool _copyright;
    private short _colourCount = -1;
    private short _transparentIndex = -1;
    private int _globalAngle = 30;
    private int _compression = -1;

    // Image pixel data (BGRA format)
    private byte[]? _pixelData;

    public int Width => _headerInfo.Width;
    public int Height => _headerInfo.Height;
    public int BitsPerPixel => _headerInfo.BitsPerPixel;
    public int Compression => _compression;
    public bool IsCopyrighted => _copyright;
    public bool HasThumbnail => _thumbnailFilled;

    public int Load(string path)
    {
        int errorCode = 0;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            if (!ReadHeader(stream))
                return -2; // Error in header

            if (!ReadColourModeData(stream))
                return -3; // Error in ColourMode Data

            if (!ReadImageResource(stream))
                return -4; // Error in Image Resource

            if (!ReadLayerAndMaskInfoSection(stream))
                return -5; // Error in Mask Info

            errorCode = ReadImageData(stream);
        }
        catch
        {
            errorCode = -1;
        }

        return errorCode;
    }

    public Bitmap? GetBitmap()
    {
        if (_pixelData == null || _headerInfo.Width <= 0 || _headerInfo.Height <= 0)
            return null;

        var bitmap = new WriteableBitmap(
            new Avalonia.PixelSize(_headerInfo.Width, _headerInfo.Height),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Unpremul);

        using var fb = bitmap.Lock();
        unsafe
        {
            fixed (byte* src = _pixelData)
            {
                Buffer.MemoryCopy(src, (void*)fb.Address, _pixelData.Length, _pixelData.Length);
            }
        }

        return bitmap;
    }

    private bool ReadHeader(FileStream stream)
    {
        var reader = new BinaryReader(stream);

        reader.BaseStream.Position = 0;
        byte[] signature = reader.ReadBytes(4);
        byte[] version = reader.ReadBytes(2);
        byte[] reserved = reader.ReadBytes(6);
        byte[] channels = reader.ReadBytes(2);
        byte[] rows = reader.ReadBytes(4);
        byte[] columns = reader.ReadBytes(4);
        byte[] depth = reader.ReadBytes(2);
        byte[] mode = reader.ReadBytes(2);

        if (Encoding.ASCII.GetString(signature) != "8BPS" || version[1] != 0x01)
            return false;

        _headerInfo = new HeaderInfo();

        if (SwapBytes(channels, 2)) _headerInfo.Channels = BitConverter.ToInt16(channels, 0);
        if (SwapBytes(rows, 4)) _headerInfo.Height = BitConverter.ToInt32(rows, 0);
        if (SwapBytes(columns, 4)) _headerInfo.Width = BitConverter.ToInt32(columns, 0);
        if (SwapBytes(depth, 2)) _headerInfo.BitsPerPixel = BitConverter.ToInt16(depth, 0);
        if (SwapBytes(mode, 2)) _headerInfo.ColourMode = BitConverter.ToInt16(mode, 0);

        return _headerInfo.Channels != -1 && _headerInfo.Height != -1 &&
               _headerInfo.Width != -1 && _headerInfo.BitsPerPixel != -1 &&
               _headerInfo.ColourMode != -1;
    }

    private bool ReadColourModeData(FileStream stream)
    {
        var reader = new BinaryReader(stream);

        reader.BaseStream.Position = 26;
        byte[] colorMode = reader.ReadBytes(4);

        _colorModeData = new ColorModeData();
        if (SwapBytes(colorMode, 4))
        {
            _colorModeData.Length = BitConverter.ToInt32(colorMode, 0);
        }

        if (_colorModeData.Length > 0)
        {
            _colorModeData.ColourData = reader.ReadBytes(_colorModeData.Length);
        }

        return true;
    }

    private bool ReadImageResource(FileStream stream)
    {
        var reader = new BinaryReader(stream);

        byte[] lengthBytes = reader.ReadBytes(4);
        _imageResource = new ImageResource();
        _resolutionInfo = new ResolutionInfo();
        _displayInfo = new DisplayInfo();

        if (SwapBytes(lengthBytes, 4))
        {
            _imageResource.Length = BitConverter.ToInt32(lengthBytes, 0);
        }

        int bytesRead = 0;
        int totalBytes = _imageResource.Length;

        while (stream.Position < stream.Length && bytesRead < totalBytes)
        {
            _imageResource.Reset();
            _imageResource.OSType = reader.ReadBytes(4);
            bytesRead += 4;

            if (Encoding.ASCII.GetString(_imageResource.OSType) != "8BIM")
                continue;

            byte[] id = reader.ReadBytes(2);
            bytesRead += 2;

            if (SwapBytes(id, 2))
                _imageResource.Id = BitConverter.ToInt16(id, 0);

            byte sizeOfName = reader.ReadByte();
            bytesRead += 1;

            int nameSize = sizeOfName;
            if (nameSize > 0)
            {
                if ((nameSize % 2) != 0)
                {
                    reader.ReadByte();
                    bytesRead += 1;
                }
                _imageResource.Name = reader.ReadBytes(nameSize);
                bytesRead += nameSize;
            }

            reader.ReadByte();
            bytesRead += 1;

            byte[] size = reader.ReadBytes(4);
            if (SwapBytes(size, 4))
                _imageResource.Size = BitConverter.ToInt32(size, 0);
            bytesRead += 4;

            if ((_imageResource.Size % 2) != 0)
                _imageResource.Size++;

            if (_imageResource.Size > 0)
            {
                switch (_imageResource.Id)
                {
                    case 1005: // Resolution info
                        _resolutionInfoFilled = true;
                        ReadResolutionInfo(reader, ref bytesRead);
                        break;

                    case 1007: // Display info
                        ReadDisplayInfo(reader, ref bytesRead);
                        break;

                    case 1034: // Copyright
                        ReadCopyright(reader, ref bytesRead);
                        break;

                    case 1033:
                    case 1036: // Thumbnail
                        _thumbnailFilled = true;
                        SkipThumbnail(reader, ref bytesRead);
                        break;

                    case 1037: // Global angle
                        byte[] angleBytes = reader.ReadBytes(4);
                        if (SwapBytes(angleBytes, 4))
                            _globalAngle = BitConverter.ToInt32(angleBytes, 0);
                        bytesRead += 4;
                        break;

                    case 1046: // Colour count
                        byte[] countBytes = reader.ReadBytes(2);
                        if (SwapBytes(countBytes, 2))
                            _colourCount = BitConverter.ToInt16(countBytes, 0);
                        bytesRead += 2;
                        break;

                    case 1047: // Transparent index
                        byte[] indexBytes = reader.ReadBytes(2);
                        if (SwapBytes(indexBytes, 2))
                            _transparentIndex = BitConverter.ToInt16(indexBytes, 0);
                        bytesRead += 2;
                        break;

                    default:
                        for (int n = 0; n < _imageResource.Size; n++)
                        {
                            reader.ReadByte();
                            bytesRead++;
                        }
                        break;
                }
            }
        }

        return true;
    }

    private void ReadResolutionInfo(BinaryReader reader, ref int bytesRead)
    {
        byte[] shortValue = reader.ReadBytes(2);
        if (SwapBytes(shortValue, 2))
            _resolutionInfo.HRes = BitConverter.ToInt16(shortValue, 0);
        bytesRead += 2;

        byte[] intValue = reader.ReadBytes(4);
        if (SwapBytes(intValue, 4))
            _resolutionInfo.HResUnit = BitConverter.ToInt32(intValue, 0);
        bytesRead += 4;

        shortValue = reader.ReadBytes(2);
        if (SwapBytes(shortValue, 2))
            _resolutionInfo.WidthUnit = BitConverter.ToInt16(shortValue, 0);
        bytesRead += 2;

        shortValue = reader.ReadBytes(2);
        if (SwapBytes(shortValue, 2))
            _resolutionInfo.VRes = BitConverter.ToInt16(shortValue, 0);
        bytesRead += 2;

        intValue = reader.ReadBytes(4);
        if (SwapBytes(intValue, 4))
            _resolutionInfo.VResUnit = BitConverter.ToInt32(intValue, 0);
        bytesRead += 4;

        shortValue = reader.ReadBytes(2);
        if (SwapBytes(shortValue, 2))
            _resolutionInfo.HeightUnit = BitConverter.ToInt16(shortValue, 0);
        bytesRead += 2;
    }

    private void ReadDisplayInfo(BinaryReader reader, ref int bytesRead)
    {
        byte[] shortValue = reader.ReadBytes(2);
        if (SwapBytes(shortValue, 2))
            _displayInfo.ColourSpace = BitConverter.ToInt16(shortValue, 0);
        bytesRead += 2;

        for (int n = 0; n < 4; n++)
        {
            shortValue = reader.ReadBytes(2);
            if (SwapBytes(shortValue, 2))
                _displayInfo.Colour[n] = BitConverter.ToInt16(shortValue, 0);
            bytesRead += 2;
        }

        shortValue = reader.ReadBytes(2);
        if (SwapBytes(shortValue, 2))
            _displayInfo.Opacity = BitConverter.ToInt16(shortValue, 0);
        if (_displayInfo.Opacity < 0 || _displayInfo.Opacity > 100)
            _displayInfo.Opacity = 100;
        bytesRead += 2;

        byte c = reader.ReadByte();
        _displayInfo.Kind = c != 0x00;
        bytesRead++;

        _displayInfo.Padding = reader.ReadByte();
        bytesRead++;
    }

    private void ReadCopyright(BinaryReader reader, ref int bytesRead)
    {
        byte[] shortValue = reader.ReadBytes(2);
        if (SwapBytes(shortValue, 2))
        {
            _copyright = BitConverter.ToInt16(shortValue, 0) > 0;
        }
        bytesRead += 2;
    }

    private void SkipThumbnail(BinaryReader reader, ref int bytesRead)
    {
        // Skip thumbnail data
        int toSkip = _imageResource.Size;
        for (int i = 0; i < toSkip; i++)
        {
            reader.ReadByte();
            bytesRead++;
        }
    }

    private bool ReadLayerAndMaskInfoSection(FileStream stream)
    {
        var reader = new BinaryReader(stream);

        byte[] dataLength = reader.ReadBytes(4);
        int totalBytes = 0;

        if (SwapBytes(dataLength, 4))
            totalBytes = BitConverter.ToInt32(dataLength, 0);

        if (stream.Position + totalBytes < stream.Length)
            stream.Position += totalBytes;

        return true;
    }

    private int ReadImageData(FileStream stream)
    {
        var reader = new BinaryReader(stream);

        byte[] shortValue = reader.ReadBytes(2);
        short compression = 0;

        if (SwapBytes(shortValue, 2))
            compression = BitConverter.ToInt16(shortValue, 0);

        _compression = compression;

        int width = _headerInfo.Width;
        int height = _headerInfo.Height;
        int bytesPerPixelPerChannel = _headerInfo.BitsPerPixel / 8;
        int pixels = width * height;
        int totalBytes = pixels * bytesPerPixelPerChannel * _headerInfo.Channels;

        byte[] pData;

        switch (compression)
        {
            case 0: // Raw data
                pData = ReadRawImageData(reader, width, height, bytesPerPixelPerChannel, pixels, totalBytes);
                break;

            case 1: // RLE compression
                pData = ReadRleImageData(reader, stream, width, height, bytesPerPixelPerChannel, pixels, totalBytes);
                break;

            case 2:
                return -10; // ZIP without prediction not supported

            case 3:
                return -11; // ZIP with prediction not supported

            default:
                return -12; // Unknown format
        }

        if (pData == null)
            return -6;

        // Allocate BGRA pixel buffer
        _pixelData = new byte[width * height * 4];

        // Process buffer based on color mode
        ProcessBuffer(pData, width, height, bytesPerPixelPerChannel, totalBytes);

        return 0;
    }

    private byte[] ReadRawImageData(BinaryReader reader, int width, int height, int bytesPerPixelPerChannel, int pixels, int totalBytes)
    {
        byte[] pData = new byte[totalBytes];
        int bytesRead = 0;

        switch (_headerInfo.ColourMode)
        {
            case 1: // Grayscale
            case 2: // Indexed
            case 8: // Duotone
                while (bytesRead < totalBytes)
                {
                    byte[] imageData = reader.ReadBytes(bytesPerPixelPerChannel);
                    for (int j = 0; j < bytesPerPixelPerChannel; j++)
                        pData[bytesRead + j] = imageData[j];
                    bytesRead += bytesPerPixelPerChannel;
                }
                break;

            case 3: // RGB
                int bytesToRead = bytesPerPixelPerChannel == 2 ? 1 : bytesPerPixelPerChannel;
                if (bytesPerPixelPerChannel == 2)
                    totalBytes = pixels * bytesToRead * _headerInfo.Channels;

                pData = new byte[totalBytes];
                int pixelCounter = 0;

                for (int colour = 0; colour < 3; colour++)
                {
                    pixelCounter = colour;
                    for (int pos = 0; pos < pixels; pos++)
                    {
                        if (bytesRead < totalBytes)
                        {
                            byte[] imageData = reader.ReadBytes(bytesToRead);
                            for (int j = 0; j < bytesToRead; j++)
                                pData[pixelCounter + j] = imageData[j];
                            bytesRead += bytesToRead;
                            pixelCounter += 3;
                            if (bytesPerPixelPerChannel == 2)
                                reader.ReadBytes(bytesToRead);
                        }
                    }
                }
                break;

            case 4: // CMYK
                pixelCounter = 0;
                for (int colour = 0; colour < 4; colour++)
                {
                    pixelCounter = colour * bytesPerPixelPerChannel;
                    for (int pos = 0; pos < pixels; pos++)
                    {
                        if (bytesRead < totalBytes)
                        {
                            byte[] imageData = reader.ReadBytes(bytesPerPixelPerChannel);
                            for (int j = 0; j < bytesPerPixelPerChannel; j++)
                                pData[pixelCounter + j] = imageData[j];
                            bytesRead += bytesPerPixelPerChannel;
                            pixelCounter += 4 * bytesPerPixelPerChannel;
                        }
                    }
                }
                break;

            case 9: // Lab
                pixelCounter = 0;
                for (int colour = 0; colour < 3; colour++)
                {
                    pixelCounter = colour * bytesPerPixelPerChannel;
                    for (int pos = 0; pos < pixels; pos++)
                    {
                        if (bytesRead < totalBytes)
                        {
                            byte[] imageData = reader.ReadBytes(bytesPerPixelPerChannel);
                            for (int j = 0; j < bytesPerPixelPerChannel; j++)
                                pData[pixelCounter + j] = imageData[j];
                            bytesRead += bytesPerPixelPerChannel;
                            pixelCounter += 3 * bytesPerPixelPerChannel;
                        }
                    }
                }
                break;
        }

        return pData;
    }

    private byte[] ReadRleImageData(BinaryReader reader, FileStream stream, int width, int height, int bytesPerPixelPerChannel, int pixels, int totalBytes)
    {
        byte[] pDest = new byte[totalBytes];
        byte[] pData = new byte[totalBytes];

        // Skip row byte counts
        stream.Position += height * _headerInfo.Channels * 2;

        int pointer = 0;

        for (int channel = 0; channel < _headerInfo.Channels; channel++)
        {
            int count = 0;
            while (count < pixels)
            {
                byte byteValue = reader.ReadByte();
                int len = byteValue;

                if (len < 128)
                {
                    len++;
                    count += len;
                    while (len != 0)
                    {
                        pData[pointer++] = reader.ReadByte();
                        len--;
                    }
                }
                else if (len > 128)
                {
                    len ^= 0xFF;
                    len += 2;
                    byteValue = reader.ReadByte();
                    count += len;
                    while (len != 0)
                    {
                        pData[pointer++] = byteValue;
                        len--;
                    }
                }
            }
        }

        // Rearrange interleaved data
        int pixelCounter = 0;
        pointer = 0;

        for (int colour = 0; colour < _headerInfo.Channels; colour++)
        {
            pixelCounter = colour * bytesPerPixelPerChannel;
            for (int pos = 0; pos < pixels; pos++)
            {
                for (int j = 0; j < bytesPerPixelPerChannel; j++)
                    pDest[pixelCounter + j] = pData[pointer + j];
                pointer++;
                pixelCounter += _headerInfo.Channels * bytesPerPixelPerChannel;
            }
        }

        return pDest;
    }

    private void ProcessBuffer(byte[] pData, int width, int height, int bytesPerPixelPerChannel, int totalBytes)
    {
        switch (_headerInfo.ColourMode)
        {
            case 1: // Grayscale
            case 8: // Duotone
                ProcessGrayscale(pData, width, height, bytesPerPixelPerChannel);
                break;

            case 2: // Indexed
                ProcessIndexed(pData, width, height, totalBytes);
                break;

            case 3: // RGB
                ProcessRgb(pData, width, height, bytesPerPixelPerChannel, totalBytes);
                break;

            case 4: // CMYK
                ProcessCmyk(pData, width, height, bytesPerPixelPerChannel, totalBytes);
                break;

            case 7: // Multichannel
                ProcessMultichannel(pData, width, height, bytesPerPixelPerChannel, totalBytes);
                break;

            case 9: // Lab
                ProcessLab(pData, width, height, bytesPerPixelPerChannel, totalBytes);
                break;
        }
    }

    private void ProcessGrayscale(byte[] pData, int width, int height, int bytesPerPixelPerChannel)
    {
        int counter = 0;
        byte[] colorValue = new byte[8];

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                for (int i = 0; i < bytesPerPixelPerChannel; i++)
                    colorValue[i] = pData[counter + i];

                SwapBytes(colorValue, bytesPerPixelPerChannel);
                int value = BitConverter.ToInt32(colorValue, 0);

                if (_headerInfo.BitsPerPixel == 16)
                    value /= 256;

                value = Math.Clamp(value, 0, 255);

                SetPixel(col, row, (byte)value, (byte)value, (byte)value, 255);
                counter += bytesPerPixelPerChannel;
            }
        }
    }

    private void ProcessIndexed(byte[] pData, int width, int height, int totalBytes)
    {
        if (_colorModeData.Length != 768 || _colourCount <= 0 || _colorModeData.ColourData == null)
            return;

        int row = 0, col = 0;

        for (int counter = 0; counter < totalBytes; counter++)
        {
            int index = pData[counter];
            int red = _colorModeData.ColourData[index];
            int green = _colorModeData.ColourData[index + 256];
            int blue = _colorModeData.ColourData[index + 512];

            SetPixel(col, row, (byte)red, (byte)green, (byte)blue, 255);

            col++;
            if (col >= width)
            {
                col = 0;
                row++;
            }
        }
    }

    private void ProcessRgb(byte[] pData, int width, int height, int bytesPerPixelPerChannel, int totalBytes)
    {
        int bytesToRead = bytesPerPixelPerChannel == 2 ? 1 : bytesPerPixelPerChannel;
        int row = 0, col = 0;
        byte[] colorValue = new byte[8];

        for (int counter = 0; counter < totalBytes; counter += _headerInfo.Channels * bytesToRead)
        {
            Array.Copy(pData, counter, colorValue, 0, bytesToRead);
            SwapBytes(colorValue, bytesToRead);
            int red = BitConverter.ToInt32(colorValue, 0);

            Array.Copy(pData, counter + bytesToRead, colorValue, 0, bytesToRead);
            SwapBytes(colorValue, bytesToRead);
            int green = BitConverter.ToInt32(colorValue, 0);

            Array.Copy(pData, counter + 2 * bytesToRead, colorValue, 0, bytesToRead);
            SwapBytes(colorValue, bytesToRead);
            int blue = BitConverter.ToInt32(colorValue, 0);

            SetPixel(col, row, (byte)Math.Clamp(red, 0, 255), (byte)Math.Clamp(green, 0, 255), (byte)Math.Clamp(blue, 0, 255), 255);

            col++;
            if (col >= width)
            {
                col = 0;
                row++;
            }
        }
    }

    private void ProcessCmyk(byte[] pData, int width, int height, int bytesPerPixelPerChannel, int totalBytes)
    {
        int row = 0, col = 0;
        byte[] colorValue = new byte[8];
        double maxColours = Math.Pow(2, _headerInfo.BitsPerPixel);

        for (int counter = 0; counter < totalBytes; counter += 4 * bytesPerPixelPerChannel)
        {
            Array.Copy(pData, counter, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exC = BitConverter.ToUInt32(colorValue, 0);

            Array.Copy(pData, counter + bytesPerPixelPerChannel, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exM = BitConverter.ToUInt32(colorValue, 0);

            Array.Copy(pData, counter + 2 * bytesPerPixelPerChannel, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exY = BitConverter.ToUInt32(colorValue, 0);

            Array.Copy(pData, counter + 3 * bytesPerPixelPerChannel, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exK = BitConverter.ToUInt32(colorValue, 0);

            double c = 1.0 - exC / maxColours;
            double m = 1.0 - exM / maxColours;
            double y = 1.0 - exY / maxColours;
            double k = 1.0 - exK / maxColours;

            var (r, g, b) = CmykToRgb(c, m, y, k);
            SetPixel(col, row, r, g, b, 255);

            col++;
            if (col >= width)
            {
                col = 0;
                row++;
            }
        }
    }

    private void ProcessMultichannel(byte[] pData, int width, int height, int bytesPerPixelPerChannel, int totalBytes)
    {
        if (_headerInfo.Channels < 3)
            return;

        int row = 0, col = 0;
        byte[] colorValue = new byte[8];
        double maxColours = Math.Pow(2, _headerInfo.BitsPerPixel);

        for (int counter = 0; counter < totalBytes; counter += _headerInfo.Channels * bytesPerPixelPerChannel)
        {
            Array.Copy(pData, counter, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exC = BitConverter.ToUInt32(colorValue, 0);

            Array.Copy(pData, counter + bytesPerPixelPerChannel, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exM = BitConverter.ToUInt32(colorValue, 0);

            Array.Copy(pData, counter + 2 * bytesPerPixelPerChannel, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exY = BitConverter.ToUInt32(colorValue, 0);

            double c = 1.0 - exC / maxColours;
            double m = 1.0 - exM / maxColours;
            double y = 1.0 - exY / maxColours;
            double k = 0;

            if (_headerInfo.Channels == 4)
            {
                Array.Copy(pData, counter + 3 * bytesPerPixelPerChannel, colorValue, 0, bytesPerPixelPerChannel);
                SwapBytes(colorValue, bytesPerPixelPerChannel);
                double exK = BitConverter.ToUInt32(colorValue, 0);
                k = 1.0 - exK / maxColours;
            }

            var (r, g, b) = CmykToRgb(c, m, y, k);
            SetPixel(col, row, r, g, b, 255);

            col++;
            if (col >= width)
            {
                col = 0;
                row++;
            }
        }
    }

    private void ProcessLab(byte[] pData, int width, int height, int bytesPerPixelPerChannel, int totalBytes)
    {
        int row = 0, col = 0;
        byte[] colorValue = new byte[8];
        double maxColours = Math.Pow(2, _headerInfo.BitsPerPixel);
        double lCoef = maxColours / 100.0;
        double aCoef = maxColours / 256.0;
        double bCoef = maxColours / 256.0;

        for (int counter = 0; counter < totalBytes; counter += 3 * bytesPerPixelPerChannel)
        {
            Array.Copy(pData, counter, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exL = BitConverter.ToUInt32(colorValue, 0);

            Array.Copy(pData, counter + bytesPerPixelPerChannel, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exA = BitConverter.ToUInt32(colorValue, 0);

            Array.Copy(pData, counter + 2 * bytesPerPixelPerChannel, colorValue, 0, bytesPerPixelPerChannel);
            SwapBytes(colorValue, bytesPerPixelPerChannel);
            double exB = BitConverter.ToUInt32(colorValue, 0);

            int l = (int)(exL / lCoef);
            int a = (int)(exA / aCoef - 128.0);
            int labB = (int)(exB / bCoef - 128.0);

            var (r, g, b) = LabToRgb(l, a, labB);
            SetPixel(col, row, r, g, b, 255);

            col++;
            if (col >= width)
            {
                col = 0;
                row++;
            }
        }
    }

    private void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
    {
        if (_pixelData == null) return;

        // Flip Y coordinate (PSD is top-down, we want bottom-up for some operations)
        // Actually Avalonia uses top-down, so no flip needed
        int index = (y * _headerInfo.Width + x) * 4;

        if (index + 3 >= _pixelData.Length) return;

        // BGRA format
        _pixelData[index] = b;
        _pixelData[index + 1] = g;
        _pixelData[index + 2] = r;
        _pixelData[index + 3] = a;
    }

    private static (byte R, byte G, byte B) CmykToRgb(double c, double m, double y, double k)
    {
        int red = (int)((1.0 - (c * (1 - k) + k)) * 255);
        int green = (int)((1.0 - (m * (1 - k) + k)) * 255);
        int blue = (int)((1.0 - (y * (1 - k) + k)) * 255);

        return ((byte)Math.Clamp(red, 0, 255), (byte)Math.Clamp(green, 0, 255), (byte)Math.Clamp(blue, 0, 255));
    }

    private static (byte R, byte G, byte B) LabToRgb(int l, int a, int b)
    {
        const double refX = 95.047;
        const double refY = 100.000;
        const double refZ = 108.883;

        double varY = (l + 16.0) / 116.0;
        double varX = a / 500.0 + varY;
        double varZ = varY - b / 200.0;

        varY = Math.Pow(varY, 3) > 0.008856 ? Math.Pow(varY, 3) : (varY - 16.0 / 116.0) / 7.787;
        varX = Math.Pow(varX, 3) > 0.008856 ? Math.Pow(varX, 3) : (varX - 16.0 / 116.0) / 7.787;
        varZ = Math.Pow(varZ, 3) > 0.008856 ? Math.Pow(varZ, 3) : (varZ - 16.0 / 116.0) / 7.787;

        double x = refX * varX;
        double y = refY * varY;
        double z = refZ * varZ;

        return XyzToRgb(x, y, z);
    }

    private static (byte R, byte G, byte B) XyzToRgb(double x, double y, double z)
    {
        double varX = x / 100.0;
        double varY = y / 100.0;
        double varZ = z / 100.0;

        double varR = varX * 3.2406 + varY * (-1.5372) + varZ * (-0.4986);
        double varG = varX * (-0.9689) + varY * 1.8758 + varZ * 0.0415;
        double varB = varX * 0.0557 + varY * (-0.2040) + varZ * 1.0570;

        varR = varR > 0.0031308 ? 1.055 * Math.Pow(varR, 1 / 2.4) - 0.055 : 12.92 * varR;
        varG = varG > 0.0031308 ? 1.055 * Math.Pow(varG, 1 / 2.4) - 0.055 : 12.92 * varG;
        varB = varB > 0.0031308 ? 1.055 * Math.Pow(varB, 1 / 2.4) - 0.055 : 12.92 * varB;

        int red = (int)(varR * 256.0);
        int green = (int)(varG * 256.0);
        int blue = (int)(varB * 256.0);

        return ((byte)Math.Clamp(red, 0, 255), (byte)Math.Clamp(green, 0, 255), (byte)Math.Clamp(blue, 0, 255));
    }

    private static bool SwapBytes(byte[] array, int length)
    {
        for (int i = 0; i < length / 2; i++)
        {
            (array[i], array[length - i - 1]) = (array[length - i - 1], array[i]);
        }
        return true;
    }
}
