using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MiniPdf.DocxConverter.Api.Common.Constants;
using MiniPdf.DocxConverter.Api.Common.Validation;
using MiniPdf.DocxConverter.Api.Configuration;
using MiniPdf.DocxConverter.Api.Contracts.Requests;
using MiniPdf.DocxConverter.Api.Contracts.Responses;
using MiniPdf.DocxConverter.Api.Services.Abstractions;
using MiniSoftware;

namespace MiniPdf.DocxConverter.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class ConverterController(
    IDocxToPdfConverter converter,
    IFontManagerService fontManager,
    IOptions<ConversionSettings> settings,
    ILogger<ConverterController> logger) : ControllerBase
{
    private readonly ConversionSettings _settings = settings.Value;

    /// <summary>
    /// Converts an uploaded DOCX file to a PDF document.
    /// </summary>
    /// <param name="request">Multipart form containing the DOCX file and optional formatting options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated PDF document as a file stream.</returns>
    [HttpPost("docx-to-pdf")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConvertDocxToPdf(
        [FromForm] ConvertDocxRequest request,
        CancellationToken cancellationToken)
    {
        await FileValidator.ValidateDocxFileAsync(request.File, _settings.MaxFileSizeBytes, cancellationToken);

        var originalFileName = Path.GetFileNameWithoutExtension(request.File.FileName);
        var outputPdfFileName = $"{originalFileName}.pdf";

        var options = new MiniPdfConversionOptions
        {
            Compress = request.Compress ?? _settings.DefaultFlateCompression
        };

        if (string.Equals(request.PageSize, "Letter", StringComparison.OrdinalIgnoreCase))
        {
            options.PageSize = MiniPdfPageSize.Letter;
        }
        else if (string.Equals(request.PageSize, "A4", StringComparison.OrdinalIgnoreCase))
        {
            options.PageSize = MiniPdfPageSize.A4;
        }

        logger.LogInformation(
            "Starting conversion for file: '{FileName}' (Size: {Size} bytes)",
            request.File.FileName,
            request.File.Length);

        await using var uploadStream = request.File.OpenReadStream();
        var pdfBytes = await converter.ConvertAsync(uploadStream, options, cancellationToken);

        return File(pdfBytes, FileConstants.PdfMimeType, outputPdfFileName);
    }

    /// <summary>
    /// Returns service health, registered fonts, and concurrency metrics.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        var registeredFonts = fontManager.GetRegisteredFonts();

        var response = new HealthStatusResponse(
            Status: "Healthy",
            Engine: $"{_settings.Engine} Engine (High-Fidelity Document Rendering)",
            RegisteredFontsCount: registeredFonts.Count,
            RegisteredFonts: registeredFonts,
            AvailableConcurrencySlots: converter.AvailableConcurrencySlots,
            TimestampUtc: DateTime.UtcNow
        );

        return Ok(response);
    }
}
