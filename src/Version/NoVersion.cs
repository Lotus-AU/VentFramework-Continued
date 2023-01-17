using Hazel;

namespace VentLib.Version;

internal sealed class NoVersion: Version
{
    public override Version Read(MessageReader reader)
    {
        return new NoVersion();
    }

    protected override void WriteInfo(MessageWriter writer) { }

    public override string ToString() => "NoVersion";
}