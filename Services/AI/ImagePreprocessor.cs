using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace VANTAGE.Services.AI
{
    // Preprocesses images to improve OCR accuracy for handwritten text
    public static class ImagePreprocessor
    {
        // Preprocess image for better OCR results
        // Applies grayscale conversion and contrast enhancement
        public static byte[] PreprocessForOcr(byte[] imageBytes)
        {
            using var inputStream = new MemoryStream(imageBytes);
            using var original = Image.FromStream(inputStream);
            using var bitmap = new Bitmap(original);

            // Step 1: Convert to grayscale
            using var grayscale = ConvertToGrayscale(bitmap);

            // Step 2: Enhance contrast (30% boost helps handwriting stand out)
            using var enhanced = EnhanceContrast(grayscale, 1.3f);

            // Return as PNG
            using var outputStream = new MemoryStream();
            enhanced.Save(outputStream, ImageFormat.Png);
            return outputStream.ToArray();
        }

        // Convert image to grayscale - standard preprocessing for OCR
        private static Bitmap ConvertToGrayscale(Bitmap original)
        {
            var grayscale = new Bitmap(original.Width, original.Height);

            using var graphics = Graphics.FromImage(grayscale);

            // Grayscale color matrix
            var colorMatrix = new ColorMatrix(new float[][]
            {
                new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix);

            graphics.DrawImage(original,
                new Rectangle(0, 0, original.Width, original.Height),
                0, 0, original.Width, original.Height,
                GraphicsUnit.Pixel,
                attributes);

            return grayscale;
        }

        // Enhance contrast to make handwriting more visible
        // factor > 1.0 increases contrast, < 1.0 decreases it
        private static Bitmap EnhanceContrast(Bitmap original, float factor)
        {
            var enhanced = new Bitmap(original.Width, original.Height);

            using var graphics = Graphics.FromImage(enhanced);

            // Contrast adjustment: scale colors away from middle gray
            // Formula: newColor = (oldColor - 0.5) * factor + 0.5
            float translate = (1.0f - factor) / 2.0f;

            var contrastMatrix = new ColorMatrix(new float[][]
            {
                new float[] { factor, 0, 0, 0, 0 },
                new float[] { 0, factor, 0, 0, 0 },
                new float[] { 0, 0, factor, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { translate, translate, translate, 0, 1 }
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(contrastMatrix);

            graphics.DrawImage(original,
                new Rectangle(0, 0, original.Width, original.Height),
                0, 0, original.Width, original.Height,
                GraphicsUnit.Pixel,
                attributes);

            return enhanced;
        }
    }
}
