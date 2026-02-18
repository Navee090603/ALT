using System;
using Altruista834OutboundMonitoring.Config;
using Altruista834OutboundMonitoring.Models;

namespace Altruista834OutboundMonitoring.Services
{
    public interface ISLAService
    {
        DateTime EstimateCompletion(DateTime startTimeLocal, FileDetails fileDetails);
        bool IsSlaAtRisk(DateTime estimatedCompletionLocal, DateTime deadlineLocal);
    }

    public sealed class SLAService : ISLAService
    {
        private readonly AppConfig _config;

        public SLAService(AppConfig config)
        {
            _config = config;
        }

        public DateTime EstimateCompletion(DateTime startTimeLocal, FileDetails fileDetails)
        {
            var mbPerHour = _config.MbPerHourProcessingRatio <= 0 ? 250d : _config.MbPerHourProcessingRatio;
            var hours = Math.Max(0.01, fileDetails.FileSizeMb / mbPerHour);
            return startTimeLocal.AddHours(hours);
        }

        public bool IsSlaAtRisk(DateTime estimatedCompletionLocal, DateTime deadlineLocal)
        {
            return estimatedCompletionLocal > deadlineLocal;
        }
    }
}
