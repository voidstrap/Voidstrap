﻿namespace Voidstrap.Models
{
    internal class WatcherData
    {
        public int ProcessId { get; set; }

        public string? LogFile { get; set; }

        public List<int>? AutoclosePids { get; set; }
    }
}
