namespace MiniPdf.DocxConverter.Api.Contracts.Requests;

public sealed class ConvertDocxRequest
{
    public required IFormFile File { get; init; }

    public bool? Compress { get; init; }

    public string? PageSize { get; init; }
}
