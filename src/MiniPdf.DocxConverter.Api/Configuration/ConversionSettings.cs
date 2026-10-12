namespace MiniPdf.DocxConverter.Api.Configuration;

public sealed class ConversionSettings
{
    public const string SectionName = "ConversionSettings";

    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024; // 25 MB default

    public int MaxConcurrentConversions { get; set; } = 4;

    public string Engine { get; set; } = "LibreOffice"; // "LibreOffice" or "MiniPdf"

    public string? LibreOfficePath { get; set; }

    public string? CustomFontsDirectory { get; set; }

    public bool DefaultFlateCompression { get; set; } = true;
}
