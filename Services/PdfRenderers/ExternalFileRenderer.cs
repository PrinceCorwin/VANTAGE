using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Renderer for ExternalFile type templates. Merges the pages of a user-selected PDF
    // directly into the work package, preserving their original page size. If the file is
    // missing (or the path is blank), returns an empty document so the generator skips it -
    // the user is warned about missing files up front in the UI before generation starts.
    public class ExternalFileRenderer : BaseRenderer
    {
        // Merge creates references into the loaded documents, not copies, so they must stay
        // open until the final merged work package is saved.
        private readonly List<PdfLoadedDocument> _loadedDocuments = new();

        // Close loaded documents after generation is complete.
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
                var structure = JsonSerializer.Deserialize<ExternalFileStructure>(structureJson);
                if (structure == null || string.IsNullOrWhiteSpace(structure.FilePath))
                {
                    AppLogger.Warning("ExternalFile template has no file path", "ExternalFileRenderer.Render");
                    return document;
                }

                if (!File.Exists(structure.FilePath))
                {
                    // Return empty - the generator drops empty documents. The user has already
                    // been asked (in the UI) whether to proceed without missing external files.
                    AppLogger.Warning($"External file not found: {structure.FilePath}", "ExternalFileRenderer.Render");
                    return document;
                }

                // Load without 'using' - Merge references the source pages until the final save.
                var loadedDoc = new PdfLoadedDocument(structure.FilePath);
                _loadedDocuments.Add(loadedDoc);
                int pageCount = loadedDoc.Pages.Count;

                PdfDocumentBase.Merge(document, loadedDoc);

                AppLogger.Info($"Merged {pageCount} page(s) from external file: {Path.GetFileName(structure.FilePath)}", "ExternalFileRenderer.Render");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ExternalFileRenderer.Render");
            }

            return document;
        }
    }
}
