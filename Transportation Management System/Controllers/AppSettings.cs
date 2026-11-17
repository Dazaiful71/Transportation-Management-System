/* -- FILEHEADER COMMENT --
    FILE        :   AppSettings.cs
    PROJECT     :   Transportation Management System
    DESCRIPTION :   Resolves configuration values without keeping secrets in source control.
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml.Linq;

namespace Transportation_Management_System
{
    ///
    /// \class AppSettings
    ///
    /// \brief Resolves a configuration value from, in order of priority:
    ///   1. an environment variable named TMS_&lt;KEY&gt; (for example TMS_PASSWORD),
    ///   2. appsettings.local.config next to the executable (git-ignored),
    ///   3. the appSettings section of App.config.
    ///
    /// This keeps database passwords out of the repository while still letting the
    /// admin page edit App.config the way it always has.
    ///
    public static class AppSettings
    {
        /// Name of the optional, git-ignored override file.
        public const string LocalFileName = "appsettings.local.config";

        /// Prefix used for environment variable overrides.
        public const string EnvironmentPrefix = "TMS_";

        private static readonly object Sync = new object();
        private static Dictionary<string, string> localValues;

        /// Directory searched for the local override file. Tests can point this elsewhere.
        public static string BaseDirectory { get; set; } = AppContext.BaseDirectory;

        ///
        /// \brief Get a configuration value, or null when it is not set anywhere.
        ///
        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A configuration key is required.", nameof(key));
            }

            string fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentPrefix + key.ToUpperInvariant());
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                return fromEnvironment;
            }

            if (LoadLocalValues().TryGetValue(key, out string fromLocalFile) && !string.IsNullOrEmpty(fromLocalFile))
            {
                return fromLocalFile;
            }

            string fromAppConfig = ConfigurationManager.AppSettings.Get(key);
            return string.IsNullOrEmpty(fromAppConfig) ? null : fromAppConfig;
        }

        ///
        /// \brief Get a configuration value, falling back to a default when it is missing.
        ///
        public static string Get(string key, string defaultValue)
        {
            return Get(key) ?? defaultValue;
        }

        ///
        /// \brief Get an integer configuration value, falling back to a default when missing or invalid.
        ///
        public static int GetInt(string key, int defaultValue)
        {
            return int.TryParse(Get(key), out int value) ? value : defaultValue;
        }

        ///
        /// \brief Forget cached values so the local override file is read again.
        ///
        public static void Reload()
        {
            lock (Sync)
            {
                localValues = null;
            }
        }

        ///
        /// \brief Parse an appSettings-style XML document into key/value pairs.
        ///
        /// Accepts either a full &lt;configuration&gt; document or a bare &lt;appSettings&gt; element.
        ///
        public static Dictionary<string, string> ParseAppSettingsXml(string xml)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(xml))
            {
                return values;
            }

            XDocument document = XDocument.Parse(xml);
            foreach (XElement add in document.Descendants("add"))
            {
                string key = (string)add.Attribute("key");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    values[key] = (string)add.Attribute("value") ?? string.Empty;
                }
            }

            return values;
        }

        private static Dictionary<string, string> LoadLocalValues()
        {
            lock (Sync)
            {
                if (localValues != null)
                {
                    return localValues;
                }

                string path = Path.Combine(BaseDirectory ?? string.Empty, LocalFileName);
                try
                {
                    localValues = File.Exists(path)
                        ? ParseAppSettingsXml(File.ReadAllText(path))
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception e)
                {
                    // A broken override file should not stop the app; App.config still applies.
                    Logger.Log($"Could not read {LocalFileName}: {e.Message}", LogLevel.Warning);
                    localValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                return localValues;
            }
        }
    }
}
