using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Altruista834OutboundMonitoring.Config;
using Altruista834OutboundMonitoring.Models;

namespace Altruista834OutboundMonitoring.Services
{
    public interface IFileMonitoringService
    {
        Task StartAsync(CancellationToken cancellationToken);
    }

    public sealed class FileMonitoringService : IFileMonitoringService
    {
        private readonly AppConfig _config;
        private readonly ILoggingService _logger;
        private readonly IEmailService _email;
        private readonly ISLAService _slaService;
        private readonly IRetryService _retry;
        private readonly TimeZoneInfo _tz;
        private readonly ConcurrentDictionary<string, DateTime> _firstSeen = new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, bool> _stepDone = new ConcurrentDictionary<string, bool>();

        public FileMonitoringService(AppConfig config, ILoggingService logger, IEmailService email, ISLAService slaService, IRetryService retry)
        {
            _config = config;
            _logger = logger;
            _email = email;
            _slaService = slaService;
            _retry = retry;
            _tz = TimeZoneInfo.FindSystemTimeZoneById(_config.TimeZoneId);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            EnsureFolders();
            using (CreateWatcher(_config.Folders.VendorExtractUtility))
            using (CreateWatcher(_config.Folders.Proprietary))
            using (CreateWatcher(_config.Folders.Hold))
            using (CreateWatcher(_config.Folders.Drop))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        foreach (var process in _config.Processes.Keys)
                        {
                            await ExecuteForProcessAsync(process, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Monitoring loop error. Continuing execution.", ex);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task ExecuteForProcessAsync(string processName, CancellationToken ct)
        {
            var cfg = _config.Processes[processName];
            await Step1VendorTxtAsync(processName, cfg, ct).ConfigureAwait(false);
            await Step2ProprietaryAsync(processName, cfg, ct).ConfigureAwait(false);
            await Step3HoldAsync(processName, cfg, ct).ConfigureAwait(false);
            await Step4DropAsync(processName, cfg, ct).ConfigureAwait(false);
        }

        private async Task Step1VendorTxtAsync(string process, ProcessFileSettings fileSettings, CancellationToken ct)
        {
            if (!IsInWindow("Step1")) return;

            var files = await SafeGetFilesAsync(_config.Folders.VendorExtractUtility, fileSettings.SearchPattern, ct).ConfigureAwait(false);
            var found = PickTodayFile(files, fileSettings);
            if (found == null)
            {
                await SendOnceAsync($"Step1Missing-{process}-{TodayKey()}", _config.EmailGroups.ItOps,
                    $"[{process}] Step 1 file missing",
                    $"No txt file found in VendorExtractUtility for {process} by current checkpoint.", ct).ConfigureAwait(false);
                return;
            }

            await ValidateStuckAndStabilityAsync(process, "Step1", found, _config.EmailGroups.ItOps, ct).ConfigureAwait(false);
        }

        private async Task Step2ProprietaryAsync(string process, ProcessFileSettings fileSettings, CancellationToken ct)
        {
            if (!IsInWindow("Step2")) return;

            var files = await SafeGetFilesAsync(_config.Folders.Proprietary, fileSettings.SearchPattern, ct).ConfigureAwait(false);
            var found = PickTodayFile(files, fileSettings);
            if (found == null)
            {
                await SendOnceAsync($"Step2Missing-{process}-{TodayKey()}", _config.EmailGroups.InternalTeam,
                    $"[{process}] Step 2 file missing",
                    $"No proprietary file found for {process}. Possible upstream/Tidal issue.", ct).ConfigureAwait(false);
                return;
            }

            await ValidateStuckAndStabilityAsync(process, "Step2", found, _config.EmailGroups.InternalTeam, ct).ConfigureAwait(false);

            var deadline = ResolveDeadline("Step2", TimeSpan.FromHours(10));
            var estimate = _slaService.EstimateCompletion(GetNowLocal(), found);
            if (_slaService.IsSlaAtRisk(estimate, deadline))
            {
                var body = $"Estimated completion {estimate:yyyy-MM-dd HH:mm:ss} exceeds SLA deadline {deadline:yyyy-MM-dd HH:mm:ss}.";
                await SendOnceAsync($"Step2SlaRiskInternal-{process}-{TodayKey()}", _config.EmailGroups.InternalTeam,
                    $"[{process}] SLA at risk - raise incident", body, ct).ConfigureAwait(false);
                await SendOnceAsync($"Step2SlaRiskClient-{process}-{TodayKey()}", _config.EmailGroups.Client,
                    $"[{process}] SLA breach communication", body, ct).ConfigureAwait(false);
            }
        }

        private async Task Step3HoldAsync(string process, ProcessFileSettings fileSettings, CancellationToken ct)
        {
            if (!IsInWindow("Step3")) return;

            var files = await SafeGetFilesAsync(_config.Folders.Hold, fileSettings.SearchPattern, ct).ConfigureAwait(false);
            var found = PickTodayFile(files.Where(f => f.Extension.Equals(".x12", StringComparison.OrdinalIgnoreCase)).ToArray(), fileSettings);
            if (found == null)
            {
                await SendOnceAsync($"Step3Missing-{process}-{TodayKey()}", _config.EmailGroups.InternalTeam,
                    $"[{process}] HOLD X12 missing",
                    $"No X12 file found in HOLD for {process}.", ct).ConfigureAwait(false);
                return;
            }

            await ValidateStuckAndStabilityAsync(process, "Step3", found, _config.EmailGroups.InternalTeam, ct).ConfigureAwait(false);
        }

        private async Task Step4DropAsync(string process, ProcessFileSettings fileSettings, CancellationToken ct)
        {
            var deadline = ResolveDeadline("Step4", new TimeSpan(9, 55, 0));
            var files = await SafeGetFilesAsync(_config.Folders.Drop, fileSettings.SearchPattern, ct).ConfigureAwait(false);
            var found = PickTodayFile(files.Where(f => f.Extension.Equals(".x12", StringComparison.OrdinalIgnoreCase)).ToArray(), fileSettings);
            if (found == null && GetNowLocal() > deadline)
            {
                await SendOnceAsync($"Step4Missing-{process}-{TodayKey()}", _config.EmailGroups.InternalTeam,
                    $"[{process}] DROP X12 missing before deadline",
                    $"No DROP file found before SLA cutoff {deadline:HH:mm:ss}.", ct).ConfigureAwait(false);
                return;
            }

            if (found != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("DROP file summary");
                sb.AppendLine($"File Name: {found.FileName}");
                sb.AppendLine($"File Size MB: {found.FileSizeMb}");
                sb.AppendLine($"Date Modified: {found.LastWriteTime:yyyy-MM-dd}");
                sb.AppendLine($"Time Modified: {found.LastWriteTime:HH:mm:ss}");
                _logger.Info($"DROP summary => {sb.ToString().Replace(Environment.NewLine, " | ")}");

                await SendOnceAsync($"Step4Summary-{process}-{TodayKey()}", _config.EmailGroups.InternalTeam,
                    $"[{process}] DROP file summary", sb.ToString(), ct).ConfigureAwait(false);
            }
        }

        private async Task ValidateStuckAndStabilityAsync(string process, string step, FileDetails file, IList<string> recipients, CancellationToken ct)
        {
            var key = $"{step}-{process}-{file.FileName}";
            var firstSeen = _firstSeen.GetOrAdd(key, _ => GetNowLocal());
            if (GetNowLocal() - firstSeen > TimeSpan.FromMinutes(_config.StuckFileThresholdMinutes))
            {
                await SendOnceAsync($"{step}Stuck-{process}-{file.FileName}-{TodayKey()}", recipients,
                    $"[{process}] {step} file stuck", $"File {file.FileName} appears stuck for over threshold.", ct).ConfigureAwait(false);
            }

            var stable = await IsStableAsync(file.FullPath, ct).ConfigureAwait(false);
            var lockState = IsLocked(file.FullPath);
            if (!stable || lockState)
            {
                await SendOnceAsync($"{step}Partial-{process}-{file.FileName}-{TodayKey()}", recipients,
                    $"[{process}] {step} partial/locked file",
                    $"File {file.FileName} is not stable yet (stable={stable}, locked={lockState}).", ct).ConfigureAwait(false);
            }
            else
            {
                _stepDone[key] = true;
            }
        }

        private async Task<FileDetails[]> SafeGetFilesAsync(string folder, string pattern, CancellationToken ct)
        {
            try
            {
                return await _retry.ExecuteAsync(() => Task.FromResult(GetFiles(folder, pattern)), _config.MaxRetries,
                    TimeSpan.FromSeconds(_config.RetryDelaySeconds), _logger, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"Folder access failure: {folder}", ex);
                return Array.Empty<FileDetails>();
            }
        }

        private FileDetails[] GetFiles(string folder, string pattern)
        {
            if (!Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException(folder);
            }

            return Directory.GetFiles(folder, pattern)
                .Select(p => new FileInfo(p))
                .Select(f => new FileDetails
                {
                    FileName = f.Name,
                    FullPath = f.FullName,
                    FileSizeBytes = f.Length,
                    LastWriteTime = f.LastWriteTime,
                    Extension = f.Extension
                }).ToArray();
        }

        private FileDetails PickTodayFile(IEnumerable<FileDetails> files, ProcessFileSettings settings)
        {
            var todayToken = GetNowLocal().ToString(settings.DateFormat, CultureInfo.InvariantCulture);
            return files
                .Where(f => !settings.RequireTodayDateInName || f.FileName.Contains(todayToken))
                .GroupBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.LastWriteTime).First())
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
        }

        private async Task<bool> IsStableAsync(string path, CancellationToken ct)
        {
            long previous = -1;
            for (var i = 0; i < _config.StabilityCheckAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                var current = new FileInfo(path).Length;
                if (previous == current && current > 0)
                {
                    return true;
                }

                previous = current;
                await Task.Delay(TimeSpan.FromSeconds(_config.StabilityCheckIntervalSeconds), ct).ConfigureAwait(false);
            }

            return false;
        }

        private bool IsLocked(string path)
        {
            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return false;
                }
            }
            catch
            {
                return true;
            }
        }

