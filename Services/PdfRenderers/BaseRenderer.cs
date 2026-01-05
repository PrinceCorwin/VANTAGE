using System;
using System.Drawing;
using System.IO;
using System.Windows;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Base class with shared rendering logic for all template types
    public abstract class BaseRenderer
    {
        // Standard page settings
        protected const float PageWidth = 612f;  // 8.5 inches at 72 DPI
        protected const float PageHeight = 792f; // 11 inches at 72 DPI
        protected const float MarginLeft = 36f;  // 0.5 inch
        protected const float MarginRight = 36f;
        protected const float MarginTop = 36f;
        protected const float MarginBottom = 36f;
        protected const float ContentWidth = PageWidth - MarginLeft - MarginRight;  // 540

        // Font settings
        protected static readonly PdfFont TitleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
        protected static readonly PdfFont HeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
        protected static readonly PdfFont BodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Regular);
        protected static readonly PdfFont SmallFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9, PdfFontStyle.Regular);
        protected static readonly PdfFont SmallBoldFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9, PdfFontStyle.Bold);

        // Colors
        protected static readonly PdfColor LightGray = new PdfColor(224, 224, 224);  // #E0E0E0
        protected static readonly PdfColor SectionGray = new PdfColor(208, 208, 208); // #D0D0D0
        protected static readonly PdfColor Black = new PdfColor(0, 0, 0);

        // Brushes
        protected static readonly PdfBrush BlackBrush = new PdfSolidBrush(Black);
        protected static readonly PdfBrush LightGrayBrush = new PdfSolidBrush(LightGray);
        protected static readonly PdfBrush SectionGrayBrush = new PdfSolidBrush(SectionGray);

        // Pens
        protected static readonly PdfPen ThinPen = new PdfPen(Black, 0.5f);
        protected static readonly PdfPen NormalPen = new PdfPen(Black, 1f);
        protected static readonly PdfPen ThickPen = new PdfPen(Black, 2f);

        // Path to default logo (embedded resource)
        protected const string DefaultLogoPath = "pack://application:,,,/Images/SummitLogoNoText.jpg";

        // Create a new PDF document with standard settings
        protected PdfDocument CreateDocument()
        {
            var document = new PdfDocument();
            document.PageSettings.Size = new SizeF(PageWidth, PageHeight);
            document.PageSettings.Margins.All = 0; // We handle margins manually
            return document;
        }

        // Render the common header block (logo, project info, title bar)
        // Returns the Y position after the header
        protected float RenderHeader(PdfPage page, TokenContext context, string title, string? logoPath = null)
        {
            var graphics = page.Graphics;
            float y = MarginTop;

            // Row 1: Logo | Project Name + Phone/Fax
            float logoWidth = 100f;
            float logoHeight = 50f;

            // Draw logo
            try
            {
                PdfImage? logo = LoadImage(logoPath);
                if (logo != null)
                {
                    // Scale to fit within logo area while maintaining aspect ratio
                    float scale = Math.Min(logoWidth / logo.Width, logoHeight / logo.Height);
                    float drawWidth = logo.Width * scale;
                    float drawHeight = logo.Height * scale;
                    graphics.DrawImage(logo, MarginLeft, y, drawWidth, drawHeight);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "BaseRenderer.RenderHeader (logo)");
            }

            // Draw project info to the right of logo
            float infoX = MarginLeft + logoWidth + 10f;
            string projectName = TokenResolver.Resolve("{ProjectName}", context);
            string phone = TokenResolver.Resolve("{Phone}", context);
            string fax = TokenResolver.Resolve("{Fax}", context);

            graphics.DrawString(projectName, HeaderFont, BlackBrush, new PointF(infoX, y));
            y += 15f;

            if (!string.IsNullOrEmpty(phone))
            {
                graphics.DrawString($"Phone: {phone}", SmallFont, BlackBrush, new PointF(infoX, y));
                y += 12f;
            }
            if (!string.IsNullOrEmpty(fax))
            {
                graphics.DrawString($"Fax: {fax}", SmallFont, BlackBrush, new PointF(infoX, y));
            }

            y = MarginTop + logoHeight + 10f;

            // Title bar with borders
            float titleBarHeight = 24f;
            graphics.DrawLine(ThickPen, MarginLeft, y, MarginLeft + ContentWidth, y);
            y += 2f;

            // Center the title text
            var titleSize = TitleFont.MeasureString(title);
            float titleX = MarginLeft + (ContentWidth - titleSize.Width) / 2;
            graphics.DrawString(title, TitleFont, BlackBrush, new PointF(titleX, y + 3f));

            y += titleBarHeight - 2f;
            graphics.DrawLine(ThickPen, MarginLeft, y, MarginLeft + ContentWidth, y);
            y += 4f;

            // PKG Manager / Scheduler row
            string pkgManager = TokenResolver.Resolve("{PKGManager}", context);
            string scheduler = TokenResolver.Resolve("{Scheduler}", context);

            graphics.DrawString($"PKG MGR: {pkgManager}", SmallFont, BlackBrush, new PointF(MarginLeft, y));

            var schedulerText = $"Scheduler: {scheduler}";
            var schedulerSize = SmallFont.MeasureString(schedulerText);
            graphics.DrawString(schedulerText, SmallFont, BlackBrush,
                new PointF(MarginLeft + ContentWidth - schedulerSize.Width, y));

            y += 18f;

            return y;
        }

        // Load an image from file path or embedded resource
        protected PdfImage? LoadImage(string? imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    // Load default logo from embedded resource
                    var uri = new Uri(DefaultLogoPath);
                    var streamInfo = Application.GetResourceStream(uri);
                    if (streamInfo?.Stream != null)
                    {
                        return new PdfBitmap(streamInfo.Stream);
                    }
                    return null;
                }

                if (File.Exists(imagePath))
                {
                    return new PdfBitmap(imagePath);
                }

                // Try as resource path
                if (imagePath.StartsWith("pack://"))
                {
                    var uri = new Uri(imagePath);
                    var streamInfo = Application.GetResourceStream(uri);
                    if (streamInfo?.Stream != null)
                    {
                        return new PdfBitmap(streamInfo.Stream);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"BaseRenderer.LoadImage({imagePath})");
                return null;
            }
        }

        // Render footer text at the bottom of the page
        protected void RenderFooter(PdfPage page, string? footerText)
        {
            if (string.IsNullOrEmpty(footerText))
                return;

            var graphics = page.Graphics;
            float footerY = PageHeight - MarginBottom - 30f;

            // Draw separator line
            graphics.DrawLine(ThinPen, MarginLeft, footerY, MarginLeft + ContentWidth, footerY);
            footerY += 5f;

            // Draw footer text (may wrap)
            var format = new PdfStringFormat
            {
                Alignment = PdfTextAlignment.Left,
                LineAlignment = PdfVerticalAlignment.Top,
                WordWrap = PdfWordWrapType.Word
            };

            var footerRect = new RectangleF(MarginLeft, footerY, ContentWidth, 25f);
            graphics.DrawString(footerText, SmallFont, BlackBrush, footerRect, format);
        }

        // Abstract method for type-specific rendering
        public abstract PdfDocument Render(string structureJson, TokenContext context, string? logoPath = null);
    }
}
