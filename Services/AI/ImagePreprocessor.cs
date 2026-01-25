using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace VANTAGE.Services.AI
{
    // Preprocesses images to improve OCR accuracy for handwritten text
    public static class ImagePreprocessor
    {
        // Preprocess image for better OCR results - applies contrast enhancement
        // contrastFactor: 1.0 = no change, 2.0 = double contrast
        public static byte[] PreprocessForOcr(byte[] imageBytes, float contrastFactor = 1.2f)
        {
            // Skip preprocessing if contrast is effectively unchanged
            if (Math.Abs(contrastFactor - 1.0f) < 0.01f)
                return imageBytes;

            using var inputStream = new MemoryStream(imageBytes);
            using var original = Image.FromStream(inputStream);
            using var bitmap = new Bitmap(original);

            // Apply contrast enhancement only (no grayscale - images are already B&W)
            using var enhanced = EnhanceContrast(bitmap, contrastFactor);

            // Return as PNG
            using var outputStream = new MemoryStream();
            enhanced.Save(outputStream, ImageFormat.Png);
            return outputStream.ToArray();
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
