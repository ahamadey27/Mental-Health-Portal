using MentalHealthPortal.Endpoints;
using MentalHealthPortal.Data; // Add this using directive
using Microsoft.EntityFrameworkCore; // Add this using directive

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// For Minimal APIs, you might not need to add many services here initially.
// If you were using Razor Pages or MVC, you'd add them here, e.g.:
// builder.Services.AddRazorPages();
// builder.Services.AddControllersWithViews();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure EF Core to use SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=mentalhealthportal.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<MentalHealthPortal.Services.TextExtractionService>(); // Register the TextExtractionService

var app = builder.Build();



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
