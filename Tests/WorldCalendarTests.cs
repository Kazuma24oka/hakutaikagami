using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class WorldCalendarTests
{
    [Theory]
    [InlineData(0, "1560年1月1日 00:00:00")]
    [InlineData(86399, "1560年1月1日 23:59:59")]
    [InlineData(86400, "1560年1月2日 00:00:00")]
    [InlineData(5097600, "1560年2月29日 00:00:00")]
    public void SharedSecondsHaveExactCalendar(long seconds, string expected) =>
        Assert.Equal(expected, WorldCalendar.Display("1560-01-01", seconds));

    [Fact] public void HistoricalBoundaryUsesEpochAndNotHardcodedElapsedYears()
    {
        foreach (string epoch in new[] { "1560-01-01", "1600-02-29", "1649-12-31" })
        {
            long end = WorldCalendar.HistoricalEndSeconds(epoch);
            Assert.Equal(new DateTime(1650, 1, 1), WorldCalendar.At(epoch, end));
            Assert.Equal(new DateTime(1649, 12, 31, 23, 59, 59), WorldCalendar.At(epoch, end - 1));
        }
        Assert.Equal(0, WorldCalendar.HistoricalEndSeconds("1651-01-01"));
    }

    [Theory]
    [InlineData("1560-01-01", -1)]
    [InlineData("1560-02-30", 0)]
    [InlineData("1560-01-01", long.MaxValue)]
    public void InvalidOrOverflowingCalendarIsRejected(string epoch, long seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldCalendar.At(epoch, seconds));

    [Fact] public void MaximumSecondIsExactAndDoesNotRoundIntoNextDay()
    {
        Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59), WorldCalendar.At("9999-12-31", 86399));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldCalendar.At("9999-12-31", 86400));
    }
}
