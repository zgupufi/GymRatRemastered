using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace GymRatService.API.Results;

public sealed class CsvStreamResult(IAsyncEnumerable<string> lines, string fileName) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        var cancellationToken = context.HttpContext.RequestAborted;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileName }.ToString();

        await response.Body.WriteAsync(Encoding.UTF8.GetPreamble(), cancellationToken);

        await using var writer = new StreamWriter(response.Body, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };

        await foreach (var line in lines.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
    }
}
