using System;

namespace DestinyGarden.Domain
{
    /// <summary>
    /// Finite saved entropy buffer (DATA_CONTRACT 4.1). UniformBelow uses consecutive
    /// big-endian uint32 words with rejection sampling. n=1 consumes nothing.
    /// </summary>
    public sealed class EntropyBuffer
    {
        private readonly byte[] _bytes;
        public int Offset { get; private set; }
        public int RejectedWords { get; private set; }
        public bool Exhausted { get; private set; }

        public EntropyBuffer(byte[] bytes)
        {
            _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public int UniformBelow(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
            if (n == 1) return 0;
            ulong limit = (1UL << 32) - ((1UL << 32) % (ulong)n);
            while (true)
            {
                if (Offset + 4 > _bytes.Length) { Exhausted = true; return -1; }
                uint word = (uint)((_bytes[Offset] << 24) | (_bytes[Offset + 1] << 16) |
                                   (_bytes[Offset + 2] << 8) | _bytes[Offset + 3]);
                Offset += 4;
                if (word < limit) return (int)(word % (uint)n);
                RejectedWords++;
            }
        }

        /// <summary>Fair D6 from single bytes: reject b &gt;= 252, return 1 + (b % 6). -1 when exhausted.</summary>
        public static int RollD6(byte[] bytes, out int bytesConsumed, out int rejected)
        {
            bytesConsumed = 0; rejected = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                bytesConsumed = i + 1;
                if (bytes[i] >= 252) { rejected++; continue; }
                return 1 + (bytes[i] % 6);
            }
            return -1;
        }
    }

    /// <summary>Weather selection: UniformBelow(256) over cumulative weights (total must be 256).</summary>
    public static class WeatherSelection
    {
        public static WeatherId Select(byte[] weatherBytes32, int periodIndex)
        {
            if (weatherBytes32 == null || weatherBytes32.Length < 4)
                throw new ArgumentException("Weather entropy requires at least 4 bytes.");
            int[] weights = periodIndex == 0 ? new[] { 256, 0, 0 } : new[] { 96, 96, 64 };
            var buf = new EntropyBuffer(weatherBytes32);
            int v = buf.UniformBelow(256);
            int acc = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (v < acc) return (WeatherId)i;
            }
            return WeatherId.Clear; // unreachable when weights sum to 256
        }
    }
}
