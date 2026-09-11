using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.OZB
{
    public class OZBReader : BaseReader<OZB>
    {
        // Matches the byte layout ReadBM8 consumes before pixel data:
        // 4 (fileType+version) + 14 (header) + 40 (info) + 1026 (bmpHeader/palette).
        private const int Bm8HeaderSize = 4 + 14 + 40 + 1026;
        private const int RecoveredTerrainSize = 256; // matches Client.Main.Constants.TERRAIN_SIZE

        protected override OZB Read(byte[] buffer)
        {
            using var br = new BinaryReader(new MemoryStream(buffer));

            var fileType = br.ReadString(3);

            var version = br.ReadByte();

            switch (fileType)
            {
                case "BM6": return this.ReadBM6(br, version);
                case "BM8": return this.ReadBM8(br, version);
                case "BM": return this.ReadBM8(br, version);
            }

            // Some asset packs ship an OZB terrain heightmap whose BMP-style header was
            // stripped/zeroed by the export tool, leaving only the raw grayscale height
            // payload behind. If the header region is empty and there's roughly a full
            // TERRAIN_SIZE x TERRAIN_SIZE payload after it, recover the height data
            // directly instead of failing the whole world load.
            if (IsAllZero(buffer, 0, Math.Min(Bm8HeaderSize, buffer.Length)))
            {
                var recovered = TryRecoverHeadlessHeightmap(buffer, version);
                if (recovered != null)
                    return recovered;
            }

            throw new FileLoadException($"Invalid OZB file type. Expected BM6 or BM8, Received: {fileType}");
        }

        private static OZB TryRecoverHeadlessHeightmap(byte[] buffer, byte version)
        {
            int expectedPixels = RecoveredTerrainSize * RecoveredTerrainSize;
            int available = buffer.Length - Bm8HeaderSize;
            if (available <= 0)
                return null;

            int pixelCount = Math.Min(available, expectedPixels);
            var data = new Color[expectedPixels];
            for (int i = 0; i < expectedPixels; i++)
            {
                byte value = i < pixelCount ? buffer[Bm8HeaderSize + i] : (byte)0;
                data[i] = Color.FromArgb(255, value, 0, 0);
            }

            return new OZB
            {
                Version = version,
                Width = RecoveredTerrainSize,
                Height = RecoveredTerrainSize,
                Data = data
            };
        }

        private static bool IsAllZero(byte[] buffer, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++)
            {
                if (buffer[i] != 0)
                    return false;
            }
            return true;
        }

        private OZB ReadBM8(BinaryReader br, byte version)
        {
            // header (14 bytes)
            var type = br.ReadInt16();
            var size = br.ReadInt32();
            var res1 = br.ReadInt16();
            var res2 = br.ReadInt16();
            var offBits = br.ReadInt32();

            // info (40 bytes)
            var biSize = br.ReadInt32();
            var width = br.ReadInt32();
            var height = br.ReadInt32();
            var planes = br.ReadInt16();
            var bitCount = br.ReadInt16();
            var compression = br.ReadInt32();
            var sizeImage = br.ReadInt32();
            var xpelsPerMeter = br.ReadInt32();
            var ypelsPerMeter = br.ReadInt32();
            var clrUsed = br.ReadInt32();
            var clrImportant = br.ReadInt32();

            var bmpHeader = br.ReadBytes(1026);

            var backTerrainHeight = br.ReadBytes(width * height);

            return new OZB
            {
                Version = version,
                Width = width,
                Height = height,
                Data = backTerrainHeight.Select(x => Color.FromArgb(255, x, 0, 0)).ToArray()
            };
        }

        private OZB ReadBM6(BinaryReader br, byte version)
        {
            // header
            var type = br.ReadInt16();
            var size = br.ReadInt32();
            var res1 = br.ReadInt16();
            var res2 = br.ReadInt16();
            var offBits = br.ReadInt32();

            // info
            var biSize = br.ReadInt32();
            var width = br.ReadInt32();
            var height = br.ReadInt32();
            var planes = br.ReadInt16();
            var bitCount = br.ReadInt16();
            var compression = br.ReadInt32();
            var sizeImage = br.ReadInt32();
            var xpelsPerMeter = br.ReadInt32();
            var ypelsPerMeter = br.ReadInt32();
            var clrUsed = br.ReadInt32();
            var clrImportant = br.ReadInt32();

            Color[] data = new Color[width * height];

            for (var i = 0; i < data.Length; i++)
            {
                var b = br.ReadByte();
                var g = br.ReadByte();
                var r = br.ReadByte();
                data[i] = Color.FromArgb(255, r, g, b);
            }

            return new OZB
            {
                Version = version,
                Width = width,
                Height = height,
                Data = data
            };
        }
    }
}
