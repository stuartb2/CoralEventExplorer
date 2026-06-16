#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using ServiceBusExplorer.Helpers;

#endregion

namespace ServiceBusExplorer.UIHelpers
{
    /// <summary>
    /// Coral: orders the saved-connection list so pinned favourites appear first,
    /// then recently used connections, then the rest alphabetically (no duplicates).
    /// Pinned and recent lists are persisted in the configuration file.
    /// </summary>
    internal static class CoralConnectionList
    {
        const string PinnedKey = "coralPinnedConnections";
        const string RecentKey = "coralRecentConnections";
        const char Delimiter = '|';
        const int MaxRecent = 8;

        internal sealed class OrderedConnections
        {
            public List<string> Keys = new List<string>();
            public HashSet<string> Pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Recent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        internal static OrderedConnections Order(IEnumerable<string> allKeys, ConfigFileUse configFileUse)
        {
            var result = new OrderedConnections();
            var all = allKeys?.ToList() ?? new List<string>();
            var allSet = new HashSet<string>(all, StringComparer.OrdinalIgnoreCase);

            var pinned = Read(configFileUse, PinnedKey)
                .Where(allSet.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            result.Pinned = new HashSet<string>(pinned, StringComparer.OrdinalIgnoreCase);

            var recent = Read(configFileUse, RecentKey)
                .Where(k => allSet.Contains(k) && !result.Pinned.Contains(k))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            result.Recent = new HashSet<string>(recent, StringComparer.OrdinalIgnoreCase);

            var used = new HashSet<string>(pinned.Concat(recent), StringComparer.OrdinalIgnoreCase);
            var rest = all.Where(k => !used.Contains(k)).OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

            result.Keys = pinned.Concat(recent).Concat(rest).ToList();
            return result;
        }

        internal static bool IsPinned(ConfigFileUse configFileUse, string key)
        {
            return Read(configFileUse, PinnedKey).Contains(key, StringComparer.OrdinalIgnoreCase);
        }

        internal static void TogglePin(ConfigFileUse configFileUse, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            var pinned = Read(configFileUse, PinnedKey);
            var existing = pinned.FirstOrDefault(p => string.Equals(p, key, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                pinned.Remove(existing);
            }
            else
            {
                pinned.Add(key);
            }
            Write(configFileUse, PinnedKey, pinned);
        }

        internal static void RecordRecent(ConfigFileUse configFileUse, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            var recent = Read(configFileUse, RecentKey);
            recent.RemoveAll(r => string.Equals(r, key, StringComparison.OrdinalIgnoreCase));
            recent.Insert(0, key);
            if (recent.Count > MaxRecent)
            {
                recent = recent.Take(MaxRecent).ToList();
            }
            Write(configFileUse, RecentKey, recent);
        }

        internal static void Remove(ConfigFileUse configFileUse, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            foreach (var listKey in new[] { PinnedKey, RecentKey })
            {
                var values = Read(configFileUse, listKey);
                if (values.RemoveAll(v => string.Equals(v, key, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    Write(configFileUse, listKey, values);
                }
            }
        }

        static List<string> Read(ConfigFileUse configFileUse, string key)
        {
            try
            {
                var configuration = TwoFilesConfiguration.Create(configFileUse);
                var raw = configuration.GetStringValue(key, string.Empty);
                return string.IsNullOrEmpty(raw)
                    ? new List<string>()
                    : raw.Split(Delimiter).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        static void Write(ConfigFileUse configFileUse, string key, List<string> values)
        {
            try
            {
                var configuration = TwoFilesConfiguration.Create(configFileUse);
                configuration.SetValue(key, string.Join(Delimiter.ToString(), values));
                configuration.Save();
            }
            catch (Exception)
            {
                // Persisting the order is best-effort.
            }
        }
    }
}
