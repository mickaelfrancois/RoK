namespace Rok.Shared.Extensions
{
    public static class DateTimeExtensions
    {
        private const long TicksPerMinute = TimeSpan.TicksPerMinute;

        public static DateTime TruncateToMinutes(this DateTime dateTime)
        {
            long ticks = dateTime.Ticks;
            long truncatedTicks = ticks - (ticks % TicksPerMinute);

            return new DateTime(truncatedTicks, dateTime.Kind);
        }
    }
}
