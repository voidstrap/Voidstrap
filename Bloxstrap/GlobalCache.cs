using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Voidstrap.AppData
{
    public static class GlobalCache
    {
        public static readonly ConcurrentDictionary<string, string?> ServerLocation = new();
        public static readonly ConcurrentDictionary<string, DateTime> ServerTime = new();
    }
}
