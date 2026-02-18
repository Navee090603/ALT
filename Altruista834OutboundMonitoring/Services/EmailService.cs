using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altruista834OutboundMonitoring.Config;
using Altruista834OutboundMonitoring.Models;

namespace Altruista834OutboundMonitoring.Services
{
    public interface IEmailService
    {
        Task SendAsync(EmailRequest request, CancellationToken cancellationToken);
    }

    public sealed class EmailService : IEmailService
    {
        private readonly AppConfig _config;
        private readonly ILoggingService _logger;
        private readonly ConcurrentDictionary<string, DateTime> _sent = new ConcurrentDictionary<string, DateTime>();

        public EmailService(AppConfig config, ILoggingService logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(EmailRequest request, CancellationToken cancellationToken)
        {
            if (request == null || request.To == null || !request.To.Any())
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(request.DeduplicationKey) && !_sent.TryAdd(request.DeduplicationKey, DateTime.UtcNow))
            {
                _logger.Info($"Duplicate email avoided for key={request.DeduplicationKey}");
                return;
            }

            try
            {
                using (var message = new MailMessage())
                using (var client = new SmtpClient(_config.Smtp.Host, _config.Smtp.Port))
                {
                    message.From = new MailAddress(_config.Smtp.From);
                    foreach (var to in request.To) message.To.Add(to);
                    foreach (var cc in request.Cc) message.CC.Add(cc);
                    message.Subject = request.Subject;
                    message.Body = request.Body;
                    message.BodyEncoding = Encoding.UTF8;

                    client.EnableSsl = _config.Smtp.EnableSsl;
                    client.Timeout = _config.Smtp.TimeoutMilliseconds;
                    if (!string.IsNullOrWhiteSpace(_config.Smtp.UserName))
                    {
                        client.Credentials = new NetworkCredential(_config.Smtp.UserName, _config.Smtp.Password);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await client.SendMailAsync(message).ConfigureAwait(false);
                    _logger.Info($"Email sent: {request.Subject}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"SMTP failure for subject '{request?.Subject}'", ex);
            }
        }
    }
}
