using ArchestrA.Client.Navigation;       // NavigationModel, NavigationItem
using ArchestrA.Client.RuntimeData;      // DataSubscription
using ArchestrA.Diagnostics;             // Logger
using LateralMenu.Models;                // TreeNode
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;                    // Application
using System.Windows.Threading;          // Dispatcher

namespace LateralMenu.Services
{
    public enum RootMode
    {
        None,
        Ambito,
        Sistema,
        Generico
    }

    public sealed class TreeBootstrapService : ITreeBootstrapService
    {

        // Dispatcher de UI inyectado por el host (LateralMenuControl)
        public System.Windows.Threading.Dispatcher UIDispatcher { get; set; }

        // ======= Logs =======
        public bool EnableLogs { get; set; }  // lo setea el control padre

        private void SvcLogInfo(Func<string> f)
        {
            if (EnableLogs && f != null) Logger.LogInfo(f);
        }
        private void SvcLogWarn(Func<string> f)
        {
            if (EnableLogs && f != null) Logger.LogWarning(f);
        }
        private void SvcLogError(Func<string> f, Exception ex = null)
        {
            if (EnableLogs && f != null) Logger.LogError(f, ex);
        }

        // ======= Estado público =======
        public ObservableCollection<TreeNode> Roots { get; } = new ObservableCollection<TreeNode>();
        public string AttributeName { get; set; } = string.Empty;
        public Func<DataSubscription, IReadOnlyList<string>, Task<IDictionary<string, object>>> OneShotReaderAsync { get; set; }
        public bool IsTreeLoaded => Roots.Count > 0;
        public RootMode LastRootMode { get; set; } = RootMode.None;




        // ======= Carga / Proyección =======
        public async Task LoadAndProject(string startPath, string userRolesCsv, DataSubscription subscription)
        {
            // PRE: limpiar estado UI en el hilo correcto
            OnUi(() =>
            {
                try { DisposeRoots(); } catch { /* best effort */ }
                Roots.Clear();
                LastRootMode = RootMode.None;
            });

            if (string.IsNullOrWhiteSpace(startPath))
            {
                Logger.LogWarning(() => "LoadAndProject: startPath vacío.");
                return;
            }

            var model = NavigationModel.ViewAppNavigationModel;
            if (model == null)
            {
                Logger.LogWarning(() => "LoadAndProject: NavigationModel.ViewAppNavigationModel es NULL.");
                return;
            }

            var rootItem = model.GetItemBypath(startPath);
            if (rootItem == null)
            {
                Logger.LogWarning(() => $"LoadAndProject: path no encontrado: '{startPath}'.");
                return;
            }

            // 1) Decidir modo según value del root
            string rootValue = await TryReadSingleValueAsync(subscription, rootItem?.Name).ConfigureAwait(false);
            var v = (rootValue ?? string.Empty).Trim().ToLowerInvariant();
            if (v == "ámbito" || v == "ambito")
                LastRootMode = RootMode.Ambito;
            else if (v == "sistema")
                LastRootMode = RootMode.Sistema;
            else
                LastRootMode = RootMode.Generico;

            bool isSistema = (LastRootMode == RootMode.Sistema);

            // 2) Roles usuario
            var userRoles = ToRoleSet(userRolesCsv);

            // 3) Construcción en memoria (SIN tocar UI)
            var built = new List<TreeNode>();

            if (!isSistema)
            {
                if (rootItem.HasItems && rootItem.Items != null)
                {
                    foreach (NavigationItem child in rootItem.Items)
                    {
                        if (child == null) continue;
                        var nodeRoles = ParseRolesCsv(child.LoggedInUserRoles);
                        if (!IsVisibleByRoles(nodeRoles, userRoles)) continue;

                        var tn = CreateNode(child, parentTitle: rootItem.Title ?? rootItem.Name, alreadyParsedRoles: nodeRoles);
                        built.Add(tn);
                    }
                }
            }
            else
            {
                if (rootItem.HasItems && rootItem.Items != null)
                {
                    foreach (NavigationItem child in rootItem.Items)
                    {
                        if (child == null) continue;

                        var childRoles = ParseRolesCsv(child.LoggedInUserRoles);
                        if (!IsVisibleByRoles(childRoles, userRoles)) continue;

                        if (!(child.HasItems && child.Items != null)) continue;

                        foreach (NavigationItem gc in child.Items)
                        {
                            if (gc == null) continue;
                            var gcRoles = ParseRolesCsv(gc.LoggedInUserRoles);
                            if (!IsVisibleByRoles(gcRoles, userRoles)) continue;

                            var tn = CreateNode(gc, parentTitle: child.Title ?? child.Name, alreadyParsedRoles: gcRoles);
                            built.Add(tn);
                        }
                    }
                }
            }

            Logger.LogInfo(() => $"Bootstrap: Projection done. BuiltCount={built.Count}, mode={LastRootMode}");

            // 4) Lectura de values (aplica valores con Dispatcher internamente)
            await ReadValuesForNodesAsync(subscription, built).ConfigureAwait(false);

            // 5) Aplicar a Roots en hilo de UI
            OnUi(() =>
            {
                for (int i = 0; i < built.Count; i++)
                    Roots.Add(built[i]);

                var prev = string.Join(", ", built.Take(10).Select(n => n.Title));
                Logger.LogInfo(() => $"Bootstrap: Roots applied on UI. Count={Roots.Count}, Preview=[{prev}{(built.Count > 10 ? ", ..." : "")}]");
            });
        }

