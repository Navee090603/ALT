using System;
using System.Collections.Generic;

namespace Altruista834OutboundMonitoring.Config
{
    public sealed class AppConfig
    {
        public bool DemoMode { get; set; }
        public string TimeZoneId { get; set; } = "India Standard Time";
        public int PollIntervalSeconds { get; set; } = 30;
        public int StabilityCheckIntervalSeconds { get; set; } = 15;
        public int StabilityCheckAttempts { get; set; } = 3;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 5;
        public int StuckFileThresholdMinutes { get; set; } = 10;
        public double MbPerHourProcessingRatio { get; set; } = 250d;
        public string LogFilePath { get; set; } = "logs/monitor.log";
        public FolderSettings Folders { get; set; } = new FolderSettings();
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();
        public EmailGroups EmailGroups { get; set; } = new EmailGroups();
        public Dictionary<string, StepWindow> TimeWindows { get; set; } = new Dictionary<string, StepWindow>();
        public Dictionary<string, ProcessFileSettings> Processes { get; set; } = new Dictionary<string, ProcessFileSettings>();
    }

    public sealed class FolderSettings
    {
        public string VendorExtractUtility { get; set; }
        public string Proprietary { get; set; }
        public string Hold { get; set; }
        public string Drop { get; set; }
    }

    public sealed class SmtpSettings
    {
        public string Host { get; set; }
        public int Port { get; set; } = 25;
        public bool EnableSsl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string From { get; set; }
        public int TimeoutMilliseconds { get; set; } = 15000;
    }

    public sealed class EmailGroups
    {
        public List<string> ItOps { get; set; } = new List<string>();
        public List<string> InternalTeam { get; set; } = new List<string>();
        public List<string> Client { get; set; } = new List<string>();
    }

    public sealed class StepWindow
    {
        public string Start { get; set; }
        public string End { get; set; }
        public string Deadline { get; set; }
    }

    public sealed class ProcessFileSettings
    {
        public string SearchPattern { get; set; }
        public bool RequireTodayDateInName { get; set; } = true;
        public string DateFormat { get; set; } = "yyyyMMdd";
    }
}
