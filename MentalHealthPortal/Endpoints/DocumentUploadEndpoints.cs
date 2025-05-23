using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; // Added for [FromForm]
using MentalHealthPortal.Models;
using MentalHealthPortal.Services;
using Microsoft.Extensions.Logging; 
using Microsoft.AspNetCore.Hosting; // Required for IWebHostEnvironment

namespace MentalHealthPortal.Endpoints
{
    public static class DocumentUploadEndpoints
    {
        
        public static void MapDocumentUploadEndpoints(this WebApplication app)
        {
            var sessionUploadsPath = Path.Combine(app.Environment.WebRootPath, "session_uploads");
            if (!Directory.Exists(sessionUploadsPath))
            {
                Directory.CreateDirectory(sessionUploadsPath);
            }

            app.MapPost("/api/documents/upload", async Task<IResult> ([FromForm] IFormFileCollection files, TextExtractionService textExtractionService, IndexService indexService, ILogger<Program> logger, IWebHostEnvironment env) =>
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
                        // Generate a unique name for storage to avoid conflicts
                        var uniqueStoredFileName = $"{Guid.NewGuid()}_{originalFileName}";
                        var filePathToSave = Path.Combine(sessionUploadsPath, uniqueStoredFileName);

                        using (var fileStream = new FileStream(filePathToSave, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }
                        
                        // Now use the saved file for extraction to ensure consistency, or use a memory stream if preferred
                        // For this example, let's re-open the saved file for extraction.
                        // Alternatively, could do memoryStream first, then save memoryStream to disk.
                        using (var streamForExtraction = new FileStream(filePathToSave, FileMode.Open, FileAccess.Read))
                        {
                             extractedText = await textExtractionService.ExtractTextAsync(streamForExtraction, fileExtension.TrimStart('.').ToUpperInvariant(), originalFileName);
                        }
                        
                        logger.LogInformation("Text extracted for {OriginalFileName}. Length: {Length}", originalFileName, extractedText.Length);

                        var metadata = new DocumentMetadata
                        {
                            Id = Guid.NewGuid().ToString(),
                            OriginalFileName = originalFileName,
                            DocumentType = fileExtension.TrimStart('.').ToUpperInvariant(),
                            UploadTimestamp = DateTime.UtcNow,
                            StoredFileName = uniqueStoredFileName // Store the unique name used for saving
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