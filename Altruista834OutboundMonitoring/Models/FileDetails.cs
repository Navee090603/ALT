using System;

namespace Altruista834OutboundMonitoring.Models
{
    public sealed class FileDetails
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime LastWriteTime { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPartialCopy { get; set; }
        public bool IsDuplicate { get; set; }
        public bool IsTodayDateValid { get; set; }
        public string Extension { get; set; }

        public double FileSizeMb => Math.Round(FileSizeBytes / 1024d / 1024d, 2);
    }
}
