using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using MiniPdf.DocxConverter.Api.Common.Exceptions;
using MiniPdf.DocxConverter.Api.Configuration;
using MiniPdf.DocxConverter.Api.Services.Abstractions;
using MiniSoftware;

namespace MiniPdf.DocxConverter.Api.Services.Implementations;

public sealed class LibreOfficeDocxConverter : IDocxToPdfConverter, IDisposable
{
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly ILogger<LibreOfficeDocxConverter> _logger;
    private readonly ConversionSettings _settings;
    private readonly string _executablePath;
    private bool _disposed;

    public int AvailableConcurrencySlots => _concurrencySemaphore.CurrentCount;

    public LibreOfficeDocxConverter(
        IOptions<ConversionSettings> settings,
        ILogger<LibreOfficeDocxConverter> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        var maxSlots = Math.Max(1, _settings.MaxConcurrentConversions);
        _concurrencySemaphore = new SemaphoreSlim(maxSlots, maxSlots);
        _executablePath = ResolveLibreOfficePath(_settings.LibreOfficePath);

        _logger.LogInformation("LibreOfficeDocxConverter initialized with binary: '{Path}'", _executablePath);
    }

    public async Task<byte[]> ConvertAsync(
        Stream docxStream,
        MiniPdfConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(docxStream);

        await _concurrencySemaphore.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        var workDir = Path.Combine(Path.GetTempPath(), $"docx2pdf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        var userProfileDir = Path.Combine(workDir, "lo_profile");
        Directory.CreateDirectory(userProfileDir);

        var inputDocxPath = Path.Combine(workDir, "document.docx");
        var expectedPdfPath = Path.Combine(workDir, "document.pdf");

        try
        {
            await using (var fileStream = new FileStream(
                inputDocxPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await docxStream.CopyToAsync(fileStream, cancellationToken);
            }

            var profileUri = "file:///" + userProfileDir.Replace('\\', '/');

            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = $"--headless \"-env:UserInstallation={profileUri}\" --convert-to pdf \"{inputDocxPath}\" --outdir \"{workDir}\"",
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var readOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var readErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
            });

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await readOutputTask;
            var stderr = await readErrorTask;

            if (process.ExitCode != 0 || !File.Exists(expectedPdfPath))
            {
                _logger.LogError(
                    "LibreOffice conversion failed. Exit code: {ExitCode}. Stdout: '{Stdout}'. Stderr: '{Stderr}'",
                    process.ExitCode,
                    stdout,
                    stderr);

                throw new ConversionException(
                    $"LibreOffice failed to convert document (Exit code: {process.ExitCode}). Details: {stderr}");
            }

            var pdfBytes = await File.ReadAllBytesAsync(expectedPdfPath, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Document successfully converted via LibreOffice in {ElapsedMs} ms. Output size: {OutputBytes} bytes",
                stopwatch.ElapsedMilliseconds,
                pdfBytes.Length);

            return pdfBytes;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LibreOffice conversion was cancelled by request token");
            throw;
        }
        catch (Exception ex) when (ex is not ConversionException)
        {
            _logger.LogError(ex, "Unexpected error in LibreOffice conversion");
            throw new ConversionException($"Error during document conversion: {ex.Message}", ex);
        }
        finally
        {
            TryDeleteDirectory(workDir);
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

    private static string ResolveLibreOfficePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var standardPaths = new[]
            {
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            };

            foreach (var path in standardPaths)
            {
                if (File.Exists(path)) return path;
            }

            return "soffice.exe";
        }

        var linuxPaths = new[]
        {
            "/usr/bin/libreoffice",
            "/usr/bin/soffice",
            "/usr/local/bin/libreoffice"
        };

        foreach (var path in linuxPaths)
        {
            if (File.Exists(path)) return path;
        }

        return "soffice";
    }

    private void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to clean temporary directory: {Dir}", directoryPath);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _concurrencySemaphore.Dispose();
        _disposed = true;
    }
}
