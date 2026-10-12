using MiniPdf.DocxConverter.Api.Common.Constants;
using MiniPdf.DocxConverter.Api.Common.Exceptions;

namespace MiniPdf.DocxConverter.Api.Common.Validation;

public static class FileValidator
{
    public static async Task ValidateDocxFileAsync(
        IFormFile? file,
        long maxFileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidFileException("The uploaded file is empty or missing.");
        }

        if (file.Length > maxFileSizeBytes)
        {
            var maxMb = maxFileSizeBytes / (1024 * 1024);
            throw new InvalidFileException($"The uploaded file exceeds the maximum allowed size of {maxMb} MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, FileConstants.DocxExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidFileException($"Invalid file extension '{extension}'. Only '{FileConstants.DocxExtension}' files are supported.");
        }

        // Validate magic bytes (DOCX must be a valid zip archive)
        await using var stream = file.OpenReadStream();
        var buffer = new byte[4];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 4), cancellationToken);

        if (bytesRead < 4 || !IsZipHeader(buffer))
        {
            throw new InvalidFileException("The uploaded file is not a valid OpenXML DOCX archive (corrupted or unrecognized header).");
        }
    }

    private static bool IsZipHeader(ReadOnlySpan<byte> buffer)
    {
        return buffer.SequenceEqual(FileConstants.ZipMagicBytes)
            || buffer.SequenceEqual(FileConstants.EmptyZipMagicBytes)
            || buffer.SequenceEqual(FileConstants.SpannedZipMagicBytes);
    }
}
