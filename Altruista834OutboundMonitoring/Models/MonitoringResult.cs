using System;

namespace Altruista834OutboundMonitoring.Models
{
    public sealed class MonitoringResult
    {
        public string ProcessName { get; set; }
        public string FileTypeName { get; set; }
        public string StepName { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
