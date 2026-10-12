namespace MiniPdf.DocxConverter.Api.Common.Constants;

public static class FileConstants
{
    public const string DocxExtension = ".docx";
    public const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string PdfMimeType = "application/pdf";

    // Standard DOCX is a ZIP archive, starting with PK (0x50, 0x4B)
    public static readonly byte[] ZipMagicBytes = [0x50, 0x4B, 0x03, 0x04];
    public static readonly byte[] EmptyZipMagicBytes = [0x50, 0x4B, 0x05, 0x06];
    public static readonly byte[] SpannedZipMagicBytes = [0x50, 0x4B, 0x07, 0x08];

    public const long DefaultMaxFileSize = 25 * 1024 * 1024; // 25 MB
}
