using GamePadEmulator.Core;
using Xunit;

namespace GamePadEmulator.Tests;

/// <summary>
/// Verifies the normalized-axis → HID-integer quantization that the DS4 and Xbox
/// backends rely on. These are the exact transforms that decide whether a pushed
/// stick registers as "fully right" in a game, so correctness here is what makes
/// the on-screen UI map faithfully to the virtual device.
/// </summary>
public class AxisMathTests
{
    [Theory]
    [InlineData(0.0, 128)]      // centre
    [InlineData(1.0, 255)]      // full right/up (DS4)
    [InlineData(-1.0, 0)]       // full left/down (DS4)
    [InlineData(0.5, 191)]      // half right
    [InlineData(-0.5, 64)]      // half left
    public void ToByte8_maps_centre_and_extremes(double input, byte expected)
    {
        Assert.Equal(expected, AxisMath.ToByte8(input));
    }

    [Theory]
    [InlineData(0.0,   (ushort)0)]
    [InlineData(0.5,   (ushort)128)]
    [InlineData(1.0,   (ushort)255)]
    [InlineData(1.2,   (ushort)255)]   // clamped
    [InlineData(-0.1,  (ushort)0)]     // clamped
    public void TriggerToByte8_maps_and_clamps(double input, ushort expected)
    {
        Assert.Equal((byte)expected, AxisMath.TriggerToByte8(input));
    }

    [Theory]
    [InlineData(0.0,   0)]
    [InlineData(1.0,   32767)]
    [InlineData(-1.0,  -32767)]
    [InlineData(-1.01, -32767)]   // clamped to -32767, never the -32768 asymmetry
    public void ToInt16_maps_centre_and_extremes(double input, int expected)
    {
        Assert.Equal((short)expected, AxisMath.ToInt16(input));
    }

    [Fact]
    public void ToByte8_is_monotonic_across_range()
    {
        byte prev = AxisMath.ToByte8(-1.0);
        for (double v = -0.99; v <= 1.0; v += 0.01)
        {
            byte cur = AxisMath.ToByte8(v);
            Assert.True(cur >= prev, $"non-monotonic at {v}: {prev} -> {cur}");
            prev = cur;
        }
    }
}
