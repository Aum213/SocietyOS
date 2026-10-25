using System;
using Microsoft.Extensions.Configuration;

namespace SocietyOS.Services
{
    public interface ISocietyTimeZoneService
    {
        string TimeZoneDisplayName { get; }
        DateTime UtcNow { get; }
        DateTime NowLocal { get; }
        DateTime ToLocalTime(DateTime utcDateTime);
        DateTime ToUtc(DateOnly localDate, TimeOnly localTime);
    }

    public class SocietyTimeZoneService : ISocietyTimeZoneService
    {
        private readonly TimeZoneInfo _societyTimeZone;

        public SocietyTimeZoneService(IConfiguration configuration)
        {
            var configuredId = configuration["SocietySettings:TimeZoneId"] ?? "Asia/Kolkata";
            try
            {
                _societyTimeZone = TimeZoneInfo.FindSystemTimeZoneById(configuredId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback for older Windows time zone tables if needed
                _societyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
        }

        public string TimeZoneDisplayName => "IST (Asia/Kolkata)";

        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime NowLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _societyTimeZone);

        public DateTime ToLocalTime(DateTime utcDateTime)
        {
            var normalizedUtc = utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, _societyTimeZone);
        }

        public DateTime ToUtc(DateOnly localDate, TimeOnly localTime)
        {
            var unspecifiedLocal = localDate.ToDateTime(localTime, DateTimeKind.Unspecified);

            if (_societyTimeZone.IsInvalidTime(unspecifiedLocal))
            {
                throw new InvalidOperationException("The selected local time is invalid due to a time-zone transition.");
            }

            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, _societyTimeZone);
        }
    }
}