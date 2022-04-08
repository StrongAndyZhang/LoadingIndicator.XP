using JetBrains.Annotations;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LoadingIndicator.XP
{
    public static class ImageExtensions
    {
        const int SRCCOPY = 0xCC0020;
        [DllImport("gdi32.dll")]
        static extern int BitBlt(IntPtr hdc, int x, int y, int cx, int cy,
            IntPtr hdcSrc, int x1, int y1, int rop);

        [NotNull]
        public static Image PrintClientRectangleToImage([NotNull] this Control control)
        {
            var clientRectangle = control.ClientRectangle;
            var bmp = new Bitmap(clientRectangle.Width, clientRectangle.Height);
            using (var bmpGraphics = Graphics.FromImage(bmp))
            {
                var bmpDC = bmpGraphics.GetHdc();
                using (Graphics formGraphics = Graphics.FromHwnd(control.Handle))
                {
                    var formDC = formGraphics.GetHdc();
                    BitBlt(bmpDC, 0, 0, clientRectangle.Width, clientRectangle.Height, formDC, 0, 0, SRCCOPY);
                    formGraphics.ReleaseHdc(formDC);
                }
                bmpGraphics.ReleaseHdc(bmpDC);
            }
            return bmp;
        }

        [NotNull]
        public static Bitmap ToBitmap([NotNull] this Image sourceImage)
        {
            if (sourceImage == null) throw new ArgumentNullException(nameof(sourceImage));

            if (sourceImage is Bitmap bitmap)
            {
                return bitmap;
            }

            return new Bitmap(sourceImage);
        }

        [NotNull]
        public static Bitmap ImageBlurFilter([NotNull] this Bitmap sourceBitmap)
        {
            if (sourceBitmap == null) throw new ArgumentNullException(nameof(sourceBitmap));

            var resultBitmap = sourceBitmap.ConvolutionFilter(
                Matrix.GaussianBlur3x3, 1.0 / 16.0);

            return resultBitmap;
        }

        [NotNull]
        public static Bitmap MakeGrayscale([NotNull] this Image original)
        {
            var greyscaleMatrix = new ColorMatrix(
                new[]
                {
                    new[] { .3f, .3f, .3f, 0, 0 },
                    new[] { .59f, .59f, .59f, 0, 0 },
                    new[] { .11f, .11f, .11f, 0, 0 },
                    new[] { 0f, 0, 0, 1, 0 },
                    new[] { 0f, 0, 0, 0, 1 }
                });

            return original.ChangeColors(greyscaleMatrix);
        }

        [NotNull]
        public static Bitmap MakeSepia([NotNull] this Image original)
        {
            var sepiaMatrix = new ColorMatrix(
                new[]
                {
                    new[] { .393f, .349f, .272f, 0, 0 },
                    new[] { .769f, .686f, .534f, 0, 0 },
                    new[] { .189f, .168f, .131f, 0, 0 },
                    new[] { 0f, 0, 0, 1, 0 },
                    new[] { 0f, 0, 0, 0, 1 }
                });

            return original.ChangeColors(sepiaMatrix);
        }

        [NotNull]
        private static Bitmap ChangeColors([NotNull] this Image original, ColorMatrix colorMatrix)
        {
            var newBitmap = new Bitmap(original.Width, original.Height);

            using (var graphics = Graphics.FromImage(newBitmap))
            {
                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                graphics.DrawImage(
                    original,
                    new Rectangle(0, 0, original.Width, original.Height),
                    0,
                    0,
                    original.Width,
                    original.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }

            return newBitmap;
        }

        [NotNull]
        private static Bitmap ConvolutionFilter(
            [NotNull] this Bitmap sourceBitmap,
            [NotNull] double[,] filterMatrix,
            double factor = 1,
            int bias = 0)
        {
            var sourceData = sourceBitmap.LockBits(
                new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            var pixelBuffer = new byte[sourceData.Stride * sourceData.Height];
            var resultBuffer = new byte[sourceData.Stride * sourceData.Height];

            Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
            sourceBitmap.UnlockBits(sourceData);

            var filterWidth = filterMatrix.GetLength(1);

            var filterOffset = (filterWidth - 1) / 2;

            for (var offsetY = filterOffset; offsetY < sourceBitmap.Height - filterOffset; offsetY++)
            {
                for (var offsetX = filterOffset; offsetX < sourceBitmap.Width - filterOffset; offsetX++)
                {
                    double blue = 0;
                    double green = 0;
                    double red = 0;

                    var byteOffset = offsetY * sourceData.Stride + offsetX * 4;

                    for (var filterY = -filterOffset; filterY <= filterOffset; filterY++)
                    {
                        for (var filterX = -filterOffset; filterX <= filterOffset; filterX++)
                        {
                            var calcOffset = byteOffset +
                                             filterX * 4 +
                                             filterY * sourceData.Stride;

                            blue += pixelBuffer[calcOffset] *
                                    filterMatrix[filterY + filterOffset, filterX + filterOffset];

                            green += pixelBuffer[calcOffset + 1] *
                                     filterMatrix[filterY + filterOffset, filterX + filterOffset];

                            red += pixelBuffer[calcOffset + 2] *
                                   filterMatrix[filterY + filterOffset, filterX + filterOffset];
                        }
                    }

                    blue = factor * blue + bias;
                    green = factor * green + bias;
                    red = factor * red + bias;

                    blue = blue > 255 ? 255 : blue < 0 ? 0 : blue;

                    green = green > 255 ? 255 : green < 0 ? 0 : green;

                    red = red > 255 ? 255 : red < 0 ? 0 : red;

                    resultBuffer[byteOffset] = (byte)blue;
                    resultBuffer[byteOffset + 1] = (byte)green;
                    resultBuffer[byteOffset + 2] = (byte)red;
                    resultBuffer[byteOffset + 3] = 255;
                }
            }

            var resultBitmap = new Bitmap(sourceBitmap.Width, sourceBitmap.Height);

            var resultData = resultBitmap.LockBits(
                new Rectangle(0, 0, resultBitmap.Width, resultBitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            Marshal.Copy(resultBuffer, 0, resultData.Scan0, resultBuffer.Length);
            resultBitmap.UnlockBits(resultData);

            return resultBitmap;
        }

        private static class Matrix
        {
            [NotNull]
            public static double[,] GaussianBlur3x3
            {
                get
                {
                    return
                        new double[,]
                        {
                            { 1, 2, 1, },
                            { 2, 4, 2, },
                            { 1, 2, 1, }
                        };
                }
            }
        }
    }
}