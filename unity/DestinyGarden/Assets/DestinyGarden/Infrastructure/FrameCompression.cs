using System;

namespace DestinyGarden.Infrastructure
{
    /// <summary>
    /// A small, dependency-free LZ77 compressor for journal frames (framed-json.v2). The garden's records are canonical
    /// JSON with the same keys repeated many times, which this shrinks several-fold. Pure managed code, so it behaves
    /// the same in the browser (WebGL) as in the app. Deterministic: the same input always gives the same bytes.
    ///
    /// Format: [uint32 LE original length] then groups of one flag byte and up to eight items (low bit first):
    /// flag 0 = one literal byte; flag 1 = a match of three bytes [uint16 LE distance 1..65535][length − 4, 0..255].
    /// Decompression checks every bound and the final length; anything malformed is a <see cref="FormatException"/>.
    /// </summary>
    public static class FrameCompression
    {
        private const int MinMatch = 4, MaxMatch = 259, Window = 65535, HashBits = 15, MaxChain = 48;

        public static byte[] Compress(byte[] input)
        {
            int n = input.Length;
            var output = new byte[4 + n + n / 8 + 16];
            output[0] = (byte)n; output[1] = (byte)(n >> 8); output[2] = (byte)(n >> 16); output[3] = (byte)(n >> 24);
            int op = 4;
            var head = new int[1 << HashBits];
            for (int i = 0; i < head.Length; i++) head[i] = -1;
            var prev = new int[Math.Max(1, n)];
            int ip = 0;
            while (ip < n)
            {
                int flagAt = op++;
                byte flags = 0;
                for (int bit = 0; bit < 8 && ip < n; bit++)
                {
                    int bestLen = 0, bestDist = 0;
                    if (ip + MinMatch <= n)
                    {
                        int h = Hash(input, ip);
                        int candidate = head[h];
                        for (int chain = 0; candidate >= 0 && ip - candidate <= Window && chain < MaxChain; chain++)
                        {
                            int len = 0, max = Math.Min(MaxMatch, n - ip);
                            while (len < max && input[candidate + len] == input[ip + len]) len++;
                            if (len > bestLen) { bestLen = len; bestDist = ip - candidate; if (len == max) break; }
                            candidate = prev[candidate];
                        }
                    }
                    if (bestLen >= MinMatch)
                    {
                        flags |= (byte)(1 << bit);
                        output[op++] = (byte)bestDist; output[op++] = (byte)(bestDist >> 8); output[op++] = (byte)(bestLen - MinMatch);
                        for (int k = 0; k < bestLen; k++) Insert(input, ip + k, n, head, prev);
                        ip += bestLen;
                    }
                    else
                    {
                        output[op++] = input[ip];
                        Insert(input, ip, n, head, prev);
                        ip++;
                    }
                }
                output[flagAt] = flags;
            }
            var result = new byte[op];
            Buffer.BlockCopy(output, 0, result, 0, op);
            return result;
        }

        private static int Hash(byte[] d, int i) =>
            (int)(((uint)(d[i] | d[i + 1] << 8 | d[i + 2] << 16 | d[i + 3] << 24) * 2654435761u) >> (32 - HashBits));

        private static void Insert(byte[] d, int i, int n, int[] head, int[] prev)
        {
            if (i + MinMatch > n) return;
            int h = Hash(d, i);
            prev[i] = head[h];
            head[h] = i;
        }

        public static byte[] Decompress(byte[] data) => Decompress(data, 0, data.Length);

        public static byte[] Decompress(byte[] data, int offset, int count)
        {
            if (count < 4) throw new FormatException("Compressed frame too short");
            int end = offset + count;
            long declared = (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
            if (declared > 64L * 1024 * 1024) throw new FormatException("Compressed frame too large");
            var output = new byte[declared];
            int ip = offset + 4, op = 0;
            while (op < output.Length)
            {
                if (ip >= end) throw new FormatException("Compressed frame ends early");
                byte flags = data[ip++];
                for (int bit = 0; bit < 8 && op < output.Length; bit++)
                {
                    if ((flags & (1 << bit)) == 0)
                    {
                        if (ip >= end) throw new FormatException("Compressed frame ends early");
                        output[op++] = data[ip++];
                        continue;
                    }
                    if (ip + 3 > end) throw new FormatException("Compressed frame ends early");
                    int dist = data[ip] | data[ip + 1] << 8;
                    int len = data[ip + 2] + MinMatch;
                    ip += 3;
                    if (dist == 0 || dist > op || op + len > output.Length) throw new FormatException("Bad match in compressed frame");
                    for (int k = 0; k < len; k++, op++) output[op] = output[op - dist];
                }
            }
            if (ip != end) throw new FormatException("Trailing bytes in compressed frame");
            return output;
        }
    }
}
