using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoEditorApp.Services
{
    public interface IImageProcessor
    {
        Task<BitmapSource> ApplyGrayscaleAsync(BitmapSource source);
    }

    public class ImageProcessor : IImageProcessor
    {
        public async Task<BitmapSource> ApplyGrayscaleAsync(BitmapSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Pastikan source sudah di-freeze jika datang dari UI thread
            if (!source.IsFrozen && source.CanFreeze)
                source.Freeze();

            return await Task.Run(() =>
            {
                // FormatConvertedBitmap adalah cara paling efisien (native WPF) 
                // untuk mengubah gambar menjadi Grayscale tanpa manipulasi array piksel manual.
                var formatConverted = new FormatConvertedBitmap();
                formatConverted.BeginInit();
                formatConverted.Source = source;
                formatConverted.DestinationFormat = PixelFormats.Gray8;
                formatConverted.EndInit();

                // SANGAT PENTING: Harus di-freeze sebelum dikembalikan ke UI Thread
                formatConverted.Freeze(); 
                return (BitmapSource)formatConverted;
            });
        }
    }
}
