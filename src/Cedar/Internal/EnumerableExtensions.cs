﻿// ReSharper disable once CheckNamespace
namespace System.Collections.Generic
{
    internal static class EnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach(var item in source)
            {
                action(item);
            }
        }
    }
}