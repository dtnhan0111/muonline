using NVorbis.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.BMD
{
    public class ItemBMDReader : BaseReader<List<ItemBMD>>
    {
        protected override List<ItemBMD> Read(byte[] buffer)
        {
            List<ItemBMD> items = [];
            var len = buffer.Length;
            using var br = new BinaryReader(new MemoryStream(buffer));
            var itemCount = br.ReadInt32();
            var payloadLength = len - 8; //len minus 4 bytes item count and 4 bytes crc at the end

            // Guard against a corrupted/incompatible header (e.g. an item.bmd that needs a
            // whole-file decryption step this reader doesn't perform): a bogus itemCount
            // makes BytesPerItem truncate to 0, which previously caused an infinite loop
            // since the read position would never advance.
            if (itemCount <= 0 || payloadLength <= 0 || itemCount > payloadLength)
            {
                Console.WriteLine($"[ItemBMDReader] Invalid item count ({itemCount}) for payload of {payloadLength} bytes - file may be encrypted or in an unsupported format. Returning empty item list.");
                return items;
            }

            var bytesPerItem = payloadLength / itemCount;
            if (bytesPerItem <= 0)
            {
                Console.WriteLine($"[ItemBMDReader] Computed BytesPerItem <= 0 (itemCount={itemCount}, payloadLength={payloadLength}). Returning empty item list.");
                return items;
            }

            while (br.BaseStream.Position + bytesPerItem <= br.BaseStream.Length - 4) //ignore last 4 bytes that is crc
            {
                var itemBytes = br.ReadBytes(bytesPerItem);
                if (itemBytes.Length < bytesPerItem)
                    break; // truncated read near EOF, stop instead of looping forever

                XOR3(ref itemBytes);
                using var itemReader = new BinaryReader(new MemoryStream(itemBytes));
                var item = itemReader.ReadStruct<ItemBMD>();
                items.Add(item);
            }
            return items;
        }


        private void XOR3(ref byte[] data)
        {
            byte[] XOR_3_KEY = { 0xFC, 0xCF, 0xAB };
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= XOR_3_KEY[i % 3];
            }
        }
    }
}
