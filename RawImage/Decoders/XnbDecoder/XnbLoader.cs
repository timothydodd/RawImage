using System;
using System.Collections.Generic;
using System.IO;

namespace RawImage.Decoders.XnbDecoder;

public static class XnbLoader
{
    public static ImageData? LoadXnb(Stream input)
    {
        var imageData = new ImageData();
        var reader = new BinaryReader(input);

        byte[] magicNumber = reader.ReadBytes(3);

        if (magicNumber[0] != 0x58 || magicNumber[1] != 0x4E || magicNumber[2] != 0x42)
        {
            return null; // Invalid XNB file header
        }

        char platform = Convert.ToChar(reader.ReadByte());

        if (platform != 'w' && platform != 'm' && platform != 'x')
        {
            return null; // XNB target platform not supported
        }

        byte xnbVersion = reader.ReadByte();

        if (xnbVersion != 0x05)
        {
            return null; // XNB version not supported
        }

        byte flags = reader.ReadByte();

        if (flags == 0x80)
        {
            return null; // Compressed XNB not supported
        }

        int totalLength = reader.ReadInt32();
        byte numTypeReaders = reader.ReadByte();
        string type = reader.ReadString();

        if (!type.Contains("Texture2DReader"))
        {
            return null; // XNB is not a valid Texture2D resource
        }

        if (!type.Contains("Version=4.0.0.0"))
        {
            return null; // XNB invalid version
        }

        int typeVersion = reader.ReadInt32();
        byte numSharedResources = reader.ReadByte();
        byte nullSharedResource = reader.ReadByte();

        var imageFormat = (PixelFormat)reader.ReadInt32();

        if (!IsSupportedPixelFormat(imageFormat))
        {
            return null; // Pixel format not supported
        }

        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int numLevels = reader.ReadInt32();

        var mipmapSizes = GetMipmapSizes(width, height);

        int canvasWidth = 0;
        for (int i = 0; i < mipmapSizes.Length; i++)
        {
            canvasWidth += mipmapSizes[i].X;
        }

        if (numLevels > 1)
        {
            imageData.Width = canvasWidth;
            imageData.Height = height;
        }
        else
        {
            imageData.Width = width;
            imageData.Height = height;
        }

        for (int i = 0; i < numLevels; i++)
        {
            int mipmapWidth = mipmapSizes[i].X;
            int mipmapHeight = mipmapSizes[i].Y;

            int dataSize = reader.ReadInt32();
            var sourceData = reader.ReadBytes(dataSize);
            var finalData = new byte[mipmapWidth * mipmapHeight * 4];

            bool needsSwapping = false;

            switch (imageFormat)
            {
                case PixelFormat.Alpha8:
                    for (int j = 0; j < dataSize; j++)
                    {
                        Array.Copy(new Alpha8Pixel(Subarray(sourceData, j, 1)).ToRgba(platform == 'x').Data, 0, finalData, j * 4, 4);
                    }
                    break;

                case PixelFormat.Bgr565:
                    for (int j = 0; j < dataSize / 2; j++)
                    {
                        Array.Copy(new Bgr565Pixel(Subarray(sourceData, j * 2, 2)).ToRgba(platform == 'x').Data, 0, finalData, j * 4, 4);
                    }
                    break;

                case PixelFormat.Bgra4444:
                    for (int j = 0; j < dataSize / 2; j++)
                    {
                        Array.Copy(new Bgra4444Pixel(Subarray(sourceData, j * 2, 2)).ToRgba(platform == 'x').Data, 0, finalData, j * 4, 4);
                    }
                    break;

                case PixelFormat.Bgra5551:
                    for (int j = 0; j < dataSize / 2; j++)
                    {
                        Array.Copy(new Bgra5551Pixel(Subarray(sourceData, j * 2, 2)).ToRgba(platform == 'x').Data, 0, finalData, j * 4, 4);
                    }
                    break;

                case PixelFormat.Rgba:
                    for (int j = 0; j < dataSize / 4; j++)
                    {
                        Array.Copy(new RgbaPixel(Subarray(sourceData, j * 4, 4)).ToRgba(platform == 'x').Data, 0, finalData, j * 4, 4);
                    }
                    needsSwapping = true;
                    break;
            }

            if (needsSwapping)
            {
                for (int j = 0; j < finalData.Length; j += 4)
                {
                    byte tmp = finalData[j];
                    finalData[j] = finalData[j + 2];
                    finalData[j + 2] = tmp;
                }
            }

            imageData.Pixels = finalData;
            break; // Only use first mipmap level
        }

        return imageData;
    }

    private static bool IsSupportedPixelFormat(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.Alpha8 => true,
            PixelFormat.Bgr565 => true,
            PixelFormat.Bgra4444 => true,
            PixelFormat.Bgra5551 => true,
            PixelFormat.Dxt1 => true,
            PixelFormat.Dxt3 => true,
            PixelFormat.Dxt5 => true,
            PixelFormat.Rgba => true,
            _ => false
        };
    }

    private static (int X, int Y)[] GetMipmapSizes(int width, int height)
    {
        var sizes = new List<(int, int)>();
        sizes.Add((width, height));

        while (width != 1 && height != 1)
        {
            width /= 2;
            if (width < 1) width = 1;

            height /= 2;
            if (height < 1) height = 1;

            sizes.Add((width, height));
        }

        return sizes.ToArray();
    }

    private static byte[] Subarray(byte[] data, int index, int length)
    {
        var result = new byte[length];
        Array.Copy(data, index, result, 0, length);
        return result;
    }
}
