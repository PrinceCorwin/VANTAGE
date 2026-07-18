using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;
using VANTAGE.Models;
using VANTAGE.Services;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Renderer for Drawings type templates
    // Imports PDF pages directly into the work package, preserving original page size (typically 11x17)
    public class DrawingsRenderer : BaseRenderer
    {
        // Keep loaded PDF documents alive - Merge creates references, not copies
        // These must remain open until the final merged document is saved
        private readonly List<PdfLoadedDocument> _loadedDocuments = new();

        // Clear loaded documents after generation is complete
        public void ClearLoadedDocuments()
        {
            foreach (var doc in _loadedDocuments)
            {
                try { doc.Close(true); } catch { }
            }
            _loadedDocuments.Clear();
        }

        public override PdfDocument Render(string structureJson, TokenContext context, string? logoPath = null)
        {
            var document = CreateDocument();

            try
            {
                var structure = JsonSerializer.Deserialize<DrawingsStructure>(structureJson);
                if (structure == null || string.IsNullOrWhiteSpace(structure.ParentFolderPath))
                {
                    AppLogger.Warning("Drawings template has no parent folder", "DrawingsRenderer.Render");
                    return document;
                }

                // The drawings for this work package live in a subfolder named exactly the
                // WorkPackage value. Missing subfolder -> empty document (the generator skips it;
                // the user was warned about missing folders before generation started).
                string wpFolder = Path.Combine(structure.ParentFolderPath, context.WorkPackage);
                if (!Directory.Exists(wpFolder))
                {
                    AppLogger.Warning($"Drawings subfolder not found: {wpFolder}", "DrawingsRenderer.Render");
                    return document;
                }

                var pdfFiles = Directory.GetFiles(wpFolder, "*.pdf", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (pdfFiles.Count == 0)
                {
                    AppLogger.Info($"No PDFs in drawings subfolder: {wpFolder}", "DrawingsRenderer.Render");
                    return document;
                }

                ImportPdfFiles(document, pdfFiles);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DrawingsRenderer.Render");
            }

            return document;
        }

        // Import PDF files directly into the document using Merge
        // Note: Merge creates references, so source documents must stay open until final save
        private void ImportPdfFiles(PdfDocument document, List<string> pdfFiles)
        {
            foreach (var filePath in pdfFiles)
            {
                try
                {
                    string extension = Path.GetExtension(filePath).ToLowerInvariant();

                    if (extension == ".pdf")
                    {
                        // Load the PDF - DO NOT use 'using' as Merge creates references
                        var loadedDoc = new PdfLoadedDocument(filePath);
                        _loadedDocuments.Add(loadedDoc); // Keep alive until generation complete
                        int pageCount = loadedDoc.Pages.Count;

                        // Merge pages into destination
                        PdfDocumentBase.Merge(document, loadedDoc);

                        AppLogger.Info($"Merged {pageCount} page(s) from {Path.GetFileName(filePath)}", "DrawingsRenderer.ImportPdfFiles");
                    }
                    else
                    {
                        AppLogger.Warning($"Skipping non-PDF file: {Path.GetFileName(filePath)} (only PDF files are supported for drawings)", "DrawingsRenderer.ImportPdfFiles");
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, $"DrawingsRenderer.ImportPdfFiles({Path.GetFileName(filePath)})");
                }
            }
        }
    }
}
