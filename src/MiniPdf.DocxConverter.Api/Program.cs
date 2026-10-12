using MiniPdf.DocxConverter.Api.Common.Middlewares;
using MiniPdf.DocxConverter.Api.Configuration;
using MiniPdf.DocxConverter.Api.Services.Abstractions;
using MiniPdf.DocxConverter.Api.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// Configuration options
builder.Services.Configure<ConversionSettings>(
    builder.Configuration.GetSection(ConversionSettings.SectionName));

// Services DI
builder.Services.AddSingleton<IFontManagerService, FontManagerService>();

var conversionSection = builder.Configuration.GetSection(ConversionSettings.SectionName);
var engine = conversionSection.GetValue<string>("Engine") ?? "LibreOffice";

if (string.Equals(engine, "MiniPdf", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocxToPdfConverter, MiniPdfDocxConverter>();
}
else
{
    builder.Services.AddSingleton<IDocxToPdfConverter, LibreOfficeDocxConverter>();
}

// Exception Handling & ProblemDetails
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CORS for cross-origin integration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Initialize and register fonts at startup
using (var scope = app.Services.CreateScope())
{
    var fontManager = scope.ServiceProvider.GetRequiredService<IFontManagerService>();
    fontManager.InitializeFonts();
}

app.Run();

public partial class Program { }
