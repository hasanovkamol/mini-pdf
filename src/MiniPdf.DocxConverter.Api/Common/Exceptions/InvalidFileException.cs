namespace MiniPdf.DocxConverter.Api.Common.Exceptions;

public class InvalidFileException : Exception
{
    public InvalidFileException(string message) : base(message)
    {
    }

    public InvalidFileException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