        public void Clear()
        {
            OnUi(() =>
            {
                try { DisposeRoots(); } catch { /* best effort */ }
                Roots.Clear();
                LastRootMode = RootMode.None;
            });
        }



        // ---------- Helpers ----------
        private void OnUi(Action action)
        {
            if (action == null) return;

            var disp = UIDispatcher;
            if (disp != null && !disp.CheckAccess())
                disp.Invoke(action, System.Windows.Threading.DispatcherPriority.Background);
            else
                action();
        }


        private TreeNode CreateNode(NavigationItem item, string parentTitle, IList<string> alreadyParsedRoles)
        {
            var title = !string.IsNullOrWhiteSpace(item.Title) ? item.Title : item.Name;
            var tn = new TreeNode(title, item.Name, item.Path, parentTitle, EnableLogs);

            // Cargar roles del nodo (ya parseados desde NavigationItem.LoggedInUserRoles)
            if (alreadyParsedRoles != null)
            {
                for (int i = 0; i < alreadyParsedRoles.Count; i++)
                {
                    var r = alreadyParsedRoles[i];
                    if (!string.IsNullOrWhiteSpace(r)) tn.LoggedInUserRoles.Add(r);
                }
            }
            return tn;
        }

        private static IList<string> ParseRolesCsv(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(csv)) return list;

