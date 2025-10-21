using ArchestrA.Client.Navigation;       // NavigationModel, NavigationItem, FilterOptions, ContentData, NavSearchOptions, SearchLevelOption
using ArchestrA.Client.RuntimeData;      // DataSubscription
using ArchestrA.Diagnostics;             // Logger
using LateralMenu.Models;                   // TreeNode
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;                    // Application
using System.Windows.Threading;          // Dispatcher

namespace LateralMenu.Services
{
    /// <summary>
    /// Quiet bootstrapper for OMI navigation trees.
    /// - Can load a subtree from a path or NavigationItem (replace or merge).
    /// - Maintains an O(1) path→node index for fast resolution.
    /// - Optionally prepares Title+TagSuffix references and performs one-shot value hydration.
    /// - Assigns ParentTitle for ALL nodes (navigation and content).
    /// </summary>
    public sealed class TreeBootstrapService : ITreeBootstrapService
    {

        // TEMP: make value bootstrap path very chatty
        private const bool VerboseValues = true;
        private static void LogV(Func<string> msg)
        {
            if (!VerboseValues || msg == null) return;
            Logger.LogInfo(msg);
        }

        // ------------ Public surface ------------

        public ObservableCollection<TreeNode> Roots { get; } = new ObservableCollection<TreeNode>();

        /// <summary>Suffix used to build value references (e.g., "TEXT"). Empty => skip value hydration.</summary>
        public string AttributeName { get; set; } = string.Empty;

        /// <summary>Content type to include per node. Empty => skip content entirely.</summary>
        public string SearchableContentType { get; set; } = string.Empty;

        /// <summary>Delegate that performs a one-shot bulk read using the OMI DataSubscription.</summary>
        public Func<DataSubscription, IReadOnlyList<string>, Task<IDictionary<string, object>>> OneShotReaderAsync { get; set; }

        /// <summary>True when Roots and the internal path index are ready.</summary>
        public bool IsTreeLoaded => Roots.Count > 0 && _byPath.Count > 0;

        /// <summary>Populate only the subtree under startPath. merge=false replaces Roots; merge=true accumulates.</summary>
        public void LoadTreeFromPath(string startPath, bool merge = false)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                Logger.LogWarning(() => "TreeBootstrapService.LoadTreeFromPath: startPath is null/empty.");
                if (!merge) Clear();
                return;
            }

            var model = NavigationModel.ViewAppNavigationModel;
            if (model == null)
            {
                Logger.LogWarning(() => "TreeBootstrapService.LoadTreeFromPath: NavigationModel.ViewAppNavigationModel is NULL.");
                if (!merge) Clear();
                return;
            }

            // NEW: prefer SDK lookup; fall back to our DFS only if needed
            var item = model.GetItemBypath(startPath) ?? FindItemByPath(model, startPath);
            if (item == null)
            {
                Logger.LogWarning(() => $"TreeBootstrapService.LoadTreeFromPath: path not found: '{startPath}'.");
                if (!merge) Clear();
                return;
            }

