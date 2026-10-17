using System;
using System.Threading.Tasks;

namespace PhotoEditorApp.Services;

/// <summary>
/// Memproses manipulasi gambar berkinerja tinggi di background thread.
/// Menggunakan manipulasi array langsung dan Parallel.For untuk efisiensi CPU.
/// </summary>
public static class ImageProcessor
{
    // Menerapkan Kecerahan (Brightness) dan Kontras (Contrast) secara bersamaan
    public static async Task<byte[]> ApplyAdjustmentsAsync(byte[] sourcePixels, int brightness, double contrast)
    {
        return await Task.Run(() =>
        {
            byte[] result = new byte[sourcePixels.Length];
            double contrastFactor = (259.0 * (contrast + 255.0)) / (255.0 * (259.0 - contrast));

            // Pemrosesan multi-core untuk gambar resolusi tinggi
            Parallel.For(0, sourcePixels.Length / 4, i =>
            {
                int index = i * 4;

                // Proses B, G, R (Format BGRA32)
                for (int j = 0; j < 3; j++)
                {
                    // 1. Terapkan kontras
                    double pixel = ((sourcePixels[index + j] / 255.0 - 0.5) * contrastFactor + 0.5) * 255.0;
                    
                    // 2. Terapkan kecerahan
                    pixel += brightness;

                    // 3. Clamp nilai agar tetap di rentang 0-255
                    result[index + j] = (byte)Clamp(pixel, 0, 255);
                }
                
                // Alpha channel (A) tetap sama
                result[index + 3] = sourcePixels[index + 3]; 
            });

            return result;
        }).ConfigureAwait(false);
    }

    public static async Task<byte[]> ApplyGrayscaleAsync(byte[] sourcePixels)
    {
        return await Task.Run(() =>
        {
            byte[] result = new byte[sourcePixels.Length];
            Parallel.For(0, sourcePixels.Length / 4, i =>
            {
                int index = i * 4;
                // Menggunakan rumus luminansi yang presisi (BT.601)
                byte b = sourcePixels[index];
                byte g = sourcePixels[index + 1];
                byte r = sourcePixels[index + 2];
                
                byte gray = (byte)((0.299 * r) + (0.587 * g) + (0.114 * b));

                result[index] = gray;
                result[index + 1] = gray;
                result[index + 2] = gray;
                result[index + 3] = sourcePixels[index + 3];
            });
            return result;
        }).ConfigureAwait(false);
    }

    public static async Task<byte[]> ApplyInvertAsync(byte[] sourcePixels)
    {
        return await Task.Run(() =>
        {
            byte[] result = new byte[sourcePixels.Length];
            Parallel.For(0, sourcePixels.Length / 4, i =>
            {
                int index = i * 4;
                result[index] = (byte)(255 - sourcePixels[index]);         // B
                result[index + 1] = (byte)(255 - sourcePixels[index + 1]); // G
                result[index + 2] = (byte)(255 - sourcePixels[index + 2]); // R
                result[index + 3] = sourcePixels[index + 3];               // A
            });
            return result;
        }).ConfigureAwait(false);
    }

    // Filter Lanjutan: Box Blur Cepat
    public static async Task<byte[]> ApplyBlurAsync(byte[] sourcePixels, int width, int height, int blurSize = 3)
    {
        return await Task.Run(() =>
        {
            byte[] result = new byte[sourcePixels.Length];
            Array.Copy(sourcePixels, result, sourcePixels.Length);

            Parallel.For(blurSize, height - blurSize, y =>
            {
                for (int x = blurSize; x < width - blurSize; x++)
                {
                    int b = 0, g = 0, r = 0, count = 0;

                    for (int ky = -blurSize; ky <= blurSize; ky++)
                    {
                        for (int kx = -blurSize; kx <= blurSize; kx++)
                        {
                            int pixelIndex = ((y + ky) * width + (x + kx)) * 4;
                            b += sourcePixels[pixelIndex];
                            g += sourcePixels[pixelIndex + 1];
                            r += sourcePixels[pixelIndex + 2];
                            count++;
                        }
                    }

                    int currentIndex = (y * width + x) * 4;
                    result[currentIndex] = (byte)(b / count);
                    result[currentIndex + 1] = (byte)(g / count);
                    result[currentIndex + 2] = (byte)(r / count);
                }
            });
            return result;
        }).ConfigureAwait(false);
    }

    private static int Clamp(double value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return (int)value;
    }
}
