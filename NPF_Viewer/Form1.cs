using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Imaging;

namespace NPF_Viewer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public int MakeXorKey(int K)
        {
            if (K == 0)
                K = 0x67895;
            for (int i = 0; i < 32; i++)
            {
                K ^= 0x65AC9365;
                K ^= (((K >> 1) ^ K) >> 3) ^ (((K << 1) ^ K) << 3);
            }
            return K;
        }

        public Byte[] ReadEncByte(Stream fd, int Offset, int Length, int Key)
        {
            if (Offset != 0)
                fd.Position = Offset;
            Byte[] Buff = new Byte[Length];
            fd.Read(Buff, 0, Length);
            int K = MakeXorKey(Key);
            for (int i = 0; i < Length; i++)
            {
                K ^= 0x65AC9365;
                K ^= (((K >> 1) ^ K) >> 3) ^ (((K << 1) ^ K) << 3);
                Buff[i] ^= Convert.ToByte(K & 255);
            }
            return Buff;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            listBox1.Items.Clear();

            String FileName = (e.Data.GetData(DataFormats.FileDrop) as String[])[0];
            Stream fd = File.OpenRead(FileName);

            var PACKHDR = new Byte[12];
            fd.Read(PACKHDR, 0, PACKHDR.Length);
            if (Encoding.ASCII.GetString(PACKHDR, 0, 4) != "PACK")
                return;

            var DATAHDR = ReadEncByte(fd, 0, 20, 0x46415420);
            if (Encoding.ASCII.GetString(DATAHDR, 0, 4) != "FAT ")
                return;

            int dwCount = BitConverter.ToInt32(DATAHDR, 8);
            var INFOHDR = ReadEncByte(fd, 0, dwCount * 20, dwCount);

            for (int i = 0; i < INFOHDR.Length; i += 20)
            {
                NPFEntry info = new NPFEntry(INFOHDR.Skip(i).ToArray());
                info.FileRoot = FileName;
                info.FileName = Encoding.GetEncoding(932).GetString(ReadEncByte(fd, info.Name_Offset, info.Name_Length, info.Data_XorKey));
                listBox1.Items.Add(info);
            }

            fd.Close();
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            int dwCount = listBox1.SelectedItems.Count;
            if (dwCount != 0)
            {
                FolderBrowserDialog fbd = new FolderBrowserDialog();
                fbd.ShowNewFolderButton = true;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    foreach (NPFEntry info in listBox1.SelectedItems)
                    {
                        Stream ifs = File.OpenRead(info.FileRoot);
                        String FileName = fbd.SelectedPath + "\\" + info.FileName;
                        Directory.CreateDirectory(Path.GetDirectoryName(FileName));
                        Byte[] Buff = IMGX2BMP.Convert(ReadEncByte(ifs, info.Data_Offset, info.Data_Length, info.Data_XorKey));
                        ifs.Close();
                        Bitmap bmp = new Bitmap(new MemoryStream(Buff));
                        bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                        bmp.Save(Path.ChangeExtension(FileName, "bmp"), ImageFormat.Bmp);
                        bmp.Dispose();
                    }
                }
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            NPFEntry info = listBox1.SelectedItem as NPFEntry;
            if (Path.GetExtension(info.FileName) != ".img")
                return;
            Stream ifs = File.OpenRead(info.FileRoot);
            Byte[] Buff = IMGX2BMP.Convert(ReadEncByte(ifs, info.Data_Offset, info.Data_Length, info.Data_XorKey));
            ifs.Close();
            pictureBox1.Image = new Bitmap(new MemoryStream(Buff));
            pictureBox1.Image.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }
    }

    public class NPFEntry
    {
        public Int32 Name_Offset;
        public Int32 Name_Length;
        public Int32 Data_Offset;
        public Int32 Data_Length;
        public Int32 Data_XorKey;
        public String FileRoot;
        public String FileName;

        public NPFEntry(Byte[] Buff)
        {
            Name_Offset = BitConverter.ToInt32(Buff, 0);
            Data_Offset = BitConverter.ToInt32(Buff, 4);
            Data_XorKey = BitConverter.ToInt32(Buff, 8);
            Name_Length = BitConverter.ToInt32(Buff, 12);
            Data_Length = BitConverter.ToInt32(Buff, 16);
        }

        public override string ToString()
        {
            return FileName;
        }
    }
}
