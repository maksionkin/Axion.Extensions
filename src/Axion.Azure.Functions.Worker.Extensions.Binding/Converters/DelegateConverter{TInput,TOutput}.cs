// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters;

class DelegateConverter<TInput, TOutput>(Func<TInput, TOutput> convert) : IConverter<TInput, TOutput>
{
    public TOutput Convert(TInput input) => convert(input);
}
