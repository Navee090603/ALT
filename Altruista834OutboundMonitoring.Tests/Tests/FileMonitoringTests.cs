using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Altruista834OutboundMonitoring.Config;
using Altruista834OutboundMonitoring.Services;

namespace Altruista834OutboundMonitoring.Tests.Tests
{
    public class FileMonitoringTests
    {
        [Test]
        public async Task MissingFiles_ShouldTriggerEmailNotifications()
        {
            var root = Path.Combine(Path.GetTempPath(), "AltruistaTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "vendor"));
            Directory.CreateDirectory(Path.Combine(root, "prop"));
            Directory.CreateDirectory(Path.Combine(root, "hold"));
            Directory.CreateDirectory(Path.Combine(root, "drop"));

            var cfg = TestConfig(root);
            var logger = new TestLogger();
            var email = new TestEmailService();
            var monitor = new FileMonitoringService(cfg, logger, email, new SLAService(cfg), new RetryService());

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try { await monitor.StartAsync(cts.Token); } catch (OperationCanceledException) { }
            }

            Assert.That(email.Sent.Any(x => x.Subject.Contains("missing", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        internal static AppConfig TestConfig(string root)
        {
            return new AppConfig
            {
                DemoMode = true,
                TimeZoneId = "UTC",
                PollIntervalSeconds = 1,
                StabilityCheckAttempts = 1,
                StabilityCheckIntervalSeconds = 1,
                StuckFileThresholdMinutes = 1,
                Folders = new FolderSettings
                {
                    VendorExtractUtility = Path.Combine(root, "vendor"),
                    Proprietary = Path.Combine(root, "prop"),
                    Hold = Path.Combine(root, "hold"),
                    Drop = Path.Combine(root, "drop")
                },
                EmailGroups = new EmailGroups
                {
                    ItOps = { "it@local" },
                    InternalTeam = { "internal@local" },
                    Client = { "client@local" }
                },
                TimeWindows =
                {
                    ["Step1"] = new StepWindow { Start = "00:00:00", End = "23:59:59" },
                    ["Step2"] = new StepWindow { Start = "00:00:00", End = "23:59:59", Deadline = "00:00:01" },
                    ["Step3"] = new StepWindow { Start = "00:00:00", End = "23:59:59" },
                    ["Step4"] = new StepWindow { Start = "00:00:00", End = "23:59:59", Deadline = "00:00:01" }
                },
                Processes =
                {
                    ["C"] = new ProcessFileSettings { SearchPattern = "*C*.txt", RequireTodayDateInName = false },
                    ["Pend_C"] = new ProcessFileSettings { SearchPattern = "*Pend_C*.txt", RequireTodayDateInName = false }
                }
            };
        }
    }
}
