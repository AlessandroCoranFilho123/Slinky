namespace Slinky;

public sealed class RecoveryLifecycle
{
    public bool IsClosed { get; private set; }

    public bool CanSchedule => !IsClosed;

    public void Close() => IsClosed = true;

    public bool TryBeginWrite() => !IsClosed;
}
