using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using MiniPdf.DocxConverter.Api.Contracts.Responses;

namespace MiniPdf.DocxConverter.Tests;

public class ConverterControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ConverterControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk_WithHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/converter/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();
        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.False(string.IsNullOrWhiteSpace(health.Engine));
    }

    [Fact]
    public async Task ConvertDocxToPdf_InvalidFileFormat_ReturnsBadRequest()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("Fake not a zip content"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "File", "fake.docx");

        // Act
        var response = await _client.PostAsync("/api/v1/converter/docx-to-pdf", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConvertDocxToPdf_ValidDocx_ReturnsPdfStream()
    {
        // Arrange
        var docxBytes = CreateDocxFileBytes("Assalomu alaykum! .NET 10 Web API konvertatsiya testi.");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(docxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "File", "report.docx");

        // Act
        var response = await _client.PostAsync("/api/v1/converter/docx-to-pdf", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var resultBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(resultBytes);
        var pdfSignature = Encoding.ASCII.GetString(resultBytes.AsSpan(0, Math.Min(resultBytes.Length, 8)));
        Assert.StartsWith("%PDF-", pdfSignature);
    }

    private static byte[] CreateDocxFileBytes(string textContent)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
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

        return memoryStream.ToArray();
    }
}
