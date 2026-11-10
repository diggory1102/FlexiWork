using FW.Application.Features.Organization;

namespace FW.UnitTests.Organization;

public class BssidFormatTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF", "AA:BB:CC:DD:EE:FF")]
    [InlineData("aa:bb:cc:dd:ee:ff", "AA:BB:CC:DD:EE:FF")]
    [InlineData("00-1a-2b-3c-4d-5e", "00:1A:2B:3C:4D:5E")]
    [InlineData("001A2B3C4D5E", "00:1A:2B:3C:4D:5E")]
    [InlineData("  00:1A:2B:3C:4D:5E  ", "00:1A:2B:3C:4D:5E")]
    public void Valid_inputs_are_normalized(string input, string expected)
    {
        Assert.True(BssidFormat.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AA:BB:CC:DD:EE")]
    [InlineData("AA:BB:CC:DD:EE:FF:00")]
    [InlineData("GG:BB:CC:DD:EE:FF")]
    [InlineData("AABBCCDDEEF")]
    [InlineData("AA:BB-CC:DD:EE:FFF")]
    public void Invalid_inputs_are_rejected(string? input) =>
        Assert.False(BssidFormat.TryNormalize(input, out _));
}
