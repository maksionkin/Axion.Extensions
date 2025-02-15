namespace System;

public static class TimeSpanExtensions
{
    public static TimeSpan Half(this TimeSpan value) =>
#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        value / 2;
#else
        TimeSpan.FromMilliseconds(value.TotalMilliseconds / 2);
#endif
}
