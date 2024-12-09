using BitMiracle.LibTiff.Classic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TiffUtility
{
    public partial class Form1 : Form
    {
        private Image _selectedImage;

        public Form1()
        {
            InitializeComponent();
            var btnLoadImage = new Button { Text = "Load Image", Left = 10, Top = 10, Width = 100 };
            btnLoadImage.Click += BtnLoadImage_Click;
            Controls.Add(btnLoadImage);

            var btnSaveTiff = new Button { Text = "Save as TIFF", Left = 120, Top = 10, Width = 100 };
            btnSaveTiff.Click += BtnSaveTiff_Click;
            Controls.Add(btnSaveTiff);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void BtnLoadImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "All Images|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.gif";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _selectedImage?.Dispose();
                    _selectedImage = Image.FromFile(ofd.FileName);
                    MessageBox.Show("Image loaded successfully!");
                }
            }
        }

        private void BtnSaveTiff_Click(object sender, EventArgs e)
        {
            if (_selectedImage == null)
            {
                MessageBox.Show("Please load an image first.");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "TIFF Image|*.tiff;*.tif";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Convert image to 1-bit pixel data
                        byte[] tiffBytes = ConvertTo1bppTiffBytes(_selectedImage);
                        File.WriteAllBytes(sfd.FileName, tiffBytes);
                        MessageBox.Show("TIFF saved successfully!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: " + ex.Message);
                    }
                }
            }
        }

        //
        private byte[] ConvertTo1bppTiffBytes(Image img)
        {
            int width = img.Width;
            int height = img.Height;

            byte[] imageData = ConvertTo1bppArray(img);

            using (MemoryStream ms = new MemoryStream())
            {
                using (Tiff tiff = Tiff.ClientOpen("InMemory", "w", ms, new InMemoryTiffStream()))
                {
                    if (tiff == null)
                        throw new Exception("Could not open TIFF in memory.");

                    tiff.SetField(TiffTag.IMAGEWIDTH, width);
                    tiff.SetField(TiffTag.IMAGELENGTH, height);
                    tiff.SetField(TiffTag.BITSPERSAMPLE, 1);
                    tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);

                    // 1 row per strip
                    tiff.SetField(TiffTag.ROWSPERSTRIP, 1);

                    // Photometric: MINISWHITE
                    tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);

                    // CCITT G4 compression
                    tiff.SetField(TiffTag.COMPRESSION, Compression.CCITTFAX4);

                    // 200 DPI
                    tiff.SetField(TiffTag.XRESOLUTION, 200.0);
                    tiff.SetField(TiffTag.YRESOLUTION, 200.0);
                    tiff.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);

                    tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

                    int bytesPerRow = (width + 7) / 8;
                    for (int row = 0; row < height; row++)
                    {
                        byte[] rowData = new byte[bytesPerRow];
                        Buffer.BlockCopy(imageData, row * bytesPerRow, rowData, 0, bytesPerRow);
                        if (!tiff.WriteScanline(rowData, row))
                            throw new Exception("Failed to write scanline");
                    }

                    tiff.WriteDirectory();
                }

                return ms.ToArray();
            }
        }

        private byte[] ConvertTo1bppArray(Image img)
        {
            int width = img.Width;
            int height = img.Height;
            int bytesPerRow = (width + 7) / 8;
            byte[] result = new byte[bytesPerRow * height];

            using (Bitmap bmp = new Bitmap(img))
            {
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, width, height),
                                             ImageLockMode.ReadOnly,
                                             PixelFormat.Format24bppRgb);
                try
                {
                    int stride = bd.Stride;
                    for (int y = 0; y < height; y++)
                    {
                        IntPtr rowPtr = bd.Scan0 + y * stride;
                        byte[] pixelRow = new byte[width * 3];
                        Marshal.Copy(rowPtr, pixelRow, 0, width * 3);

                        for (int x = 0; x < width; x++)
                        {
                            int b = pixelRow[x * 3 + 0];
                            int g = pixelRow[x * 3 + 1];
                            int r = pixelRow[x * 3 + 2];

                            int gray = (int)(r * 0.3 + g * 0.59 + b * 0.11);
                            bool isBlack = (gray < 128);

                            if (isBlack)
                            {
                                int index = y * bytesPerRow + (x / 8);
                                byte mask = (byte)(0x80 >> (x % 8));
                                result[index] |= mask;
                            }
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(bd);
                }
            }

            return result;
        }

    }

    internal class InMemoryTiffStream : BitMiracle.LibTiff.Classic.TiffStream
    {
        public override int Read(object clientData, byte[] buffer, int offset, int count)
        {
            Stream s = clientData as Stream;
            return s.Read(buffer, offset, count);
        }

        public override void Write(object clientData, byte[] buffer, int offset, int count)
        {
            Stream s = clientData as Stream;
            s.Write(buffer, offset, count);
        }

        public override long Seek(object clientData, long offset, SeekOrigin origin)
        {
            Stream s = clientData as Stream;
            return s.Seek(offset, origin);
        }

        public override void Close(object clientData)
        {
            Stream s = clientData as Stream;
            s.Close();
        }

        public override long Size(object clientData)
        {
            Stream s = clientData as Stream;
            return s.Length;
        }
    }
}
