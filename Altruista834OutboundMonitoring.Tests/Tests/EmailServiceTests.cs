using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Altruista834OutboundMonitoring.Config;
using Altruista834OutboundMonitoring.Models;
using Altruista834OutboundMonitoring.Services;

namespace Altruista834OutboundMonitoring.Tests.Tests
{
    public class EmailServiceTests
    {
        [Test]
        public async Task SendAsync_ShouldNotThrow_OnSmtpFailure()
        {
            var cfg = new AppConfig
            {
                Smtp = new SmtpSettings
                {
                    Host = "invalid.host.local",
                    Port = 25,
                    From = "from@local",
                    TimeoutMilliseconds = 100
                }
            };

            var logger = new TestLogger();
            var svc = new EmailService(cfg, logger);

            Assert.DoesNotThrowAsync(async () =>
            {
                await svc.SendAsync(new EmailRequest
                {
                    Subject = "test",
                    Body = "body",
                    To = { "to@local" },
                    DeduplicationKey = "key1"
                }, CancellationToken.None);
            });
        }

        [Test]
        public async Task SendAsync_ShouldDeduplicateKey()
        {
            var cfg = new AppConfig
            {
                Smtp = new SmtpSettings
                {
                    Host = "invalid.host.local",
                    Port = 25,
                    From = "from@local",
                    TimeoutMilliseconds = 100
                }
            };
            var logger = new TestLogger();
            var svc = new EmailService(cfg, logger);

            var req = new EmailRequest { Subject = "sub", Body = "body", DeduplicationKey = "dup", To = { "to@local" } };
            await svc.SendAsync(req, CancellationToken.None);
            await svc.SendAsync(req, CancellationToken.None);

            Assert.That(logger.Entries, Has.Some.Contains("Duplicate email avoided"));
        }
    }
}
