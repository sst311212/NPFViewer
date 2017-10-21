using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NPF_Viewer
{
    public class IMGX2BMP
    {
        public static Byte[] Convert(Byte[] Buff)
        {
            IMGXHDR hdr = new IMGXHDR(Buff);

            uint out_len = ~((hdr.original_length << 16) | (hdr.original_length >> 16));
            byte[] out_buff = new byte[out_len];
            lzw_t lzw = new lzw_t(hdr.compress_data, (uint)hdr.compress_data.Length);
            lzw.uncompress(out_buff, out_len);

            IMGXHDR2 hdr2 = new IMGXHDR2(out_buff);

            Byte[] Data = new Byte[out_len];
            Data[0] = (int)'B';
            Data[1] = (int)'M';
            Buffer.BlockCopy(BitConverter.GetBytes(out_len), 0, Data, 2, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(out_len - (hdr2.width * hdr2.height * hdr2.depth / 8)), 0, Data, 10, 4);
            Buffer.BlockCopy(out_buff, 0, Data, 14, (int)out_len - 14);

            return Data;
        }
    }

    class IMGXHDR
    {
        public IMGXHDR(Byte[] Buff)
        {
            using (BinaryReader br = new BinaryReader(new MemoryStream(Buff)))
            {
                orig_size = (uint)br.BaseStream.Length;
                signature = br.ReadBytes(4);
                original_length = br.ReadUInt32();
                compress_data = br.ReadBytes((int)orig_size - signature.Length);
            }
        }
        public uint orig_size { set; get; }
        public byte[] signature { set; get; } = new byte[4];
        public uint original_length { set; get; }
        public byte[] compress_data { set; get; }
    }

    class IMGXHDR2
    {
        public IMGXHDR2(byte[] data)
        {
            using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
            {
                header_length = br.ReadUInt32();
                width = br.ReadUInt32();
                height = br.ReadUInt32();
                unknown1 = br.ReadUInt16();
                depth = br.ReadUInt16();
                unknown3 = br.ReadUInt32();
                data_length = br.ReadUInt32();
                unknown4 = br.ReadUInt32();
                unknown5 = br.ReadUInt32();
                color_count = br.ReadUInt32();
                unknown6 = br.ReadUInt32();
            }
        }
        public uint header_length { set; get; }
        public uint width { set; get; }
        public uint height { set; get; }
        public UInt16 unknown1 { set; get; }
        public UInt16 depth { set; get; }
        public uint unknown3 { set; get; }
        public uint data_length { set; get; }
        public uint unknown4 { set; get; }
        public uint unknown5 { set; get; }
        public uint color_count { set; get; }
        public uint unknown6 { set; get; }
    }

    class lzw_t
    {
        public lzw_t(byte[] _buff, uint _len)
        {
            buff = _buff;
            len = _len;
            saved_count = 0;
            saved_bits = 0;
            want_bits = 9;
            dict_index = 0x103;
        }
        public uint uncompress(byte[] out_buff, uint out_len)
        {
            byte[] temp_buff = new byte[65535];
            uint temp_len = 0;

            uint end = (uint)buff.LongLength;
            uint out_end = (uint)out_buff.LongLength;

            while (index_buff < end && index_out_buff < out_end)
            {
                clear_dict();

                uint index = get_bits(want_bits);
                uint value = index;
                out_buff[index_out_buff++] = (byte)value;

                uint prev_index = index;
                uint prev_value = value;

                while (index_buff < end && index_out_buff < out_end)
                {
                    index = get_bits(want_bits);
                    value = index;

                    // end of stream marker
                    if (index == 0x100) return out_len - (out_end - index_out_buff);

                    // extend index size marker
                    if (index == 0x101)
                    {
                        want_bits++;
                        continue;
                    }

                    // reset dictionary marker
                    if (index == 0x102) break;
                    
                    if (value >= dict_index)
                    {
                        temp_buff[0] = (byte)prev_value;
                        temp_len = 1;
                        value = prev_index;
                    }
                    else
                    {
                        temp_len = 0;
                    }

                    while (value > 0xFF)
                    {
                        temp_buff[temp_len++] = dict[value].value;
                        value = dict[value].child;
                    }

                    temp_buff[temp_len++] = (byte)value;

                    while (temp_len != 0 && index_out_buff < out_end)
                    {
                        out_buff[index_out_buff++] = temp_buff[temp_len-- - 1];
                    }

                    dict[dict_index].child = prev_index;
                    dict[dict_index].value = (byte)value;
                    dict_index++;

                    prev_index = index;
                    prev_value = value;
                }
            }

            return out_len - (out_end - index_out_buff);
        }
        uint get_bits(uint bits)
        {
            while (bits > saved_count)
            {
                saved_bits = (uint)((int)buff[index_buff++] << (int)saved_count) | saved_bits;
                saved_count += 8;
            }

            uint val = saved_bits & (uint)~(0xFFFFFFFF << (int)bits);
            saved_bits = (uint)((int)saved_bits >> (int)bits);
            saved_count -= bits;

            return val;
        }
        private void clear_dict()
        {
            want_bits = 9;
            dict_index = 0x103;

            for (uint i = 0; i < dict.Length; i++)
            {
                dict[i].child = 4294967295;
                dict[i].value = 0;
            }
        }
        private struct dict_entry_t
        {
            public uint child;
            public byte value;
        };
        private dict_entry_t[] dict = new dict_entry_t[512000];
        private uint index_buff = 0;
        private uint index_out_buff = 0;
        private uint want_bits { set; get; }
        private uint dict_index { set; get; }
        private byte[] buff { set; get; }
        private uint len { set; get; }
        private uint saved_count { set; get; }
        private uint saved_bits { set; get; }
    }
}
