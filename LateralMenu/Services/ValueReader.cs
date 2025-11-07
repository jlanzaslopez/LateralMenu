using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArchestrA.Client.RuntimeData;   // DataSubscription, DataReferenceSource, VTQ
using ArchestrA.Diagnostics;          // Logger

namespace LateralMenu.Services
{
    /// <summary>
    /// One-shot bulk reader for OMI values using DataSubscription.ReadOnceAsync(DataReferenceSource[]).
    /// Returns a dictionary mapping each input reference to VTQ.Value.ValueAsString.
    /// </summary>
    internal static class ValueReader
    {
        // ===== Logging control =====
        public static bool EnableLogs { get; set; } = false;

        private static void LogInfo(Func<string> f)
        {
            if (EnableLogs && f != null)
                Logger.LogInfo(f);
        }

        private static void LogWarn(Func<string> f)
        {
            if (EnableLogs && f != null)
                Logger.LogWarning(f);
        }

        private static void LogError(Func<string> f, Exception ex = null)
        {
            if (EnableLogs && f != null)
                Logger.LogError(f, ex);
        }

        // ===== Main method =====
        public static async Task<IDictionary<string, object>> ReadBulkAsync(
            DataSubscription subscription,
            IReadOnlyList<string> references)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);

            if (subscription == null || references == null || references.Count == 0)
            {
                LogInfo(() => "ValueReader.ReadBulkAsync: nothing to read (null subscription or empty references).");
                return result;
            }

            LogInfo(() => $"ValueReader.ReadBulkAsync: BEGIN read ({references.Count} refs). Preview=[{string.Join(", ", references.Count > 5 ? references.SubList(0, 5) : references)}{(references.Count > 5 ? ", ..." : "")}]");

            // Build DataReferenceSource[] from reference strings
            var sources = new DataReferenceSource[references.Count];
            for (int i = 0; i < references.Count; i++)
                sources[i] = new DataReferenceSource(references[i]);

            VTQ[] vtqs;
            try
            {
                LogInfo(() => "ValueReader.ReadBulkAsync: invoking ReadOnceAsync...");
                vtqs = await subscription.ReadOnceAsync(sources).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(() => $"ValueReader.ReadBulkAsync: ReadOnceAsync threw: {ex.Message}", ex);
                return result;
            }

            if (vtqs == null)
            {
                LogWarn(() => "ValueReader.ReadBulkAsync: ReadOnceAsync returned null VTQ array.");
                return result;
            }

            // Map by index (OMI returns VTQs in the same order as the sources array)
            int n = Math.Min(references.Count, vtqs.Length);
            int nulls = 0;
            for (int i = 0; i < n; i++)
            {
                string text = null;
                try
                {
                    var vtq = vtqs[i];
                    if (vtq != null && vtq.Value != null)
                    {
                        // Use the canonical string representation from the Value object
                        text = vtq.Value.ValueAsString;
                    }
                }
                catch
                {
                    // Be defensive; leave text as null on any unexpected shape
                }

                if (text == null) nulls++;
                result[references[i]] = text;
            }

            if (vtqs.Length != references.Count)
                LogWarn(() => $"ValueReader.ReadBulkAsync: VTQ length ({vtqs.Length}) != refs length ({references.Count}). Applied {n}.");
            else
                LogInfo(() => $"ValueReader.ReadBulkAsync: applied {n} value(s), null/empty={nulls}.");

            LogInfo(() => "ValueReader.ReadBulkAsync: END read.");
            return result;
        }

        // Helper for preview substring of list (compatible with C# 7.3)
        private static IEnumerable<string> SubList(this IReadOnlyList<string> list, int start, int count)
        {
            var res = new List<string>();
            for (int i = start; i < list.Count && i < start + count; i++)
                res.Add(list[i]);
            return res;
        }
    }
}
