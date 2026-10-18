using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoEditorApp.Services
{
    public interface IImageProcessor
    {
        Task<BitmapSource> ApplyGrayscaleAsync(BitmapSource source);
        Task<BitmapSource> ApplyInvertAsync(BitmapSource source);
        Task<BitmapSource> ApplySepiaAsync(BitmapSource source);
    }

    public class ImageProcessor : IImageProcessor
    {
        // Grayscale menggunakan native WPF API (Sangat Cepat)
        public async Task<BitmapSource> ApplyGrayscaleAsync(BitmapSource source)
        {
            if (source == null) return null;
            if (!source.IsFrozen && source.CanFreeze) source.Freeze();

            return await Task.Run(() =>
            {
                var formatConverted = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
                formatConverted.Freeze(); 
                return formatConverted;
            });
        }

        // Invert (Negative) menggunakan manipulasi Byte Array
        public async Task<BitmapSource> ApplyInvertAsync(BitmapSource source)
        {
            if (source == null) return null;
            if (!source.IsFrozen && source.CanFreeze) source.Freeze();

            return await Task.Run(() =>
            {
                // Standarisasi ke BGRA32 agar aman
                var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                int stride = converted.PixelWidth * 4;
                byte[] pixels = new byte[converted.PixelHeight * stride];
                converted.CopyPixels(pixels, stride, 0);

                // Manipulasi Paralel untuk performa maksimal pada gambar besar
                Parallel.For(0, pixels.Length / 4, i =>
                {
                    int index = i * 4;
                    pixels[index]     = (byte)(255 - pixels[index]);       // Blue
                    pixels[index + 1] = (byte)(255 - pixels[index + 1]);   // Green
                    pixels[index + 2] = (byte)(255 - pixels[index + 2]);   // Red
                    // Index + 3 adalah Alpha, biarkan utuh
                });

                var newBitmap = BitmapSource.Create(
                    converted.PixelWidth, converted.PixelHeight,
                    converted.DpiX, converted.DpiY,
                    PixelFormats.Bgra32, null, pixels, stride);
                
                newBitmap.Freeze();
                return newBitmap;
            });
        }

        // Sepia Filter
        public async Task<BitmapSource> ApplySepiaAsync(BitmapSource source)
        {
            if (source == null) return null;
            if (!source.IsFrozen && source.CanFreeze) source.Freeze();

            return await Task.Run(() =>
            {
                var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                int stride = converted.PixelWidth * 4;
                byte[] pixels = new byte[converted.PixelHeight * stride];
                converted.CopyPixels(pixels, stride, 0);

                Parallel.For(0, pixels.Length / 4, i =>
                {
                    int index = i * 4;
                    byte b = pixels[index];
                    byte g = pixels[index + 1];
                    byte r = pixels[index + 2];

                    // Standar kalkulasi matriks Sepia
                    int tr = (int)(0.393 * r + 0.769 * g + 0.189 * b);
                    int tg = (int)(0.349 * r + 0.686 * g + 0.168 * b);
                    int tb = (int)(0.272 * r + 0.534 * g + 0.131 * b);

                    pixels[index]     = (byte)(tb > 255 ? 255 : tb);
                    pixels[index + 1] = (byte)(tg > 255 ? 255 : tg);
                    pixels[index + 2] = (byte)(tr > 255 ? 255 : tr);
                });

                var newBitmap = BitmapSource.Create(
                    converted.PixelWidth, converted.PixelHeight,
                    converted.DpiX, converted.DpiY,
                    PixelFormats.Bgra32, null, pixels, stride);
                
                newBitmap.Freeze();
                return newBitmap;
            });
        }
    }
}