            var parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var r = parts[i]?.Trim();
                if (!string.IsNullOrEmpty(r)) list.Add(r);
            }
            return list;
        }

        private static ISet<string> ToRoleSet(string csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(csv)) return set;

            var parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var r = parts[i]?.Trim();
                if (!string.IsNullOrEmpty(r)) set.Add(r);
            }
            return set;
        }

        private static bool IsVisibleByRoles(IList<string> nodeRoles, ISet<string> userRoles)
        {
            if (nodeRoles == null || nodeRoles.Count == 0) return true;

            for (int i = 0; i < nodeRoles.Count; i++)
                if (string.Equals(nodeRoles[i]?.Trim(), "Unconfigured", StringComparison.OrdinalIgnoreCase))
                    return true;

            if (userRoles == null || userRoles.Count == 0) return false;

            for (int i = 0; i < nodeRoles.Count; i++)
            {
                var role = nodeRoles[i];
                if (string.IsNullOrWhiteSpace(role)) continue;
                if (userRoles.Contains(role.Trim())) return true;
            }
            return false;
        }

        // Variante que además devuelve el rol que hizo match (para el log)
        private static bool IsVisibleByRolesWithMatch(IList<string> nodeRoles, ISet<string> userRoles, out string matchedRole)
        {
            matchedRole = null;

            if (nodeRoles == null || nodeRoles.Count == 0) return true;

            for (int i = 0; i < nodeRoles.Count; i++)
                if (string.Equals(nodeRoles[i]?.Trim(), "Unconfigured", StringComparison.OrdinalIgnoreCase))
                    return true;

            if (userRoles == null || userRoles.Count == 0) return false;

            for (int i = 0; i < nodeRoles.Count; i++)
            {
                var role = nodeRoles[i];
                if (string.IsNullOrWhiteSpace(role)) continue;
                var r = role.Trim();
                if (userRoles.Contains(r))
                {
                    matchedRole = r;
                    return true;
                }
            }
            return false;
        }

        private async Task<string> TryReadSingleValueAsync(DataSubscription subscription, string name)
        {
            if (subscription == null || OneShotReaderAsync == null) return null;
            if (string.IsNullOrWhiteSpace(AttributeName)) return null;
            if (string.IsNullOrWhiteSpace(name)) return null;

            var suffix = NormalizeSuffix(AttributeName);
            var reference = BuildReference(name, suffix);
            if (string.IsNullOrEmpty(reference)) return null;

            try
            {
                var dict = await OneShotReaderAsync(subscription, new List<string> { reference }.AsReadOnly()).ConfigureAwait(false);
                if (dict != null && dict.TryGetValue(reference, out var val))
                    return val != null ? Convert.ToString(val) : null;
            }
            catch (Exception ex)
            {
                SvcLogError(() => $"TryReadSingleValueAsync: {ex.Message}", ex);
            }
            return null;
        }

        private async Task ReadValuesForNodesAsync(DataSubscription subscription, IEnumerable<TreeNode> nodes)
        {
            if (subscription == null || OneShotReaderAsync == null) { SvcLogInfo(() => "Bootstrap: Skip ReadValues (subscription/reader null)."); return; }
            if (string.IsNullOrWhiteSpace(AttributeName)) { SvcLogInfo(() => "Bootstrap: Skip ReadValues (AttributeName empty)."); return; }
            if (nodes == null) { SvcLogInfo(() => "Bootstrap: Skip ReadValues (nodes null)."); return; }

            var suffix = NormalizeSuffix(AttributeName);
            var list = nodes.Where(n => n != null && !string.IsNullOrWhiteSpace(n.Name)).ToList();
            if (list.Count == 0) { SvcLogInfo(() => "Bootstrap: Skip ReadValues (no nodes)."); return; }

            var refs = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var r = BuildReference(list[i].Name, suffix);
                if (!string.IsNullOrEmpty(r)) refs.Add(r);
            }
            if (refs.Count == 0) { SvcLogInfo(() => "Bootstrap: Skip ReadValues (no references)."); return; }

            SvcLogInfo(() => $"Bootstrap: Preparing value refs for Roots (suffix='{(AttributeName ?? "")}'). Count={refs.Count}, preview=[{string.Join(", ", refs.Take(5))}{(refs.Count > 5 ? ", ..." : "")}]");

            IDictionary<string, object> dict;
            try
            {
                dict = await OneShotReaderAsync(subscription, refs.AsReadOnly()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SvcLogError(() => $"ReadValuesForNodesAsync: {ex.Message}", ex);
                return;
            }
            if (dict == null || dict.Count == 0)
            {
                SvcLogWarn(() => "Bootstrap: ReadValues -> dictionary NULL o vacío.");
                return;
            }

            int applied = 0, missing = 0;

            var disp = UIDispatcher;
            Action apply = () =>
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var tn = list[i];
                    var reff = BuildReference(tn.Name, suffix);
                    if (string.IsNullOrEmpty(reff)) { missing++; continue; }
                    object valObj;
                    if (!dict.TryGetValue(reff, out valObj)) { missing++; continue; }
                    tn.Value = valObj != null ? Convert.ToString(valObj) : null;
                    applied++;
                }
            };

            if (disp != null && !disp.CheckAccess())
                await disp.InvokeAsync(apply, System.Windows.Threading.DispatcherPriority.Background);
            else
                apply();

            SvcLogInfo(() => $"Bootstrap: Values applied to Roots. updated={applied}, missingRefs={missing}");
        }

        private static string BuildReference(string name, string normalizedSuffix)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (string.IsNullOrEmpty(normalizedSuffix)) return null;
            return name + "." + normalizedSuffix;
        }

        private static string NormalizeSuffix(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName)) return string.Empty;
            var s = attributeName.Trim();
            while (s.Length > 0 && s[0] == '.') s = s.Substring(1);
            return s;
        }

        private void DisposeRoots()
        {
            for (int i = 0; i < Roots.Count; i++)
            {
                try { Roots[i]?.Dispose(); } catch { }
            }
        }
    }
}
