using MentalHealthPortal.Endpoints;
using Microsoft.AspNetCore.Mvc; // Add this for [FromQuery]
using MentalHealthPortal.Services; // Add this for IndexService
using MentalHealthPortal.Models; // Add this for SearchResultItem

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// For Minimal APIs, you might not need to add many services here initially.
// If you were using Razor Pages or MVC, you'd add them here, e.g.:
// builder.Services.AddRazorPages();
// builder.Services.AddControllersWithViews();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<MentalHealthPortal.Services.TextExtractionService>(); // Register the TextExtractionService

builder.Services.AddSingleton<MentalHealthPortal.Services.IBackgroundTaskQueue, MentalHealthPortal.Services.BackgroundTaskQueue>();

builder.Services.AddHostedService<MentalHealthPortal.Services.QueuedHostedService>();

builder.Services.AddSingleton<MentalHealthPortal.Services.IndexService>();



var app = builder.Build();

app.MapGet("/api/search", ([FromQuery] string keywords, [FromQuery] string? docTypeFilter, IndexService indexService) =>
{
    if (string.IsNullOrWhiteSpace(keywords))
    {
        return Results.BadRequest("Search term cannot be empty.");
    }

    var searchResults = indexService.Search(keywords, docTypeFilter);
    return Results.Ok(searchResults);
})
.WithName("SearchDocuments")
.WithTags("Search")
.Produces<List<MentalHealthPortal.Models.SearchResultItem>>(StatusCodes.Status200OK) // Added MentalHealthPortal.Models namespace
.Produces(StatusCodes.Status400BadRequest);



if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error"); //Redirect to error page
    app.UseHsts(); //Adds HTTP Strict Transport Security Protocol Header
}

// app.UseHttpsRedirection(); // Commented out for now to resolve port warning in local dev
app.UseDefaultFiles(); // Serve default files like index.html from wwwroot
app.UseStaticFiles(); //Serve static files from wwwroot (indes.html, css, js)
app.UseRouting(); //Enables Routing

// If you add authentication/authorization later, they would go here:
// app.UseAuthentication();
// app.UseAuthorization();

app.MapDocumentUploadEndpoints(); //Map custom document upload endpoints
app.Run();
