using LoanDocAI.Api.Models;

namespace LoanDocAI.Api.Services;

public interface IDocumentExtractor
{
    Task<DocumentExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken cancellationToken = default);
    Task<DocumentExtractionResult> ExtractAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);
}
