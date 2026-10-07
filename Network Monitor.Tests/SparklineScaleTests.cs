namespace Network_Monitor.Tests;

public class SparklineScaleTests
{
    [Fact]
    public void For_WithoutZeroBaseline_SpansTheValuesOwnRange()
    {
        var scale = SparklineScale.For(new double[] { 15, 31, 22 }, baselineZero: false);

        Assert.Equal(15, scale.Bottom);
        Assert.Equal(31, scale.Top);
        Assert.Equal(0, scale.GetHeight(15));
        Assert.Equal(1, scale.GetHeight(31));
    }

    [Fact]
    public void For_WithZeroBaseline_StartsAtZero()
    {
        var scale = SparklineScale.For(new double[] { 50, 100 }, baselineZero: true);

        Assert.Equal(0, scale.Bottom);
        Assert.Equal(0.5, scale.GetHeight(50));
    }

    [Fact]
    public void GetHeight_ForAFlatSeries_SitsOnTheMidline()
    {
        var scale = SparklineScale.For(new double[] { 20, 20 }, baselineZero: false);

        Assert.Equal(0.5, scale.GetHeight(20));
    }

    [Fact]
    public void GetHeight_ForAllZeroesOnAZeroBaseline_SitsOnTheFloor()
    {
        var scale = SparklineScale.For(new double[] { 0, 0 }, baselineZero: true);

        Assert.Equal(0, scale.GetHeight(0));
    }
}
