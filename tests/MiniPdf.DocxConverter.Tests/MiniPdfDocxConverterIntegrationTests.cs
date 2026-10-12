using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniPdf.DocxConverter.Api.Configuration;
using MiniPdf.DocxConverter.Api.Services.Implementations;

namespace MiniPdf.DocxConverter.Tests;

public class MiniPdfDocxConverterIntegrationTests
{
    [Fact]
    public async Task ConvertAsync_ValidDocxStream_ProducesValidPdfBytes()
    {
        // Arrange
        var settings = Options.Create(new ConversionSettings
        {
            MaxConcurrentConversions = 2,
            DefaultFlateCompression = true
        });

        using var converter = new MiniPdfDocxConverter(settings, NullLogger<MiniPdfDocxConverter>.Instance);
        using var docxStream = CreateMinimalDocxStream("Salom Dunyo! .NET 10 and MiniPdf Docx to Pdf Converter Test.");

        // Act
        var pdfBytes = await converter.ConvertAsync(docxStream);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);

        // Check PDF header signature: %PDF-1.
        var header = Encoding.ASCII.GetString(pdfBytes.AsSpan(0, Math.Min(pdfBytes.Length, 8)));
        Assert.StartsWith("%PDF-", header);
    }

    private static MemoryStream CreateMinimalDocxStream(string textContent)
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. [Content_Types].xml
            var contentTypesEntry = archive.CreateEntry("[Content_Types].xml");
            using (var writer = new StreamWriter(contentTypesEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """);
            }

            // 2. _rels/.rels
            var relsEntry = archive.CreateEntry("_rels/.rels");
            using (var writer = new StreamWriter(relsEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);
            }

            // 3. word/document.xml
            var docEntry = archive.CreateEntry("word/document.xml");
            using (var writer = new StreamWriter(docEntry.Open(), Encoding.UTF8))
            {
                writer.Write($"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                    <w:p>
                      <w:r>
                        <w:t>{textContent}</w:t>
                      </w:r>
                    </w:p>
                  </w:body>
                </w:document>
                """);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }
}
