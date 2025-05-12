using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; //access model-view-controller
using Microsoft.Extensions.Hosting; //Adds IWebHostEnvironment which provides information about the web hosting environment the application is running in


namespace MentalHealthPortal.Endpoints
{
    public static class DocumentUploadEndpoints
    {
        public static void MapDocumentUploadEndpoints(this WebApplication app)
        {
            app.MapPost("/api/documents/upload", async Task<IResult> ([FromForm] IFormFile file, IWebHostEnvironment env) =>
            {
                if (file == null || file.Length == 0)
                {
                    return Results.BadRequest(new { message = "No file uploaded." });
                }

                var originalFileName = file.FileName;
                var fileExtension = Path.GetExtension(originalFileName).ToLowerInvariant();
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}"; //Generates a unique file name

                if (string.IsNullOrEmpty(fileExtension) || (fileExtension != ".pdf" && fileExtension != ".docx"))
                {
                    return Results.BadRequest(new { message = "Invalid file type. Only PDF or DOCX are allowed" });
                }
                try
                {
                    // 1. Define storage path
                    var uploadsFolderPath = Path.Combine(env.ContentRootPath, "UploadedDocuments");

                    // 2. Ensure the directory exists
                    Directory.CreateDirectory(uploadsFolderPath);

                    // 3. Generate the full file path for the new file
                    var filePath = Path.Combine(uploadsFolderPath, uniqueFileName);

                    // 4. Save the file asynchronously
                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // 5. Return seccess with actual stored filename and path
                    return Results.Ok(new
                    {
                        message = $"File '{originalFileName}' uploaded successfully and saved as '{uniqueFileName}'.",
                        storedFileName = uniqueFileName,
                        filePath = filePath
                    });

                }
                catch (IOException ex) // Be more specific with IO exceptions
                {
                    // Log the exception (e.g., using ILogger in a real app)
                    Console.WriteLine($"Error saving file '{originalFileName}': {ex.Message}"); // Log specific error
                    return Results.Problem("An error occurred while saving the file. Please try again.", statusCode: 500);
                }
                catch (Exception ex)
                {
                    // Basic error handling for now. You'll want to log this error.
                    Console.WriteLine($"Error during file processing for '{originalFileName}': {ex.Message}"); // Log specific error
                    return Results.Problem("An unexpected error occurred while processing the file.", statusCode: 500);
                }
                // ---- END of new file saving logic ----

            })
            .DisableAntiforgery(); //For simple forms without antiforgery tokens
        }
    }
}