namespace Network_Monitor.Tests;

public class SparklineScaleTests
{
    [Fact]
    public void For_AHealthyLatencyTrace_StaysInTheLowerPart()
    {
        var scale = SparklineScale.For(new double[] { 15, 31, 22 }, minimumTop: 50);

        Assert.Equal(50, scale.Top);
        Assert.Equal(0.3, scale.GetHeight(15), 3);
        Assert.Equal(0.62, scale.GetHeight(31), 3);
    }

    [Fact]
    public void For_ALatencySpike_ReachesTheTop()
    {
        var scale = SparklineScale.For(new double[] { 20, 300, 20 }, minimumTop: 50);

        Assert.Equal(1, scale.GetHeight(300));
        Assert.True(scale.GetHeight(20) < 0.1);
    }

    [Fact]
    public void For_Throughput_StartsAtZeroAndFillsTheHeight()
    {
        var scale = SparklineScale.For(new double[] { 50, 100 }, minimumTop: 0);

        Assert.Equal(0.5, scale.GetHeight(50));
        Assert.Equal(1, scale.GetHeight(100));
    }

    [Fact]
    public void GetHeight_ForAllZeroes_SitsOnTheFloor()
    {
        var scale = SparklineScale.For(new double[] { 0, 0 }, minimumTop: 0);

        Assert.Equal(0, scale.GetHeight(0));
    }
}
