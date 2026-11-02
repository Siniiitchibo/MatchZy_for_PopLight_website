using CounterStrikeSharp.API;


class SourceSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        Server.NextWorldUpdate(() => d(state));
    }

    public override SynchronizationContext CreateCopy()
    {
        return this;
    }
}

class SyncContextScope : IDisposable
{
    private static readonly SynchronizationContext _sourceContext = new SourceSynchronizationContext();
    private readonly SynchronizationContext? _oldContext;

    public SyncContextScope()
    {
        _oldContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(_sourceContext);
    }

    public void Dispose()
    {
        if (_oldContext != null)
            SynchronizationContext.SetSynchronizationContext(_oldContext);
    }
}
