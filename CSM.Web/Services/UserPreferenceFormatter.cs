using System.Globalization;
namespace CSM.Web.Services;

public static class UserPreferenceFormatter
{
    public static string Timestamp(DateTime? value, string? dateFormat, string? timeFormat, string? timeZone, string? language)
    {
        if (value is null) return "—";
        var utc = value.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value.Value.ToUniversalTime();
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "UTC"); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        var date = dateFormat is "dd/MM/yyyy" or "MM/dd/yyyy" or "yyyy-MM-dd" ? dateFormat : "yyyy-MM-dd";
        var culture = CultureInfo.GetCultureInfo(language is "en" or "am" or "om" ? language : "en");
        return TimeZoneInfo.ConvertTimeFromUtc(utc, zone).ToString(date + (timeFormat == "12h" ? " hh:mm tt" : " HH:mm"), culture);
    }
}
