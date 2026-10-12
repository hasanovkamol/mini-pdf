using System.Diagnostics;
using Microsoft.Extensions.Options;
using MiniPdf.DocxConverter.Api.Common.Exceptions;
using MiniPdf.DocxConverter.Api.Configuration;
using MiniPdf.DocxConverter.Api.Services.Abstractions;
using MiniSoftware;

namespace MiniPdf.DocxConverter.Api.Services.Implementations;

public sealed class MiniPdfDocxConverter : IDocxToPdfConverter, IDisposable
{
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly ILogger<MiniPdfDocxConverter> _logger;
    private readonly ConversionSettings _settings;
    private bool _disposed;

    public int AvailableConcurrencySlots => _concurrencySemaphore.CurrentCount;

    public MiniPdfDocxConverter(
        IOptions<ConversionSettings> settings,
        ILogger<MiniPdfDocxConverter> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        var maxSlots = Math.Max(1, _settings.MaxConcurrentConversions);
        _concurrencySemaphore = new SemaphoreSlim(maxSlots, maxSlots);
    }

    public async Task<byte[]> ConvertAsync(
        Stream docxStream,
        MiniPdfConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(docxStream);

        await _concurrencySemaphore.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var tempInputFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");

        try
        {
            await using (var fileStream = new FileStream(
                tempInputFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await docxStream.CopyToAsync(fileStream, cancellationToken);
            }

            options ??= new MiniPdfConversionOptions
            {
                Compress = _settings.DefaultFlateCompression
            };

            var pdfBytes = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return MiniSoftware.MiniPdf.ConvertToPdf(tempInputFile, options);
            }, cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "DOCX successfully converted to PDF in {ElapsedMs} ms. Output size: {OutputBytes} bytes",
                stopwatch.ElapsedMilliseconds,
                pdfBytes.Length);

            return pdfBytes;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("DOCX conversion cancelled by request token");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while converting DOCX to PDF");
            throw new ConversionException($"Failed to convert document: {ex.Message}", ex);
        }
        finally
        {
            TryDeleteTempFile(tempInputFile);
            _concurrencySemaphore.Release();
        }
    }

    public async Task ConvertAsync(
        Stream docxStream,
        Stream pdfOutputStream,
        MiniPdfConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(docxStream);
        ArgumentNullException.ThrowIfNull(pdfOutputStream);

        var bytes = await ConvertAsync(docxStream, options, cancellationToken);
        await pdfOutputStream.WriteAsync(bytes, cancellationToken);
    }

    private void TryDeleteTempFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to delete temporary file {FilePath}", filePath);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _concurrencySemaphore.Dispose();
        _disposed = true;
    }
}
