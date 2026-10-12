using MiniSoftware;

namespace MiniPdf.DocxConverter.Api.Services.Abstractions;

public interface IDocxToPdfConverter
{
    int AvailableConcurrencySlots { get; }

    Task<byte[]> ConvertAsync(
        Stream docxStream,
        MiniPdfConversionOptions? options = null,
        CancellationToken cancellationToken = default);

    Task ConvertAsync(
        Stream docxStream,
        Stream pdfOutputStream,
        MiniPdfConversionOptions? options = null,
        CancellationToken cancellationToken = default);
}
