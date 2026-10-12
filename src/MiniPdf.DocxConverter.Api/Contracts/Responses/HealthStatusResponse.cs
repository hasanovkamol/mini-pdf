namespace MiniPdf.DocxConverter.Api.Contracts.Responses;

public sealed record HealthStatusResponse(
    string Status,
    string Engine,
    int RegisteredFontsCount,
    IReadOnlyList<string> RegisteredFonts,
    int AvailableConcurrencySlots,
    DateTime TimestampUtc
);
