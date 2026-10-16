namespace CSM.Infrastructure.Persistence;

internal static class PostgresDateTimeNormalizer
{
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value
    };
}
