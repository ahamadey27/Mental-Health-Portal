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
            app.MapPost("/api/documents/upload", async Task<IResult> ([FromForm] IFormFile file, TextExtractionService textExtractionService, IndexService indexService, ILogger<Program> logger) =>
            {
                if (file == null || file.Length == 0)
                {
                    logger.LogWarning("Upload attempt with no file.");
                    return Results.BadRequest(new { message = "No file uploaded." });
                }

                var originalFileName = file.FileName;
                var fileExtension = Path.GetExtension(originalFileName).ToLowerInvariant();

                logger.LogInformation("Upload request received for file: {OriginalFileName}, Extension: {FileExtension}", originalFileName, fileExtension);

                if (string.IsNullOrEmpty(fileExtension) || (fileExtension != ".pdf" && fileExtension != ".docx"))
                {
                    logger.LogWarning("Invalid file type uploaded: {FileExtension}", fileExtension);
                    return Results.BadRequest(new { message = "Invalid file type. Only PDF or DOCX are allowed" });
                }
                try
                {
                    string extractedText;
                    using (var memoryStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memoryStream);
                        // TextExtractionService now handles stream position internally if needed
                        extractedText = await textExtractionService.ExtractTextAsync(memoryStream, fileExtension.TrimStart('.').ToUpperInvariant(), originalFileName);
                    }
                    
                    logger.LogInformation("Text extracted for {OriginalFileName}. Length: {Length}", originalFileName, extractedText.Length);

                    // Ensure DocumentMetadata is initialized correctly with string Id
                    var metadata = new DocumentMetadata
                    {
                        Id = Guid.NewGuid().ToString(), // Id is string
                        OriginalFileName = originalFileName,
                        DocumentType = fileExtension.TrimStart('.').ToUpperInvariant(),
                        UploadTimestamp = DateTime.UtcNow
                    };

                    indexService.AddOrUpdateDocument(metadata, extractedText);
                    logger.LogInformation("Document {OriginalFileName} (ID: {DocumentId}) submitted to IndexService.", metadata.OriginalFileName, metadata.Id);
                    
                    return Results.Ok(new { 
                        message = "File uploaded and processed successfully.", 
                        documentId = metadata.Id, 
                        fileName = metadata.OriginalFileName 
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during document upload and processing for file {OriginalFileName}.", originalFileName);
                    return Results.Problem("An error occurred during file processing.");
                }
            })
            .DisableAntiforgery(); // Consider if antiforgery is needed for your specific auth setup
        }
    }
}