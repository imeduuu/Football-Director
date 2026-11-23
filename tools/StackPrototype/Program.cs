using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<MatchBatchBenchmark>();

[MemoryDiagnoser]
[ShortRunJob]
public class MatchBatchBenchmark
{
    private const int MatchCount = 100;
    private const int IntervalCount = 18;
    private const double IntervalSeconds = 5.0;
    private ulong[] _seeds = [];

    [GlobalSetup]
    public void Setup()
    {
        _seeds = new ulong[MatchCount];
        for (var i = 0; i < _seeds.Length; i++)
            _seeds[i] = 0x853c49e6748fea9bUL + (ulong)i * 0x9e3779b97f4a7c15UL;
    }

    [Benchmark(Description = "100 dummy matches x 18 intervals")]
    public double SimulateBatch()
    {
        var checksum = 0.0;
        for (var match = 0; match < MatchCount; match++)
        {
            var rng = new Pcg32(_seeds[match]);
            var home = 0.0;
            var away = 0.0;
            for (var interval = 0; interval < IntervalCount; interval++)
            {
                var pace = 0.85 + rng.NextUnitDouble() * 0.3;
                var homeChance = (0.025 + rng.NextUnitDouble() * 0.035) * pace;
                var awayChance = (0.020 + rng.NextUnitDouble() * 0.032) / pace;
                var homeNoise = (rng.NextUnitDouble() - 0.5) * 0.12;
                var awayNoise = (rng.NextUnitDouble() - 0.5) * 0.12;
                home += Math.Max(0.0, homeChance + homeNoise) * IntervalSeconds;
                away += Math.Max(0.0, awayChance + awayNoise) * IntervalSeconds;
            }

            checksum += home * 1.013 + away * 0.997;
        }

        return checksum;
    }

    // Small, fixed algorithm: no platform RNG and no allocations in the measured loop.
    private struct Pcg32(ulong seed)
    {
        private ulong _state = seed;
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;

        public uint NextUInt()
        {
            var oldState = _state;
            _state = unchecked(oldState * Multiplier + Increment);
            var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            var rotation = (int)(oldState >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        public double NextUnitDouble() => NextUInt() * (1.0 / 4294967296.0);
    }
}
