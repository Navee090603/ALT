using System;
using NUnit.Framework;
using Altruista834OutboundMonitoring.Config;
using Altruista834OutboundMonitoring.Models;
using Altruista834OutboundMonitoring.Services;

namespace Altruista834OutboundMonitoring.Tests.Tests
{
    public class SLAServiceTests
    {
        [Test]
        public void EstimateCompletion_ShouldScaleWithFileSize()
        {
            var config = new AppConfig { MbPerHourProcessingRatio = 100 };
            var svc = new SLAService(config);
            var file = new FileDetails { FileSizeBytes = 200L * 1024L * 1024L };
            var start = new DateTime(2025, 1, 1, 6, 0, 0);

            var estimate = svc.EstimateCompletion(start, file);

            Assert.That((estimate - start).TotalHours, Is.EqualTo(2d).Within(0.01));
        }

        [Test]
        public void IsSlaAtRisk_ShouldReturnTrue_WhenAfterDeadline()
        {
            var svc = new SLAService(new AppConfig());
            var risk = svc.IsSlaAtRisk(DateTime.Today.AddHours(10.5), DateTime.Today.AddHours(10));
            Assert.That(risk, Is.True);
        }
    }
}
