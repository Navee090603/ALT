using System;
using System.IO;
using Newtonsoft.Json;
using Altruista834OutboundMonitoring.Services;

namespace Altruista834OutboundMonitoring.Config
{
    public sealed class ConfigLoader
    {
        private readonly ILoggingService _logger;

        public ConfigLoader(ILoggingService logger)
        {
            _logger = logger;
        }

        public AppConfig Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Config path is required.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Config file not found.", path);
            }

            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonConvert.DeserializeObject<AppConfig>(json);
                if (cfg == null)
                {
                    throw new InvalidDataException("Config JSON deserialized to null.");
                }

                return cfg;
            }
            catch (JsonException ex)
            {
                _logger.Error("Invalid JSON config.", ex);
                throw;
            }
        }
    }
}
