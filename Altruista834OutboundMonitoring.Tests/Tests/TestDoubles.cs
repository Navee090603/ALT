using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altruista834OutboundMonitoring.Models;
using Altruista834OutboundMonitoring.Services;

namespace Altruista834OutboundMonitoring.Tests.Tests
{
    internal sealed class TestLogger : ILoggingService
    {
        public ConcurrentQueue<string> Entries { get; } = new ConcurrentQueue<string>();
        public void Info(string message) => Entries.Enqueue("INFO:" + message);
        public void Warn(string message) => Entries.Enqueue("WARN:" + message);
        public void Error(string message, Exception ex = null) => Entries.Enqueue("ERROR:" + message + ex?.Message);
    }

    internal sealed class TestEmailService : IEmailService
    {
        public List<EmailRequest> Sent { get; } = new List<EmailRequest>();
        public Task SendAsync(EmailRequest request, CancellationToken cancellationToken)
        {
            lock (Sent)
            {
                Sent.Add(request);
            }

            return Task.CompletedTask;
        }
    }
}
