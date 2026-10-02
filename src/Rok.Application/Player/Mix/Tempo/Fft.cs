using System.Numerics;

namespace Rok.Application.Player.Mix.Tempo;

/// <summary>In-place radix-2 FFT of a fixed power-of-two size, with precomputed tables.</summary>
internal sealed class Fft
{
    private readonly float[] _cos;
    private readonly float[] _sin;
    private readonly int[] _reversed;

    public Fft(int size)
    {
        if (size < 2 || (size & (size - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be a power of two.");

        Size = size;
        _cos = new float[size / 2];
        _sin = new float[size / 2];
        _reversed = new int[size];

        for (var i = 0; i < size / 2; i++)
        {
            var angle = -2 * Math.PI * i / size;
            _cos[i] = (float)Math.Cos(angle);
            _sin[i] = (float)Math.Sin(angle);
        }

        var bits = BitOperations.Log2((uint)size);

        for (var i = 0; i < size; i++)
        {
            var reversed = 0;

            for (var bit = 0; bit < bits; bit++)
            {
                if ((i & (1 << bit)) != 0)
                    reversed |= 1 << (bits - 1 - bit);
            }

            _reversed[i] = reversed;
        }
    }

    public int Size { get; }

    public void Transform(Span<float> real, Span<float> imaginary)
    {
        if (real.Length != Size || imaginary.Length != Size)
            throw new ArgumentException("Buffers must match the FFT size.");

        for (var i = 0; i < Size; i++)
        {
            var j = _reversed[i];

            if (j <= i)
                continue;

            (real[i], real[j]) = (real[j], real[i]);
            (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
        }

        for (var length = 2; length <= Size; length <<= 1)
        {
            var half = length / 2;
            var step = Size / length;

            for (var start = 0; start < Size; start += length)
            {
                for (var k = 0; k < half; k++)
                {
                    var cos = _cos[k * step];
                    var sin = _sin[k * step];
                    var even = start + k;
                    var odd = even + half;
                    var tr = (real[odd] * cos) - (imaginary[odd] * sin);
                    var ti = (real[odd] * sin) + (imaginary[odd] * cos);

                    real[odd] = real[even] - tr;
                    imaginary[odd] = imaginary[even] - ti;
                    real[even] += tr;
                    imaginary[even] += ti;
                }
            }
        }
    }
}