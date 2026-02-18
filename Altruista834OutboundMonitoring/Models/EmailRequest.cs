using System.Collections.Generic;

namespace Altruista834OutboundMonitoring.Models
{
    public sealed class EmailRequest
    {
        public string Subject { get; set; }
        public string Body { get; set; }
        public IList<string> To { get; set; } = new List<string>();
        public IList<string> Cc { get; set; } = new List<string>();
        public string DeduplicationKey { get; set; }
    }
}
