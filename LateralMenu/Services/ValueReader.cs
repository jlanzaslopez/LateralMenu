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
        public static async Task<IDictionary<string, object>> ReadBulkAsync(
            DataSubscription subscription,
            IReadOnlyList<string> references)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);

            if (subscription == null || references == null || references.Count == 0)
            {
                Logger.LogInfo(() => "ValueReader.ReadBulkAsync: nothing to read (null subscription or empty references).");
                return result;
            }

            // Build DataReferenceSource[] from reference strings
            var sources = new DataReferenceSource[references.Count];
            for (int i = 0; i < references.Count; i++)
                sources[i] = new DataReferenceSource(references[i]);

            VTQ[] vtqs;
            try
            {
                vtqs = await subscription.ReadOnceAsync(sources).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(() => $"ValueReader.ReadBulkAsync: ReadOnceAsync threw: {ex.Message}", ex);
                return result;
            }

            if (vtqs == null)
            {
                Logger.LogWarning(() => "ValueReader.ReadBulkAsync: ReadOnceAsync returned null VTQ array.");
                return result;
            }

            // Map by index (OMI returns VTQs in the same order as the sources array)
            int n = Math.Min(references.Count, vtqs.Length);
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

                result[references[i]] = text;
            }

            if (vtqs.Length != references.Count)
            {
                Logger.LogWarning(() => $"ValueReader.ReadBulkAsync: VTQ length ({vtqs.Length}) != refs length ({references.Count}). Applied {n}.");
            }
            else
            {
                Logger.LogInfo(() => $"ValueReader.ReadBulkAsync: applied {n} value(s).");
            }

            return result;
        }
    }
}
