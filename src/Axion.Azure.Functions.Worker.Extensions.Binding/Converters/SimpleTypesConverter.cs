// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace Axion.Azure.Functions.Worker.Converters;

static class ConverterRegistartion
{
    public static void RegisterSimpleTypesConverters(this IServiceCollection services)
    {


        services.AddConverter((sbyte input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((short input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((int input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((long input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((System.Numerics.BigInteger input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((byte input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((ushort input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((uint input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((ulong input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((float input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((double input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((decimal input) => input.ToString(CultureInfo.InvariantCulture));

        services.AddConverter((string input) => sbyte.Parse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => short.Parse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => int.Parse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => long.Parse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => System.Numerics.BigInteger.Parse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => byte.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => ushort.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => uint.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => ulong.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => float.Parse(input, NumberStyles.AllowTrailingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => double.Parse(input, NumberStyles.AllowTrailingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture));

        services.AddConverter((string input) => decimal.Parse(input, NumberStyles.AllowTrailingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture));
    }
}
