using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MiniPdf.DocxConverter.Api.Common.Exceptions;

namespace MiniPdf.DocxConverter.Api.Common.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, title, detail) = exception switch
        {
            InvalidFileException invalidEx => (
                StatusCodes.Status400BadRequest,
                "Invalid File Request",
                invalidEx.Message
            ),
            ConversionException conversionEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Document Conversion Error",
                conversionEx.Message
            ),
            OperationCanceledException => (
                StatusCodes.Status408RequestTimeout,
                "Request Cancelled",
                "The conversion request was cancelled by the client or timed out."
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing the document."
            )
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
