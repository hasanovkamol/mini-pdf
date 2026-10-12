using Microsoft.Extensions.Options;
using MiniSoftware;
using MiniPdf.DocxConverter.Api.Configuration;
using MiniPdf.DocxConverter.Api.Services.Abstractions;

namespace MiniPdf.DocxConverter.Api.Services.Implementations;

public sealed class FontManagerService(
    IOptions<ConversionSettings> settings,
    ILogger<FontManagerService> logger) : IFontManagerService
{
    private readonly HashSet<string> _registeredFonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private bool _initialized;

    public void InitializeFonts()
    {
        lock (_lock)
        {
            if (_initialized) return;

            var fontDirectories = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "Fonts"),
                "/usr/share/fonts",
                "/usr/local/share/fonts"
            };

            var customDir = settings.Value.CustomFontsDirectory;
            if (!string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir))
            {
                fontDirectories.Add(customDir);
            }

            var extensions = new[] { "*.ttf", "*.otf", "*.ttc" };

            foreach (var dir in fontDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                logger.LogInformation("Scanning fonts in directory: {Directory}", dir);

                try
                {
                    foreach (var ext in extensions)
                    {
                        var files = Directory.GetFiles(dir, ext, SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            try
                            {
                                var fontName = Path.GetFileNameWithoutExtension(file);
                                if (_registeredFonts.Contains(fontName)) continue;

                                var fontBytes = File.ReadAllBytes(file);
                                MiniSoftware.MiniPdf.RegisterFont(fontName, fontBytes);
                                _registeredFonts.Add(fontName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogDebug(ex, "Failed to register font from file: {File}", file);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not traverse font directory: {Directory}", dir);
                }
            }

            logger.LogInformation("Font registration completed. Total fonts registered: {Count}", _registeredFonts.Count);
            _initialized = true;
        }
    }

    public IReadOnlyList<string> GetRegisteredFonts()
    {
        lock (_lock)
        {
            return _registeredFonts.Order().ToList();
        }
    }
}
