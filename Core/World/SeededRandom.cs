namespace HakutaiKagami.Core.World;

// Explicit algorithm and state: independent of System.Random implementation changes.
public sealed class SeededRandom(ulong seed)
{
    public ulong State { get; private set; } = seed;
    public uint Next()
    {
        State = unchecked(State * 6364136223846793005UL + 1442695040888963407UL);
        return (uint)(State >> 32);
    }
}
