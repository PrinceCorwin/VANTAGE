using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Syncfusion.Pdf;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Services.ProgressBook;
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
        private readonly ExternalFileRenderer _externalFileRenderer = new();

        // Generate PDFs for a single work package.
        // noSubfolders=true drops every PDF directly into the project's "Work Pkgs"
        // folder (no per-WP subfolder). Mutually exclusive with includeIndividualPdfs
        // at the UI level.
        public async Task<GenerationResult> GenerateAsync(
            string wpTemplateId,
            TokenContext context,
            string outputFolder,
            bool includeIndividualPdfs,
            string? logoPath = null,
            bool noSubfolders = false)
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

                // Set output folder in context for renderers that need it (e.g., DrawingsRenderer)
                context.OutputFolder = outputFolder;

                // Create output folder structure.
                // Default: {outputFolder}/{ProjectID} - Work Pkgs/{WorkPackage}/
                // No Subfolders: {outputFolder}/{ProjectID} - Work Pkgs/  (all PDFs flat)
                string projectFolderName = $"{SanitizeFileName(context.ProjectID)} - Work Pkgs";
                string sanitizedWP = SanitizeFileName(context.WorkPackage);
                string wpFolder = noSubfolders
                    ? Path.Combine(outputFolder, projectFolderName)
                    : Path.Combine(outputFolder, projectFolderName, sanitizedWP);
                Directory.CreateDirectory(wpFolder);

                // Generate each form PDF
                var formDocuments = new List<(PdfDocument doc, string name)>();
                int formIndex = 1;

                foreach (var formRef in formRefs)
                {
                    PdfDocument formDoc;
                    string formName;

                    if (formRef.ProgressBookLayoutId.HasValue)
                    {
                        // Embedded Progress Book: generate the book scoped to this work package.
                        var bookDoc = await GenerateProgressBookFormAsync(formRef.ProgressBookLayoutId.Value, context);
                        if (bookDoc == null)
                            continue; // missing layout or no matching activities
                        formDoc = bookDoc;
                        formName = SanitizeFileName("ProgressBook");
                    }
                    else
                    {
                        var formTemplate = await TemplateRepository.GetFormTemplateByIdAsync(formRef.FormTemplateId);
                        if (formTemplate == null)
                        {
                            AppLogger.Warning($"Form template not found: {formRef.FormTemplateId}", "WorkPackageGenerator.GenerateAsync");
                            continue;
                        }

                        // Render the form
                        formDoc = RenderForm(formTemplate, context, logoPath);

                        // Skip forms that produced no pages (e.g. an external-file form whose PDF is
                        // missing). The user was already warned about missing files before generation.
                        if (formDoc.Pages.Count == 0)
                        {
                            formDoc.Close(true);
                            continue;
                        }

                        formName = SanitizeFileName(formTemplate.TemplateName);
                    }

                    formDocuments.Add((formDoc, formName));

                    // Save individual PDF if requested. Prefixed with WorkPackage so multiple
                    // WPs writing to the same flat folder don't overwrite each other's files.
                    if (includeIndividualPdfs)
                    {
                        string individualPath = Path.Combine(wpFolder, $"{sanitizedWP}-{formIndex}_{formName}.pdf");
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
                string mergedFileName = $"{sanitizedWP}-WorkPackage.pdf";
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

                // Clear any loaded PDFs kept alive during merge (drawings + external files)
                _drawingsRenderer.ClearLoadedDocuments();
                _externalFileRenderer.ClearLoadedDocuments();

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
            string? logoPath = null,
            bool noSubfolders = false)
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

                var result = await GenerateAsync(wpTemplateId, context, outputFolder, includeIndividualPdfs, logoPath, noSubfolders);
                results.Add(result);
            }

            return results;
        }

        // Generate an embedded Progress Book PDF for a saved layout, scoped to the work package
        // being generated. Returns null if the layout is missing or nothing matches (caller skips
        // it). The layout's own filter is overridden to this work package; every other setting
        // (columns, groups, sorts, paper size, exclude-completed, assignee scope) is honored.
        private static async Task<PdfDocument?> GenerateProgressBookFormAsync(int layoutId, TokenContext context)
        {
            try
            {
                var layout = await Services.ProgressBook.ProgressBookLayoutRepository.GetByIdAsync(layoutId);
                if (layout == null)
                {
                    AppLogger.Warning($"Progress Book layout not found: {layoutId}", "WorkPackageGenerator.GenerateProgressBookFormAsync");
                    return null;
                }

                var config = layout.GetConfiguration();
                config.FilterField = "WorkPackage";
                config.FilterValue = context.WorkPackage;

                var username = App.CurrentUser?.Username ?? "";
                return await ProgressBookGenerationService.GenerateAsync(config, context.ProjectID, context.WorkPackage, username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageGenerator.GenerateProgressBookFormAsync");
                return null;
            }
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
                TemplateTypes.ExternalFile => _externalFileRenderer.Render(template.StructureJson, context, logoPath),
                _ => throw new InvalidOperationException($"Unknown template type: {template.TemplateType}")
            };
        }

        // Merge the form documents into one, in list order. Each source page is copied onto a
        // new per-page section sized to that page, so pages keep their original size (letter forms
        // stay letter; an 11x17 drawing stays 11x17) and the forms stay in list order.
        private PdfDocument MergeDocuments(List<(PdfDocument doc, string name)> documents)
        {
            var mergedDoc = new PdfDocument();
            mergedDoc.PageSettings.Margins.All = 0;

            foreach (var (doc, _) in documents)
            {
                for (int i = 0; i < doc.Pages.Count; i++)
                {
                    var page = doc.Pages[i];

                    var section = mergedDoc.Sections.Add();
                    section.PageSettings.Margins.All = 0;
                    section.PageSettings.Size = page.Size;
                    var importedPage = section.Pages.Add();

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

                // Clear any loaded drawing PDFs (if this was a Drawings template)
                _drawingsRenderer.ClearLoadedDocuments();

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
