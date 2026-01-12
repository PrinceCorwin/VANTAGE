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
                if (structure == null)
                {
                    AppLogger.Warning("Failed to parse DrawingsStructure JSON", "DrawingsRenderer.Render");
                    return document;
                }

                // Load drawing files based on source
                List<string> drawingFiles = LoadDrawingFiles(structure, context);

                if (drawingFiles.Count == 0)
                {
                    // Return empty document - no placeholder page needed
                    // The work package will simply not have a drawings section
                    AppLogger.Info("No drawing files found for work package", "DrawingsRenderer.Render");
                    return document;
                }

                // Import all PDF pages directly
                ImportPdfFiles(document, drawingFiles);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DrawingsRenderer.Render");
            }

            return document;
        }

        // Load drawing files from pre-fetched Drawings subfolder
        // Both Local and Procore fetches in Generate tab copy PDFs to {outputFolder}/Drawings/
        private List<string> LoadDrawingFiles(DrawingsStructure structure, TokenContext context)
        {
            var files = new List<string>();

            // Look for pre-fetched PDFs in the Drawings subfolder of output folder
            if (string.IsNullOrEmpty(context.OutputFolder))
            {
                AppLogger.Warning("Output folder not set - cannot load drawings", "DrawingsRenderer.LoadDrawingFiles");
                return files;
            }

            var drawingsFolder = Path.Combine(context.OutputFolder, "Drawings");
            if (!Directory.Exists(drawingsFolder))
            {
                AppLogger.Info($"No drawings folder found at {drawingsFolder}. Use Fetch Drawings in Generate tab first.", "DrawingsRenderer.LoadDrawingFiles");
                return files;
            }

            // Get DwgNO values for this work package from database
            var dwgNumbers = GetDwgNumbersForWorkPackage(context.ProjectID, context.WorkPackage);
            if (dwgNumbers.Count == 0)
            {
                AppLogger.Info($"No DwgNO values found for work package {context.WorkPackage}", "DrawingsRenderer.LoadDrawingFiles");
                return files;
            }

            // Get all PDF files in the drawings folder
            var allPdfFiles = Directory.GetFiles(drawingsFolder, "*.pdf", SearchOption.TopDirectoryOnly);

            // Find matching PDF files for each DwgNO
            var matchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dwgNo in dwgNumbers)
            {
                // Try full DwgNO contains match first
                var matches = allPdfFiles
                    .Where(f => Path.GetFileNameWithoutExtension(f)
                        .Contains(dwgNo, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Fallback: try last two segments (e.g., "017004-01" from "LP150-TWSP-017004-01")
                if (matches.Count == 0)
                {
                    var shortMatch = GetLastTwoSegments(dwgNo);
                    if (!string.IsNullOrEmpty(shortMatch))
                    {
                        matches = allPdfFiles
                            .Where(f => Path.GetFileNameWithoutExtension(f)
                                .Contains(shortMatch, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                }

                foreach (var match in matches)
                {
                    if (matchedFiles.Add(match))
                    {
                        files.Add(match);
                    }
                }
            }

            // Sort files by name for consistent ordering
            files = files.OrderBy(f => Path.GetFileName(f)).ToList();

            if (files.Count > 0)
            {
                AppLogger.Info($"Found {files.Count} drawing PDF(s) for {context.WorkPackage}", "DrawingsRenderer.LoadDrawingFiles");
            }
            else
            {
                AppLogger.Info($"No matching drawing PDFs found for {context.WorkPackage}. DwgNOs: {string.Join(", ", dwgNumbers)}", "DrawingsRenderer.LoadDrawingFiles");
            }

            return files;
        }

        // Extract last two hyphen-separated segments from a DwgNO (e.g., "017004-01" from "LP150-TWSP-017004-01")
        private string? GetLastTwoSegments(string dwgNo)
        {
            if (string.IsNullOrEmpty(dwgNo)) return null;

            var parts = dwgNo.Split('-');
            if (parts.Length >= 2)
            {
                return $"{parts[^2]}-{parts[^1]}";
            }
            return null;
        }

        // Get distinct DwgNO values for a work package from the database
        private List<string> GetDwgNumbersForWorkPackage(string projectId, string workPackage)
        {
            var dwgNumbers = new List<string>();

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT DISTINCT DwgNO
                    FROM Activities
                    WHERE ProjectID = @projectId
                    AND WorkPackage = @workPackage
                    AND DwgNO IS NOT NULL AND DwgNO != ''
                    ORDER BY DwgNO";

                cmd.Parameters.AddWithValue("@projectId", projectId);
                cmd.Parameters.AddWithValue("@workPackage", workPackage);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    dwgNumbers.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DrawingsRenderer.GetDwgNumbersForWorkPackage");
            }

            return dwgNumbers;
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
