using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GymRatService.IntegrationTests.Chat;

public sealed record FakeOpenRouterRequest(string Authorization, string Path, JsonElement Body)
{
    public string Model => Body.GetProperty("model").GetString() ?? string.Empty;
}

public sealed record FakeOpenRouterReply(int Status, string Body, TimeSpan Delay = default)
{
    public static FakeOpenRouterReply Text(string content, int totalTokens = 15) =>
        Completion(new { role = "assistant", content }, "stop", totalTokens);

    public static FakeOpenRouterReply NullContent() =>
        Completion(new { role = "assistant", content = (string?)null }, "stop", 15);

    public static FakeOpenRouterReply ToolCall(string name, string arguments = "{}") =>
        Completion(
            new
            {
                role = "assistant",
                content = (string?)null,
                tool_calls = new[] { new { id = "call_1", type = "function", function = new { name, arguments } } },
            },
            "tool_calls",
            15);

    public static FakeOpenRouterReply Error(int status) =>
        new(status, JsonSerializer.Serialize(new { error = new { message = $"upstream said {status}", code = status } }));

    public FakeOpenRouterReply After(TimeSpan delay) => this with { Delay = delay };

    private static FakeOpenRouterReply Completion(object message, string finishReason, int totalTokens) =>
        new(
            200,
            JsonSerializer.Serialize(new
            {
                id = "gen-1",
                @object = "chat.completion",
                created = 1,
                model = "fake",
                choices = new[] { new { index = 0, message, finish_reason = finishReason } },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = totalTokens },
            }));
}

public sealed class FakeOpenRouterServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly object _gate = new();
    private readonly List<FakeOpenRouterRequest> _requests = [];

    public FakeOpenRouterServer(Func<FakeOpenRouterRequest, FakeOpenRouterReply> handler)
    {
        var port = GetFreePort();
        Endpoint = $"http://127.0.0.1:{port}/api/v1";
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();

        _ = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(context, handler));
            }
        });
    }

    public string Endpoint { get; }

    public IReadOnlyList<FakeOpenRouterRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    public void Dispose() => _listener.Close();

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    private async Task HandleAsync(HttpListenerContext context, Func<FakeOpenRouterRequest, FakeOpenRouterReply> handler)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = JsonDocument.Parse(await reader.ReadToEndAsync()).RootElement.Clone();
            var request = new FakeOpenRouterRequest(
                context.Request.Headers["Authorization"] ?? string.Empty,
                context.Request.Url?.AbsolutePath ?? string.Empty,
                body);

            lock (_gate)
            {
                _requests.Add(request);
            }

            var reply = handler(request);

            if (reply.Delay > TimeSpan.Zero)
            {
                await Task.Delay(reply.Delay);
            }

            var bytes = Encoding.UTF8.GetBytes(reply.Body);
            context.Response.StatusCode = reply.Status;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception)
        {
            context.Response.Abort();
        }
    }
}
