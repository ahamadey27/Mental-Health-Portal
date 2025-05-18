using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; // Added for [FromForm]
using MentalHealthPortal.Models;
using MentalHealthPortal.Services;
using Microsoft.Extensions.Logging; 

namespace MentalHealthPortal.Endpoints
{
    public static class DocumentUploadEndpoints
    {
        
        public static void MapDocumentUploadEndpoints(this WebApplication app)
        {
            // Changed ILogger<DocumentUploadEndpoints> to ILogger<Program> 
            // app.MapPost("/api/documents/upload", async Task<IResult> ([FromForm] IFormFile file, TextExtractionService textExtractionService, IndexService indexService, ILogger<Program> logger) =>
            app.MapPost("/api/documents/upload", async Task<IResult> ([FromForm] IFormFileCollection files, TextExtractionService textExtractionService, IndexService indexService, ILogger<Program> logger) =>
            {
                // if (file == null || file.Length == 0)
                if (files == null || files.Count == 0)
                {
                    logger.LogWarning("Upload attempt with no files.");
                    return Results.BadRequest(new { message = "No files uploaded." });
                }

                var successfulUploads = new List<string>();
                var errorMessages = new List<string>();

                foreach (var file in files)
                {
                    if (file == null || file.Length == 0)
                    {
                        logger.LogWarning("Skipping an empty file part in multi-file upload.");
                        errorMessages.Add("An empty file part was skipped.");
                        continue;
                    }

                    var originalFileName = file.FileName;
                    var fileExtension = Path.GetExtension(originalFileName).ToLowerInvariant();

                    logger.LogInformation("Processing file: {OriginalFileName}, Extension: {FileExtension}", originalFileName, fileExtension);

                    if (string.IsNullOrEmpty(fileExtension) || (fileExtension != ".pdf" && fileExtension != ".docx"))
                    {
                        logger.LogWarning("Invalid file type: {OriginalFileName} ({FileExtension})", originalFileName, fileExtension);
                        errorMessages.Add($"Invalid file type: {originalFileName} ({fileExtension}). Only PDF or DOCX are allowed.");
                        continue; 
                    }
                    try
                    {
                        string extractedText;
                        using (var memoryStream = new MemoryStream())
                        {
                            await file.CopyToAsync(memoryStream);
                            extractedText = await textExtractionService.ExtractTextAsync(memoryStream, fileExtension.TrimStart('.').ToUpperInvariant(), originalFileName);
                        }
                        
                        logger.LogInformation("Text extracted for {OriginalFileName}. Length: {Length}", originalFileName, extractedText.Length);

                        var metadata = new DocumentMetadata
                        {
                            Id = Guid.NewGuid().ToString(),
                            OriginalFileName = originalFileName,
                            DocumentType = fileExtension.TrimStart('.').ToUpperInvariant(),
                            UploadTimestamp = DateTime.UtcNow
                        };

                        indexService.AddOrUpdateDocument(metadata, extractedText);
                        logger.LogInformation("Document {OriginalFileName} (ID: {DocumentId}) submitted to IndexService.", metadata.OriginalFileName, metadata.Id);
                        successfulUploads.Add(originalFileName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error during document upload and processing for file {OriginalFileName}.", originalFileName);
                        errorMessages.Add($"Error processing {originalFileName}: {ex.Message}");
                    }
                }

                if (successfulUploads.Count == 0 && errorMessages.Count == 0)
                {
                    // This case should ideally not be hit if initial check for files.Count > 0 passes
                    return Results.BadRequest(new { message = "No files were processed." });
                }

                return Results.Ok(new { 
                    message = "File processing complete.", 
                    successfulUploads = successfulUploads,
                    errors = errorMessages 
                });
            })
            .DisableAntiforgery(); // Consider if antiforgery is needed for your specific auth setup
        }
    }
}