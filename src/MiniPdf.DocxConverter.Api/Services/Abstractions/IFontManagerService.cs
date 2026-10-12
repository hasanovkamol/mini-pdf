namespace MiniPdf.DocxConverter.Api.Services.Abstractions;

public interface IFontManagerService
{
    void InitializeFonts();

    IReadOnlyList<string> GetRegisteredFonts();
}
