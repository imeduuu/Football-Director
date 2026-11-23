using BT.Core;
using Xunit;

namespace BT.Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void CoreAssemblyIsAvailableToTests()
    {
        Assert.Equal("BT.Core", CoreModule.Name);
    }
}
