using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Altruista834OutboundMonitoring.Config;
using Altruista834OutboundMonitoring.Services;

namespace Altruista834OutboundMonitoring
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var bootstrapLogger = new LoggingService("logs/bootstrap.log");
            AppConfig config;
            try
            {
                var configPath = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                var loader = new ConfigLoader(bootstrapLogger);
                config = loader.Load(configPath);
            }
            catch (Exception ex)
            {
                bootstrapLogger.Error("Startup failed due to config issue.", ex);
                return -1;
            }

            var logger = new LoggingService(config.LogFilePath);
            var retry = new RetryService();
            var email = new EmailService(config, logger);
            var sla = new SLAService(config);
            var monitor = new FileMonitoringService(config, logger, email, sla, retry);

            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    logger.Warn("Graceful shutdown requested.");
                    cts.Cancel();
                };

                AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
                {
                    logger.Error("Unhandled exception captured.", eventArgs.ExceptionObject as Exception);
                };

                logger.Info("Altruista834OutboundMonitoring started.");
                try
                {
                    await monitor.StartAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    logger.Info("Monitoring stopped by cancellation.");
                }
                catch (Exception ex)
                {
                    logger.Error("Fatal runtime exception.", ex);
                }
            }

            return 0;
        }
    }
}
