using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Altruista834OutboundMonitoring.Services;

namespace Altruista834OutboundMonitoring.Tests.Tests
{
    public class EndToEndSimulationTests
    {
        [Test]
        public async Task DropFiles_ShouldGenerateSummaryEmail()
        {
            var root = Path.Combine(Path.GetTempPath(), "AltruistaE2E", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "vendor"));
            Directory.CreateDirectory(Path.Combine(root, "prop"));
            Directory.CreateDirectory(Path.Combine(root, "hold"));
            Directory.CreateDirectory(Path.Combine(root, "drop"));

            var cfg = FileMonitoringTests.TestConfig(root);
            var logger = new TestLogger();
            var email = new TestEmailService();

            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            File.WriteAllText(Path.Combine(root, "vendor", $"out_C_{today}.txt"), "ok");
            File.WriteAllText(Path.Combine(root, "prop", $"out_C_{today}.txt"), new string('A', 1024));
            File.WriteAllText(Path.Combine(root, "hold", $"out_C_{today}.x12"), "ISA*00*");
            File.WriteAllText(Path.Combine(root, "drop", $"out_C_{today}.x12"), "ISA*00*");

            var monitor = new FileMonitoringService(cfg, logger, email, new SLAService(cfg), new RetryService());

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
            {
                try { await monitor.StartAsync(cts.Token); } catch (OperationCanceledException) { }
            }

            Assert.That(email.Sent.Any(x => x.Subject.Contains("summary", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }
}
