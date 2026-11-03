using System.ClientModel.Primitives;

namespace GymRatService.IntegrationTests.Chat;

public sealed class StatusPipelineResponse(int status) : PipelineResponse
{
    private Stream? _contentStream;

    public override int Status => status;

    public override string ReasonPhrase => string.Empty;

    public override Stream? ContentStream
    {
        get => _contentStream;
        set => _contentStream = value;
    }

    public override BinaryData Content => BinaryData.Empty;

    protected override PipelineResponseHeaders HeadersCore => new EmptyHeaders();

    public override BinaryData BufferContent(CancellationToken cancellationToken = default) => BinaryData.Empty;

    public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(BinaryData.Empty);

    public override void Dispose()
    {
    }

    private sealed class EmptyHeaders : PipelineResponseHeaders
    {
        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Enumerable.Empty<KeyValuePair<string, string>>().GetEnumerator();

        public override bool TryGetValue(string name, out string? value)
        {
            value = null;

            return false;
        }

        public override bool TryGetValues(string name, out IEnumerable<string>? values)
        {
            values = null;

            return false;
        }
    }
}
