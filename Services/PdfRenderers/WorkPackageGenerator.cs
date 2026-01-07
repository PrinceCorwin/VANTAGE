using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Syncfusion.Pdf;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Result of PDF generation
    public class GenerationResult
    {
        public bool Success { get; set; }
        public string? MergedPdfPath { get; set; }
        public List<string> IndividualPdfPaths { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    // Orchestrates the generation of Work Package PDFs
    public class WorkPackageGenerator
    {
        private readonly CoverRenderer _coverRenderer = new();
        private readonly ListRenderer _listRenderer = new();
        private readonly FormRenderer _formRenderer = new();
        private readonly GridRenderer _gridRenderer = new();
        private readonly DrawingsRenderer _drawingsRenderer = new();

        // Generate PDFs for a single work package
        public async Task<GenerationResult> GenerateAsync(
            string wpTemplateId,
            TokenContext context,
            string outputFolder,
            bool includeIndividualPdfs,
            string? logoPath = null)
        {
            var result = new GenerationResult();

            try
            {
                // Load the WP template
                var wpTemplate = await TemplateRepository.GetWPTemplateByIdAsync(wpTemplateId);
                if (wpTemplate == null)
                {
                    result.ErrorMessage = $"WP Template not found: {wpTemplateId}";
                    return result;
                }

                // Parse the forms list
                var formRefs = JsonSerializer.Deserialize<List<FormReference>>(wpTemplate.FormsJson);
                if (formRefs == null || formRefs.Count == 0)
                {
                    result.ErrorMessage = "WP Template has no forms configured";
                    return result;
                }

                // Parse settings for expiration days
                var settings = JsonSerializer.Deserialize<WPTemplateSettings>(wpTemplate.DefaultSettings);
                if (settings != null)
                {
                    context.ExpirationDays = settings.ExpirationDays;
                }

                // Create output folder structure
                string wpFolder = Path.Combine(outputFolder, context.ProjectID, SanitizeFileName(context.WorkPackage));
                Directory.CreateDirectory(wpFolder);

                // Generate each form PDF
                var formDocuments = new List<(PdfDocument doc, string name)>();
                int formIndex = 1;

                foreach (var formRef in formRefs)
                {
                    var formTemplate = await TemplateRepository.GetFormTemplateByIdAsync(formRef.FormTemplateId);
                    if (formTemplate == null)
                    {
                        AppLogger.Warning($"Form template not found: {formRef.FormTemplateId}", "WorkPackageGenerator.GenerateAsync");
                        continue;
                    }

                    // Render the form
                    PdfDocument formDoc = RenderForm(formTemplate, context, logoPath);
                    string formName = SanitizeFileName(formTemplate.TemplateName);
                    formDocuments.Add((formDoc, formName));

                    // Save individual PDF if requested
                    if (includeIndividualPdfs)
                    {
                        string individualPath = Path.Combine(wpFolder, $"{formIndex}_{formName}.pdf");
                        using var fileStream = new FileStream(individualPath, FileMode.Create, FileAccess.Write);
                        formDoc.Save(fileStream);
                        result.IndividualPdfPaths.Add(individualPath);
                    }

                    formIndex++;
                }

                if (formDocuments.Count == 0)
                {
                    result.ErrorMessage = "No forms were generated";
                    return result;
                }

                // Merge all documents
                PdfDocument mergedDoc = MergeDocuments(formDocuments);

                // Save merged PDF
                string mergedFileName = $"{SanitizeFileName(context.WorkPackage)}-WorkPackage.pdf";
                string mergedPath = Path.Combine(wpFolder, mergedFileName);
                using (var mergedStream = new FileStream(mergedPath, FileMode.Create, FileAccess.Write))
                {
                    mergedDoc.Save(mergedStream);
                }

                // Close all documents
                foreach (var (doc, _) in formDocuments)
                {
                    doc.Close(true);
                }
                mergedDoc.Close(true);

                result.Success = true;
                result.MergedPdfPath = mergedPath;

                AppLogger.Info($"Generated Work Package PDF: {mergedPath}", "WorkPackageGenerator.GenerateAsync", App.CurrentUser?.Username);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                AppLogger.Error(ex, "WorkPackageGenerator.GenerateAsync");
            }

            return result;
        }

        // Generate PDFs for multiple work packages (bulk)
        public async Task<List<GenerationResult>> GenerateBulkAsync(
            string wpTemplateId,
            string projectId,
            List<string> workPackages,
            string pkgManagerUsername,
            string pkgManagerFullName,
            string schedulerUsername,
            string schedulerFullName,
            string wpNamePattern,
            string outputFolder,
            bool includeIndividualPdfs,
            string? logoPath = null)
        {
            var results = new List<GenerationResult>();

            foreach (var workPackage in workPackages)
            {
                var context = new TokenContext
                {
                    ProjectID = projectId,
                    WorkPackage = workPackage,
                    PKGManagerUsername = pkgManagerUsername,
                    PKGManagerFullName = pkgManagerFullName,
                    SchedulerUsername = schedulerUsername,
                    SchedulerFullName = schedulerFullName,
                    WPNamePattern = wpNamePattern
                };

                var result = await GenerateAsync(wpTemplateId, context, outputFolder, includeIndividualPdfs, logoPath);
                results.Add(result);
            }

            return results;
        }

        // Render a form template to PDF
        private PdfDocument RenderForm(FormTemplate template, TokenContext context, string? logoPath)
        {
            return template.TemplateType switch
            {
                TemplateTypes.Cover => _coverRenderer.Render(template.StructureJson, context, logoPath),
                TemplateTypes.List => _listRenderer.Render(template.StructureJson, context, logoPath),
                TemplateTypes.Form => _formRenderer.Render(template.StructureJson, context, logoPath),
                TemplateTypes.Grid => _gridRenderer.Render(template.StructureJson, context, logoPath),
                TemplateTypes.Drawings => _drawingsRenderer.Render(template.StructureJson, context, logoPath),
                _ => throw new InvalidOperationException($"Unknown template type: {template.TemplateType}")
            };
        }

        // Merge multiple PDF documents into one
        private PdfDocument MergeDocuments(List<(PdfDocument doc, string name)> documents)
        {
            var mergedDoc = new PdfDocument();

            // Set page size to match our standard letter size (8.5 x 11 inches)
            mergedDoc.PageSettings.Size = new System.Drawing.SizeF(612f, 792f);
            mergedDoc.PageSettings.Margins.All = 0;

            foreach (var (doc, _) in documents)
            {
                // Import all pages from each document
                for (int i = 0; i < doc.Pages.Count; i++)
                {
                    var page = doc.Pages[i];
                    var importedPage = mergedDoc.Pages.Add();

                    // Copy page content using template
                    var template = page.CreateTemplate();
                    importedPage.Graphics.DrawPdfTemplate(template, System.Drawing.PointF.Empty);
                }
            }

            return mergedDoc;
        }

        // Generate preview PDF for a WP template (with placeholder data)
        public async Task<MemoryStream?> GeneratePreviewAsync(string wpTemplateId, string? logoPath = null)
        {
            var context = TokenResolver.GetPlaceholderContext();
            return await GeneratePreviewAsync(wpTemplateId, context, logoPath);
        }

        // Generate preview PDF for a WP template with provided context
        public async Task<MemoryStream?> GeneratePreviewAsync(string wpTemplateId, TokenContext context, string? logoPath = null)
        {
            try
            {
                var tempFolder = Path.GetTempPath();

                var result = await GenerateAsync(wpTemplateId, context, tempFolder, false, logoPath);
                if (!result.Success || string.IsNullOrEmpty(result.MergedPdfPath))
                {
                    return null;
                }

                // Read the generated file into memory stream
                var memStream = new MemoryStream();
                using (var fileStream = File.OpenRead(result.MergedPdfPath))
                {
                    await fileStream.CopyToAsync(memStream);
                }
                memStream.Position = 0;

                // Clean up temp file
                try { File.Delete(result.MergedPdfPath); } catch { }

                return memStream;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageGenerator.GeneratePreviewAsync");
                return null;
            }
        }

        // Generate preview PDF for a single form template (with placeholder data)
        public MemoryStream? GenerateFormPreview(FormTemplate template, string? logoPath = null)
        {
            var context = TokenResolver.GetPlaceholderContext();
            return GenerateFormPreview(template, context, logoPath);
        }

        // Generate preview PDF for a single form template with provided context
        public MemoryStream? GenerateFormPreview(FormTemplate template, TokenContext context, string? logoPath = null)
        {
            try
            {
                var doc = RenderForm(template, context, logoPath);

                var memStream = new MemoryStream();
                doc.Save(memStream);
                doc.Close(true);
                memStream.Position = 0;

                return memStream;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageGenerator.GenerateFormPreview");
                return null;
            }
        }

        // Sanitize filename by removing invalid characters
        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim();
        }
    }
}
