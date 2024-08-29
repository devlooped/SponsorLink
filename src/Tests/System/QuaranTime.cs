namespace System
{
    /// <summary>
    /// Provides the <see cref="Epoch"/> constant that signaled the 
    /// start of the Quarantime time and methods to convert to and from 
    /// Quarantine-relative values.
    /// </summary>
    static partial class QuaranTime
    {
        // From DateTime
        internal const long DateTimeMinTicks = 0L;
        internal const long DateTimeMaxTicks = 3155378975999999999L;

        internal const long QuarantineMinSeconds = DateTimeMinTicks / TimeSpan.TicksPerSecond - QuarantineEpochSeconds;
        internal const long QuarantineMaxSeconds = DateTimeMaxTicks / TimeSpan.TicksPerSecond - QuarantineEpochSeconds;

        internal const long QuarantineEpochTicks = 637202700000000000;
        internal const long QuarantineEpochSeconds = QuarantineEpochTicks / TimeSpan.TicksPerSecond;
        internal const long QuarantineEpochMiliseconds = QuarantineEpochTicks / TimeSpan.TicksPerMillisecond;

        /// <summary>
        /// The value of this constant is equivalent to 00:00:00 GMT-0300, March 3, 2020, in the Gregorian calendar. 
        /// <see cref="Epoch"/> defines the point in time when the quarantine started in Argentina.
        /// </summary>
        public static DateTimeOffset Epoch { get; } = new DateTimeOffset(637202592000000000, TimeSpan.FromHours(-3));

        /// <summary>
        /// Converts a Quarantine time expressed as the number of seconds that have elapsed since 
        /// since 2020-03-20T00:00:00-03:00 to a <see cref="DateTimeOffset"/> value.
        /// </summary>
        /// <param name="seconds">
        /// A Quarantine time, expressed as the number of seconds that have elapsed since 
        /// 2020-03-20T00:00:00-03:00 (March 3, 2020, at 12:00 AM GMT-0300). For Quarantine times 
        /// before this date, its value is negative.
        /// </param>
        /// <returns>
        /// A date and time value that represents the same moment in time as the Quarantine time.
        /// </returns>
        /// <remarks>
        /// The <see cref="DateTimeOffset.Offset"/> property value of the returned <see cref="DateTimeOffset"/> 
        /// instance is <see cref="TimeSpan.Zero"/>, which represents Coordinated Universal Time. 
        /// You can convert it to the time in a specific time zone by calling the 
        /// <see cref="TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)"/>.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="seconds"/> seconds is less than -63,720,270,000 or greater than 251,817,627,599.
        /// </exception>
        public static DateTimeOffset FromQuaranTimeSeconds(long seconds)
        {
            if (seconds < QuarantineMinSeconds || seconds > QuarantineMaxSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            var ticks = seconds * TimeSpan.TicksPerSecond + QuarantineEpochTicks;
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        /// <summary>
        /// Converts a Quarantine time expressed as the number of milliseconds that have elapsed 
        /// since 2020-03-20T00:00:00-03:00 to a <see cref="DateTimeOffset"/> value.
        /// </summary>
        /// <param name="milliseconds">
        /// A Quarantine time, expressed as the number of milliseconds that have elapsed since 
        /// 2020-03-20T00:00:00-03:00 (March 3, 2020, at 12:00 AM GMT-0300). For Quarantine times 
        /// before this date, its value is negative.
        /// </param>
        /// <returns>
        /// A date and time value that represents the same moment in time as the Quarantine time.
        /// </returns>
        /// <remarks>
        /// The <see cref="DateTimeOffset.Offset"/> property value of the returned <see cref="DateTimeOffset"/> 
        /// instance is <see cref="TimeSpan.Zero"/>, which represents Coordinated Universal Time. 
        /// You can convert it to the time in a specific time zone by calling the 
        /// <see cref="TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)"/>.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="milliseconds"/> seconds is less than -63,720,270,000,000 or greater than 251,817,627,599,999.
        /// </exception>
        public static DateTimeOffset FromQuaranTimeMilliseconds(long milliseconds)
        {
            const long MinMilliseconds = DateTimeMinTicks / TimeSpan.TicksPerMillisecond - QuarantineEpochMiliseconds;
            const long MaxMilliseconds = DateTimeMaxTicks / TimeSpan.TicksPerMillisecond - QuarantineEpochMiliseconds;

            if (milliseconds < MinMilliseconds || milliseconds > MaxMilliseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            }

            var ticks = milliseconds * TimeSpan.TicksPerMillisecond + QuarantineEpochTicks;
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        /// <summary>
        /// Returns the number of seconds that have elapsed since 2020-03-20T00:00:00-03:00.
        /// </summary>
        /// <remarks>
        /// Quarantine time represents the number of seconds that have elapsed since 2020-03-20T00:00:00-03:00 
        /// (March 3, 2020, at 12:00 AM GMT-0300). It does not take leap seconds into account. This method 
        /// returns the number of seconds in Quarantine time.
        /// <para>
        /// This method first converts the current instance to UTC before returning its Quarantine time. 
        /// For date and time values before 2020-03-20T00:00:00-03:00, this method returns a negative value.
        /// </para>
        /// </remarks>
        /// <returns>The number of seconds that have elapsed since 2020-03-20T00:00:00-03:00.</returns>
        public static long ToQuaranTimeSeconds(this DateTimeOffset dateTime)
        {
            // Truncate just like ToUnixTimeSeconds does, see https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/DateTimeOffset.cs#L583

            var seconds = dateTime.UtcDateTime.Ticks / TimeSpan.TicksPerSecond;
            return seconds - QuarantineEpochSeconds;
        }

        /// <summary>
        /// Returns the number of milliseconds that have elapsed since 2020-03-20T00:00:00-03:00.
        /// </summary>
        /// <remarks>
        /// Quarantine time represents the number of seconds that have elapsed since 2020-03-20T00:00:00-03:00 
        /// (March 3, 2020, at 12:00 AM GMT-0300). It does not take leap seconds into account. This method 
        /// returns the number of milliseconds in Quarantine time.
        /// <para>
        /// This method first converts the current instance to UTC before returning the number of milliseconds 
        /// in its Quarantine time. For date and time values before 2020-03-20T00:00:00-03:00, this method returns 
        /// a negative value.
        /// </para>
        /// </remarks>
        /// <returns>The number of miliseconds that have elapsed since 2020-03-20T00:00:00-03:00.</returns>
        public static long ToQuaranTimeMilliseconds(this DateTimeOffset dateTime)
        {
            // Truncate just like ToUnixTimeMiliseconds does, see https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/DateTimeOffset.cs#L605

            var seconds = dateTime.UtcDateTime.Ticks / TimeSpan.TicksPerMillisecond;
            return seconds - QuarantineEpochMiliseconds;
        }
    }
}
