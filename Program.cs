using Microsoft.EntityFrameworkCore;
using ProductRecommender.Backend.Models.Core;
using ProductRecommender.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<MLService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        builder =>
        {
            builder.WithOrigins(
                "http://localhost:4200",
                "https://recomendador.upgrade.com.pe"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});

builder.Services.AddDbContext<UpgradedbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
// TEMPORARY: Enable detailed errors in Prod to debug startup issues
// Enable detailed errors in Prod to debug startup issues
app.UseDeveloperExceptionPage();

// Enable Swagger always (for Prod testing)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vende+ API V1");
    // Optional: Set Swagger as the home page by uncommenting below
    // c.RoutePrefix = string.Empty; 
});

app.UseHttpsRedirection();

// 1. Enable Static Files (for Angular)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowAngular");
app.MapControllers();

// 2. Fallback to Angular's index.html for non-API routes
app.MapFallbackToFile("index.html");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