        private async Task SendOnceAsync(string key, IList<string> to, string subject, string body, CancellationToken ct)
        {
            await _email.SendAsync(new EmailRequest
            {
                To = to,
                Subject = subject,
                Body = body,
                DeduplicationKey = key
            }, ct).ConfigureAwait(false);
        }

        private bool IsInWindow(string step)
        {
            if (!_config.TimeWindows.TryGetValue(step, out var w))
            {
                return true;
            }

            var now = GetNowLocal().TimeOfDay;
            var start = TimeSpan.Parse(w.Start);
            var end = TimeSpan.Parse(w.End);
            return now >= start && now <= end;
        }

        private DateTime ResolveDeadline(string step, TimeSpan fallback)
        {
            if (_config.TimeWindows.TryGetValue(step, out var w) && !string.IsNullOrWhiteSpace(w.Deadline))
            {
                return GetNowLocal().Date.Add(TimeSpan.Parse(w.Deadline));
            }

            return GetNowLocal().Date.Add(fallback);
        }

        private DateTime GetNowLocal() => TimeZoneInfo.ConvertTime(DateTime.UtcNow, _tz);

        private string TodayKey() => GetNowLocal().ToString("yyyyMMdd");

        private void EnsureFolders()
        {
            EnsureFolder(_config.Folders.VendorExtractUtility);
            EnsureFolder(_config.Folders.Proprietary);
            EnsureFolder(_config.Folders.Hold);
            EnsureFolder(_config.Folders.Drop);
        }

        private void EnsureFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                _logger.Error($"Cannot initialize folder {path}", ex);
            }
        }

        private FileSystemWatcher CreateWatcher(string path)
        {
            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            watcher.Created += (s, e) => _logger.Info($"File created event: {e.FullPath}");
            watcher.Changed += (s, e) => _logger.Info($"File changed event: {e.FullPath}");
            watcher.Error += (s, e) => _logger.Error($"Watcher error for {path}", e.GetException());
            return watcher;
        }
    }
}
