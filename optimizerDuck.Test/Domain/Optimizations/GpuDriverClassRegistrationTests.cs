using optimizerDuck.Domain.Optimizations.Categories;

namespace optimizerDuck.Test.Domain.Optimizations;

public class GpuDriverClassRegistrationTests
{
    [Theory]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\0000")]
    [InlineData(@"{4D36E968-E325-11CE-BFC1-08002BE10318}\0042")]
    public void ValidDisplayClassRegistration_ResolvesCorrectKey(string registration)
    {
        Assert.True(Gpu.TryParseDriverClassRegistration(registration, out var path));
        Assert.Equal(@"SYSTEM\CurrentControlSet\Control\Class\" + registration, path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"VideoController1")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\0")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\00000")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\000x")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\0000 ")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\0000\other")]
    [InlineData(@"{4d36e967-e325-11ce-bfc1-08002be10318}\0000")]
    public void InvalidDisplayClassRegistration_FailsClosed(string? registration)
    {
        Assert.False(Gpu.TryParseDriverClassRegistration(registration, out var path));
        Assert.Empty(path);
    }

    [Fact]
    public void UnsafeGlobalTweaks_AreNotDiscoverable()
    {
        Assert.Null(typeof(Performance).GetNestedType("OptimizeMultimediaScheduler"));
        Assert.Null(typeof(Performance).GetNestedType("ProcessPriority"));
        Assert.Null(typeof(BloatwareAndServices).GetNestedType("ConfigureServices"));
    }
}