            LoadTreeFromItem(item, merge);
        }


        /// <summary>Populate only the subtree under startItem. merge=false replaces Roots; merge=true accumulates.</summary>
        public void LoadTreeFromItem(NavigationItem startItem, bool merge = false)
        {
            if (startItem == null)
            {
                Logger.LogWarning(() => "TreeBootstrapService.LoadTreeFromItem: startItem is NULL.");
                if (!merge) Clear();
                return;
            }

            var model = NavigationModel.ViewAppNavigationModel;
            if (model == null)
            {
                Logger.LogWarning(() => "TreeBootstrapService.LoadTreeFromItem: NavigationModel.ViewAppNavigationModel is NULL.");
                if (!merge) Clear();
                return;
            }

            if (!merge)
            {
                Roots.Clear();
                _byPath.Clear();
                _sources.Clear();
                _refToNode.Clear();
            }

            // Build filter: empty ContentTypes => skip content queries entirely
            var filter = new FilterOptions
            {
                NavSearchOptions = NavSearchOptions.StartNode,
                SearchLevelOptions = SearchLevelOption.OneLevel,
                ContentTypes = string.IsNullOrWhiteSpace(SearchableContentType)
                    ? new List<string>()       // skip content
                    : new List<string> { SearchableContentType }
            };

            // Build subtree; root has no parent title
            var rootNode = ConvertNode(startItem, model, filter, parentObjectPath: null, parentTitle: null);
            if (rootNode != null)
            {
                Roots.Add(rootNode);
                IndexSubtree(rootNode);
            }

            // Refresh value reference maps (quiet)
            RebuildSources();
        }

        /// <summary>Fast O(1) lookup of an already-built node by its exact path.</summary>
        public bool TryGetNodeByPath(string path, out TreeNode node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            return _byPath.TryGetValue(NormalizePath(path), out node);
        }

        /// <summary>
        /// One-shot value hydration. Reads values for prepared references and writes them to nodes.
        /// Quiet mode: returns silently on benign skip conditions.
        /// </summary>
        public async Task BootstrapValuesAsync(DataSubscription subscription)
        {
            // Verbose entry
            LogV(() => $"[Values] BEGIN: AttributeName='{AttributeName ?? ""}', SourcesPrepared={_sources.Count}, ReaderSet={(OneShotReaderAsync != null)}.");

            if (subscription == null)
            {
                LogV(() => "[Values] SKIP: DataSubscription is NULL.");
                return;
            }
            if (string.IsNullOrWhiteSpace(AttributeName))
            {
                LogV(() => "[Values] SKIP: AttributeName (TagSuffix) is empty.");
                return;
            }

            if (_sources.Count == 0)
            {
                LogV(() => "[Values] RebuildSources (initial)...");
                RebuildSources();
                LogV(() => $"[Values] RebuildSources done: prepared={_sources.Count}.");
            }

            if (_sources.Count == 0)
            {
                LogV(() => "[Values] SKIP: No prepared references.");
                return;
            }

            var reader = OneShotReaderAsync;
            if (reader == null)
            {
                LogV(() => "[Values] SKIP: OneShotReaderAsync not set.");
                return;
            }

            // Sample first few refs
            var preview = string.Join(", ", _sources.Take(10).Select(s => s.Reference));
            LogV(() => $"[Values] Reading refs: count={_sources.Count}, preview=[{preview}{(_sources.Count > 10 ? ", ..." : "")}]");

            IDictionary<string, object> dict;
            try
            {
                var refs = _sources.Select(s => s.Reference).ToList().AsReadOnly();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                dict = await reader(subscription, refs).ConfigureAwait(false);
                sw.Stop();

                if (dict == null)
                {
                    LogV(() => "[Values] Reader returned NULL dictionary.");
                    return;
                }

                LogV(() => $"[Values] Reader returned {dict.Count} key(s) in {sw.ElapsedMilliseconds} ms.");
            }
            catch (Exception ex)
            {
                Logger.LogError(() => $"TreeBootstrapService.BootstrapValuesAsync: reader threw: {ex.Message}", ex);
                return;
            }

            // Apply on UI thread
            var dispatcher = Application.Current?.Dispatcher;
            int applied = 0;
            int missingKeys = 0;

            Action apply = () =>
            {
                // Track misses: keys prepared but not present in dict
                var dictKeys = new HashSet<string>(dict.Keys, StringComparer.Ordinal);

                foreach (var src in _sources)
                {
                    if (!dict.TryGetValue(src.Reference, out var val))
                    {
                        missingKeys++;
                        continue;
                    }

                    if (_refToNode.TryGetValue(src.Reference, out var nodes) && nodes != null)
                    {
                        var text = val != null ? Convert.ToString(val) : null;
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            nodes[i].Value = text;
                            applied++;
                        }
                    }
                }
            };

            if (dispatcher != null && !dispatcher.CheckAccess())
                await dispatcher.InvokeAsync(apply, DispatcherPriority.Background);
            else
                apply();

            LogV(() => $"[Values] APPLY DONE: nodesUpdated={applied}, missingRefs={missingKeys}.");
            LogV(() => "[Values] END");
        }


        /// <summary>Clear tree and all maps.</summary>
        public void Clear()
        {
            Roots.Clear();
            _byPath.Clear();
            _sources.Clear();
            _refToNode.Clear();
        }

        // ------------ Internals (quiet) ------------

        private readonly Dictionary<string, TreeNode> _byPath =
            new Dictionary<string, TreeNode>(StringComparer.Ordinal); // adjust comparer if paths are case-insensitive

        private readonly List<SourceMap> _sources = new List<SourceMap>();
        private readonly Dictionary<string, List<TreeNode>> _refToNode =
            new Dictionary<string, List<TreeNode>>(StringComparer.Ordinal);

        private static string NormalizePath(string path) => path?.Trim() ?? string.Empty;

        private static NavigationItem FindItemByPath(NavigationModel model, string path)
        {
            if (model == null || string.IsNullOrWhiteSpace(path)) return null;
            var norm = NormalizePath(path);

            // Direct root match
            if (model.RootItem != null &&
                string.Equals(NormalizePath(model.RootItem.Path), norm, StringComparison.Ordinal))
                return model.RootItem;

            // Scan top-level items
            var roots = model.Items;
            if (roots != null)
            {
                foreach (NavigationItem ni in roots)
                {
                    var found = DfsFindByPath(ni, norm);
                    if (found != null) return found;
                }
            }

            // Fallback: DFS under RootItem
            return model.RootItem != null ? DfsFindByPath(model.RootItem, norm) : null;
        }

        private static NavigationItem DfsFindByPath(NavigationItem start, string normPath)
        {
            if (start == null) return null;
            var stack = new Stack<NavigationItem>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null) continue;

                if (string.Equals(NormalizePath(cur.Path), normPath, StringComparison.Ordinal))
                    return cur;

                if (cur.HasItems && cur.Items != null)
                {
                    for (int i = cur.Items.Count - 1; i >= 0; i--)
                    {
                        var child = cur.Items[i];
                        if (child != null) stack.Push(child);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Build a TreeNode from a NavigationItem.
        /// - Assigns ParentTitle for the current node.
        /// - Adds content nodes (when enabled) with ParentTitle = owning object's Title.
        /// - Recurses into navigation children, passing the current node's Title as their ParentTitle.
        /// </summary>
        private TreeNode ConvertNode(
            NavigationItem item,
            NavigationModel model,
            FilterOptions filter,
            string parentObjectPath,
            string parentTitle)
        {
            if (item == null) return null;

            string title = !string.IsNullOrWhiteSpace(item.Title) ? item.Title : item.Name;
            string name = item.Name;
            string path = item.Path;

            var node = new TreeNode
            {
                Title = title,
                Name = name,
                Path = path,
                IsContent = false,    // navigation object
                ParentPath = null,
                ParentTitle = parentTitle
            };

            // CONTENT (skip if ContentTypes is empty)
            bool skipContent = (filter?.ContentTypes == null || filter.ContentTypes.Count() == 0);
            if (!skipContent && node.Items != null)
            {
                ContentData[] contents = null;
                try
                {
                    contents = model.GetContentInHierarchy(path, filter);
                }
                catch (Exception ex)
                {
                    Logger.LogError(() => $"TreeBootstrapService.ConvertNode('{path}'): GetContentInHierarchy threw: {ex.Message}", ex);
                    contents = null;
                }

                if (contents != null && contents.Length > 0)
                {
                    for (int i = 0; i < contents.Length; i++)
                    {
                        var c = contents[i];
                        if (c == null) continue;

                        // Use trailing segment after the last '.' as display title
                        var rawName = c.ContentName ?? string.Empty;
                        var lastDot = rawName.LastIndexOf('.');
                        string cTitle = (lastDot >= 0 && lastDot + 1 < rawName.Length)
                            ? rawName.Substring(lastDot + 1).Trim()
                            : rawName.Trim();

                        var contentNode = new TreeNode
                        {
                            Title = cTitle,
                            Path = path,     // content reuses owning object's path
                            IsContent = true,
                            ParentPath = path,     // navigate to the owning object
                            ParentTitle = title     // parent is the current nav node
                        };

                        node.Items.Add(contentNode);
                    }
                }
            }

            // NAV CHILDREN
            if (item.HasItems && item.Items != null && node.Items != null)
            {
                var nextParentPath = !string.IsNullOrWhiteSpace(path) ? path : parentObjectPath;

                foreach (NavigationItem child in item.Items)
                {
                    var childTn = ConvertNode(child, model, filter, nextParentPath, parentTitle: title);
                    if (childTn != null)
                        node.Items.Add(childTn);
                }
            }

            return node;
        }

        private void IndexSubtree(TreeNode root)
        {
            if (root == null) return;

            var stack = new Stack<TreeNode>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n == null) continue;

                if (!n.IsContent)
                {
                    var key = NormalizePath(n.Path);
                    if (!string.IsNullOrEmpty(key))
                        _byPath[key] = n; // merge/replace OK
                }

                var kids = n.Items;
                if (kids != null)
                {
                    for (int i = kids.Count - 1; i >= 0; i--)
                    {
                        var c = kids[i];
                        if (c != null) stack.Push(c);
                    }
                }
            }
        }

        // Prepare Title.Suffix references for one-shot hydration (quiet)
        private void RebuildSources()
        {
            _sources.Clear();
            _refToNode.Clear();

            int traversed = 0;
            int navigable = 0;

            if (Roots.Count == 0)
            {
                LogV(() => "[Values] RebuildSources: no roots.");
                return;
            }
            if (string.IsNullOrWhiteSpace(AttributeName))
            {
                LogV(() => "[Values] RebuildSources: AttributeName empty → skip.");
                return;
            }

            string suffix = NormalizeSuffix(AttributeName);

            foreach (var node in Flatten(Roots))
            {
                traversed++;
                if (node == null) continue;
                if (node.IsContent) continue; // content nodes do not produce value refs

                navigable++;

                var reference = BuildReference(node, suffix);
                if (string.IsNullOrEmpty(reference)) continue;

                _sources.Add(new SourceMap { Node = node, Reference = reference });

                if (!_refToNode.TryGetValue(reference, out var bucket))
                {
                    bucket = new List<TreeNode>();
                    _refToNode[reference] = bucket;
                }
                bucket.Add(node);
            }

            var preview = string.Join(", ", _sources.Take(10).Select(s => s.Reference));
            LogV(() => $"[Values] RebuildSources: traversed={traversed}, navigable={navigable}, prepared={_sources.Count}, preview=[{preview}{(_sources.Count > 10 ? ", ..." : "")}]");
        }


        private static IEnumerable<TreeNode> Flatten(IEnumerable<TreeNode> roots)
        {
            if (roots == null) yield break;

            var stack = new Stack<TreeNode>();
            foreach (var r in roots)
                if (r != null) stack.Push(r);

            while (stack.Count > 0)
            {
                var n = stack.Pop();
                yield return n;

                var kids = n.Items;
                if (kids != null)
                {
                    for (int i = kids.Count - 1; i >= 0; i--)
                    {
                        var c = kids[i];
                        if (c != null) stack.Push(c);
                    }
                }
            }
        }

        private static string BuildReference(TreeNode node, string normalizedSuffix)
        {
            if (node == null || node.IsContent) return null;
            if (string.IsNullOrWhiteSpace(node.Name)) return null;
            if (string.IsNullOrEmpty(normalizedSuffix)) return null;
            return node.Name + "." + normalizedSuffix;
        }

        private static string NormalizeSuffix(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName)) return string.Empty;
            var s = attributeName.Trim();
            while (s.Length > 0 && s[0] == '.') s = s.Substring(1);
            return s;
        }

        private sealed class SourceMap
        {
            public TreeNode Node;
            public string Reference;
        }
    }
}
