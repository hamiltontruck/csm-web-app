using CSM.Infrastructure.Persistence;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class PostgresDateTimeNormalizerTests
{
    [Fact]
    public void Normalize_TreatsUnspecifiedValuesAsUtcWithoutChangingTheClockTime()
    {
        var input = new DateTime(2026, 10, 16, 12, 35, 0, DateTimeKind.Unspecified);

        var result = PostgresDateTimeNormalizer.Normalize(input);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(input.Ticks, result.Ticks);
    }

    [Fact]
    public void Normalize_ConvertsLocalValuesToUtc()
    {
        var input = new DateTime(2026, 10, 16, 12, 35, 0, DateTimeKind.Local);

        var result = PostgresDateTimeNormalizer.Normalize(input);

        Assert.Equal(input.ToUniversalTime(), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }
}
