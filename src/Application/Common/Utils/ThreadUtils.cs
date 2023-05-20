﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Common.Utils;
/// <summary>
///     Process a set of tasks using Async parallelism.
/// </summary>
public static class ThreadUtils
{
    public static async Task ExecuteInParallel<T>(this IEnumerable<T> collection,
        Func<T, Task> processor, int degreeOfParallelism)
    {
        var queue = new ConcurrentQueue<T>(collection);

        await queue.ExecuteInParallel(processor, degreeOfParallelism);
    }

    public static async Task ExecuteInParallel<T>(this ConcurrentQueue<T> queue,
        Func<T, Task> processor, int degreeOfParallelism)
    {
        var tasks = Enumerable.Range(0, degreeOfParallelism)
            .Select(async _ =>
            {
                while (queue.TryDequeue(out var item)) await processor(item).ConfigureAwait(false);
                // await Task.Delay(100); // Don't thrash.
            });
        await Task.WhenAll(tasks);
    }
}
