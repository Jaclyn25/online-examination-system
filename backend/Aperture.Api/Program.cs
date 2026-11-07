var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapOpenApi();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", product = "Aperture Exams" }));

app.Run();

public partial class Program;
