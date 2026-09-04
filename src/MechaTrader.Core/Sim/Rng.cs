namespace MechaTrader.Core.Sim;

/// <summary>
/// xorshift64* — small, fast, and fully serialisable via a single ulong.
///
/// The generator state lives in <see cref="State.GameState"/> rather than in a static,
/// so a save file restores the exact random sequence and a given seed plus a given
/// command list always reproduces the same game. Determinism is what makes the balance
/// harness, replays and regression tests possible.
/// </summary>
public sealed class Rng
{
    private const ulong DefaultSeed = 0x9E3779B97F4A7C15UL;

    private ulong _state;

    public Rng(ulong seed) => _state = seed == 0 ? DefaultSeed : seed;

    /// <summary>Raw generator state, for persisting into and restoring from game state.</summary>
    public ulong State
    {
        get => _state;
        set => _state = value == 0 ? DefaultSeed : value;
    }

    public ulong NextULong()
    {
        var x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Uniform in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Uniform in [-1, 1).</summary>
    public double NextSigned() => NextDouble() * 2.0 - 1.0;

    public int NextInt(int maxExclusive)
        => maxExclusive <= 0 ? 0 : (int)(NextDouble() * maxExclusive);
}
