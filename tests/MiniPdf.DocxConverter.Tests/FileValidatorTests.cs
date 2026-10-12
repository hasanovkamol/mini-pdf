using Microsoft.AspNetCore.Http;
using MiniPdf.DocxConverter.Api.Common.Exceptions;
using MiniPdf.DocxConverter.Api.Common.Validation;

namespace MiniPdf.DocxConverter.Tests;

public class FileValidatorTests
{
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB

    [Fact]
    public async Task ValidateDocxFileAsync_NullOrEmptyFile_ThrowsInvalidFileException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidFileException>(() =>
            FileValidator.ValidateDocxFileAsync(null, MaxSizeBytes));

        var emptyStream = new MemoryStream();
        var emptyFile = new FormFile(emptyStream, 0, 0, "file", "test.docx");

        await Assert.ThrowsAsync<InvalidFileException>(() =>
            FileValidator.ValidateDocxFileAsync(emptyFile, MaxSizeBytes));
    }

    [Fact]
    public async Task ValidateDocxFileAsync_ExceedsMaxSize_ThrowsInvalidFileException()
    {
        // Arrange
        var content = new byte[100];
        var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, content.Length, "file", "test.docx");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidFileException>(() =>
            FileValidator.ValidateDocxFileAsync(file, maxFileSizeBytes: 50));
    }

    [Fact]
    public async Task ValidateDocxFileAsync_NonDocxExtension_ThrowsInvalidFileException()
    {
        // Arrange
        var content = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, content.Length, "file", "document.pdf");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidFileException>(() =>
            FileValidator.ValidateDocxFileAsync(file, MaxSizeBytes));

        Assert.Contains("Only '.docx' files are supported", ex.Message);
    }

    [Fact]
    public async Task ValidateDocxFileAsync_InvalidMagicBytes_ThrowsInvalidFileException()
    {
        // Arrange (fake file with .docx extension but not ZIP bytes)
        var content = "This is not a zip archive"u8.ToArray();
        var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, content.Length, "file", "document.docx");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidFileException>(() =>
            FileValidator.ValidateDocxFileAsync(file, MaxSizeBytes));

        Assert.Contains("not a valid OpenXML DOCX archive", ex.Message);
    }

    [Fact]
    public async Task ValidateDocxFileAsync_ValidMagicBytesAndExtension_PassesValidation()
    {
        // Arrange
        var content = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };
        var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, content.Length, "file", "document.docx");

        // Act & Assert (Should not throw)
        var exception = await Record.ExceptionAsync(() =>
            FileValidator.ValidateDocxFileAsync(file, MaxSizeBytes));

        Assert.Null(exception);
    }
}
