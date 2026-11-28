using Microsoft.Extensions.Hosting;

namespace GymRatService.IntegrationTests;

internal sealed class GatedHostLifetime : IHostLifetime
{
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release()
    {
        _released.TrySetResult();
    }

    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        return _released.Task.WaitAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
