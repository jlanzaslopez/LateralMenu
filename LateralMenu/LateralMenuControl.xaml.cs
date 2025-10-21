using ArchestrA.Client.RuntimeData;   // IRuntimeDataClient, DataSubscription
using ArchestrA.Diagnostics;          // Logger
using LateralMenu.Controls;               // InstallationsListControl, TreeNodeEventArgs, ListDepthMode
using LateralMenu.Models;                 // TreeNode
using LateralMenu.Services;               // ITreeBootstrapService, TreeBootstrapService, ValueReader
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LateralMenu
{
    public partial class LateralMenuControl : UserControl, INotifyPropertyChanged, IRuntimeDataClient
    {
        // =============== Private state ===============
        private CancellationTokenSource _reloadCts;
        private ITreeBootstrapService _localBootstrap;

        private bool _initialValuesReady;
        private bool _fullTreeLoaded;
        private string _lastResolvedPath;

        public LateralMenuControl()
        {
            if (EnableLogs) Logger.LogInfo(() => "LateralMenuControl ctor: InitializeComponent + hooks.");
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // =============== Dependency Properties ===============

        // NAVIGATION ROOT (clamps the subtree we build)
        [Category("Navigation")]
        public static readonly DependencyProperty StartPathProperty =
            DependencyProperty.Register(
                nameof(StartPath),
                typeof(string),
                typeof(LateralMenuControl),
                new PropertyMetadata(default(string), OnStartPathChanged));

        [Category("Navigation")]
        public string StartPath
        {
            get => (string)GetValue(StartPathProperty);
            set => SetValue(StartPathProperty, value);
        }

        // CURRENT NAVIGATION PATH (two-way)
        [Category("Navigation")]
        public static readonly DependencyProperty CurrentPathProperty =
            DependencyProperty.Register(
                nameof(CurrentPath),
                typeof(string),
                typeof(LateralMenuControl),
                new FrameworkPropertyMetadata(
                    default(string),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnCurrentPathChanged));

        [Category("Navigation")]
        public string CurrentPath
        {
            get => (string)GetValue(CurrentPathProperty);
            set => SetValue(CurrentPathProperty, value);
        }

        // INTERNAL: resolved parent node we feed into the child control
        [Category("Data")]
        public static readonly DependencyProperty ParentNodeProperty =
            DependencyProperty.Register(
                nameof(ParentNode),
                typeof(TreeNode),
                typeof(LateralMenuControl),
                new PropertyMetadata(null, OnParentNodeChanged));

        [Category("Data")]
        public TreeNode ParentNode
        {
            get => (TreeNode)GetValue(ParentNodeProperty);
            set => SetValue(ParentNodeProperty, value);
        }

        // BEHAVIOR FLAGS (host overrides removed from manifest; we still expose them)
        [Category("Behavior")]
        public static readonly DependencyProperty DepthModeProperty =
            DependencyProperty.Register(
                nameof(DepthMode),
                typeof(ListDepthMode),
                typeof(LateralMenuControl),
                new PropertyMetadata(ListDepthMode.FirstLevel, OnDepthModeChanged));

        [Category("Behavior")]
        public ListDepthMode DepthMode
        {
            get => (ListDepthMode)GetValue(DepthModeProperty);
            set => SetValue(DepthModeProperty, value);
        }

        [Category("Behavior")]
        public static readonly DependencyProperty ShowParentTitleProperty =
            DependencyProperty.Register(
                nameof(ShowParentTitle),
                typeof(bool),
                typeof(LateralMenuControl),
                new PropertyMetadata(false, OnShowParentTitleChanged));

        [Category("Behavior")]
        public bool ShowParentTitle
        {
            get => (bool)GetValue(ShowParentTitleProperty);
            set => SetValue(ShowParentTitleProperty, value);
        }

        [Category("Behavior")]
        public static readonly DependencyProperty GroupByValueProperty =
            DependencyProperty.Register(
                nameof(GroupByValue),
                typeof(bool),
                typeof(LateralMenuControl),
                new PropertyMetadata(false, OnGroupByValueChanged));

        [Category("Behavior")]
        public bool GroupByValue
        {
            get => (bool)GetValue(GroupByValueProperty);
            set => SetValue(GroupByValueProperty, value);
        }

        [Category("Behavior")]
        public static readonly DependencyProperty SortBySeverityProperty =
            DependencyProperty.Register(
                nameof(SortBySeverity),
                typeof(bool),
                typeof(LateralMenuControl),
                new PropertyMetadata(false, OnSortBySeverityChanged));

        [Category("Behavior")]
        public bool SortBySeverity
        {
            get => (bool)GetValue(SortBySeverityProperty);
            set => SetValue(SortBySeverityProperty, value);
        }

        [Category("Behavior")]
        public static readonly DependencyProperty SeveritySortDescendingProperty =
            DependencyProperty.Register(
                nameof(SeveritySortDescending),
                typeof(bool),
                typeof(LateralMenuControl),
                new PropertyMetadata(true, OnSeveritySortDescendingChanged));

        [Category("Behavior")]
        public bool SeveritySortDescending
        {
            get => (bool)GetValue(SeveritySortDescendingProperty);
            set => SetValue(SeveritySortDescendingProperty, value);
        }

        // VALUE READ CONFIG (TagSuffix)
        [Category("Configuration")]
        public static readonly DependencyProperty TagSuffixProperty =
            DependencyProperty.Register(
                nameof(TagSuffix),
                typeof(string),
                typeof(LateralMenuControl),
                new PropertyMetadata(string.Empty, OnTagSuffixChanged));

        [Category("Configuration")]
        public string TagSuffix
        {
            get => (string)GetValue(TagSuffixProperty);
            set => SetValue(TagSuffixProperty, value);
        }

        // LOGGING
        [Category("Logs")]
        public static readonly DependencyProperty EnableLogsProperty =
            DependencyProperty.Register(
                nameof(EnableLogs),
                typeof(bool),
                typeof(LateralMenuControl),
                new PropertyMetadata(false));

        [Category("Logs")]
        public bool EnableLogs
        {
            get => (bool)GetValue(EnableLogsProperty);
            set => SetValue(EnableLogsProperty, value);
        }

        // =============== Runtime Data (IRuntimeDataClient) ===============
        [Category("Runtime Data")]
        public DataSubscription DataSubscription { get; set; }

        // Optional external bootstrap (shared model). If null, we create a local one.
        public ITreeBootstrapService Bootstrap { get; set; }

        // =============== Lifecycle ===============
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            LogInfo(() => "LateralMenuControl Loaded: initializing service, building full subtree from StartPath, one-shot read, then show UI.");

            var svc = EnsureBootstrap();

            // Hide the list until values are applied
            if (ListControl != null) ListControl.Visibility = Visibility.Collapsed;

            // 1) Build full subtree from StartPath once (replace roots)
            if (!string.IsNullOrWhiteSpace(StartPath))
            {
                LogInfo(() => $"LateralMenuControl Loaded: LoadTreeFromPath(StartPath='{StartPath}', merge=false) [FULL TREE].");
                svc.LoadTreeFromPath(StartPath, merge: false);
                _fullTreeLoaded = true;
            }
            else
            {
                LogInfo(() => "LateralMenuControl Loaded: StartPath is empty — cannot build full tree.");
                _fullTreeLoaded = false;
            }

            // 2) One-shot value read once
            var tagSuffix = TagSuffix ?? string.Empty;
            if (_fullTreeLoaded && !string.IsNullOrWhiteSpace(tagSuffix) && DataSubscription != null)
            {
                try
                {
                    LogInfo(() => $"LateralMenuControl Loaded: BootstrapValuesAsync BEGIN (TagSuffix='{tagSuffix}').");
                    await svc.BootstrapValuesAsync(DataSubscription).ConfigureAwait(true);
                    LogInfo(() => "LateralMenuControl Loaded: BootstrapValuesAsync END.");
                }
                catch (Exception ex)
                {
                    LogError(() => $"LateralMenuControl Loaded: BootstrapValuesAsync threw: {ex.Message}", ex);
                }
            }
            else
            {
                LogInfo(() =>
                    $"LateralMenuControl Loaded: Skipping BootstrapValues (FullTreeLoaded={_fullTreeLoaded}, TagSuffix empty? {string.IsNullOrWhiteSpace(tagSuffix)}, DataSubscription null? {DataSubscription == null}).");
            }

            _initialValuesReady = true;

            // 3) Wire child events and push behavior flags
            HookChildControlEvents();
            ApplyHostPropsToChild();

            // 4) Resolve initial node and show UI
            ResolveAndReload(reason: "LoadedInitial");
            if (ListControl != null) ListControl.Visibility = Visibility.Visible;

            LogInfo(() => "LateralMenuControl Loaded: initial tree + values ready, UI shown.");
        }

        // =============== DP Change Handlers ===============
        private static void OnStartPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.LogInfo(() => $"LateralMenuControl StartPath changed: '{e.OldValue ?? ""}' → '{e.NewValue ?? ""}'.");
            if (host.IsLoaded)
            {
                // If StartPath changes at runtime, force a rebuild/read on next load if desired.
                host._fullTreeLoaded = false;
                host._initialValuesReady = false;
                host._lastResolvedPath = null;
                host.ResolveAndReload("StartPathChanged"); // will likely no-op until reload path completes
            }
        }

        private static void OnCurrentPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.LogInfo(() => $"LateralMenuControl CurrentPath changed: '{e.OldValue ?? ""}' → '{e.NewValue ?? ""}'.");
            host.HandleCurrentPathChanged();
        }

        private static void OnParentNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.LogInfo(() => $"LateralMenuControl ParentNode changed: {(e.NewValue != null ? "set" : "null")}.");
            host.ApplyConditionalPresentation(host.ParentNode);   // apply rules based on node.Value
            host.ApplyHostPropsToChild();                        // forward flags
        }

        private static void OnDepthModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.LogInfo(() => $"LateralMenuControl DepthMode changed: {e.OldValue} → {e.NewValue}.");
            host.HandleDepthModeChanged();
        }

        private static void OnShowParentTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.LogInfo(() => $"LateralMenuControl ShowParentTitle changed: {e.OldValue} → {e.NewValue}.");
            host.ApplyHostPropsToChild();
        }

        private static void OnGroupByValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.LogInfo(() => $"LateralMenuControl GroupByValue changed: {e.OldValue} → {e.NewValue}.");
            host.ApplyHostPropsToChild();
        }

        private static void OnSortBySeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.LogInfo(() => $"LateralMenuControl SortBySeverity changed: {e.OldValue} → {e.NewValue}.");
            host.ApplyHostPropsToChild();
        }

        private static void OnSeveritySortDescendingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.LogInfo(() => $"LateralMenuControl SeveritySortDescending changed: {e.OldValue} → {e.NewValue}.");
            host.ApplyHostPropsToChild();
        }

        private static void OnTagSuffixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            var newVal = e.NewValue as string ?? string.Empty;
            host.LogInfo(() => $"LateralMenuControl: TagSuffix set to '{newVal}'.");
            if (host._localBootstrap != null)
                host._localBootstrap.AttributeName = newVal;
            // No auto-reload here; startup pipeline performs the one-shot read.
        }

        private void HandleCurrentPathChanged()
        {
            // Avoid duplicate reloads if the effective path hasn't changed.
            ResolveAndReload(reason: "CurrentPathChanged");
        }

        private void HandleDepthModeChanged()
        {
            CancelReload();
            ClearListFast();
            _ = ReloadViewSliceAsync("DepthModeChanged");
        }

        // =============== Bootstrap/service ===============
        private ITreeBootstrapService EnsureBootstrap()
        {
            // If an external service is provided, ensure it’s wired and synced.
            if (Bootstrap != null)
            {
                if (Bootstrap.OneShotReaderAsync == null)
                    Bootstrap.OneShotReaderAsync = ValueReader.ReadBulkAsync;
                if (!string.Equals(Bootstrap.AttributeName ?? string.Empty, TagSuffix ?? string.Empty, StringComparison.Ordinal))
                    Bootstrap.AttributeName = TagSuffix ?? string.Empty;
                return Bootstrap;
            }

            if (_localBootstrap == null)
            {
                _localBootstrap = new TreeBootstrapService
                {
                    AttributeName = TagSuffix ?? string.Empty,
                    SearchableContentType = string.Empty // keep service quiet on content
                };
                _localBootstrap.OneShotReaderAsync = ValueReader.ReadBulkAsync;
            }

            Bootstrap = _localBootstrap;
            return Bootstrap;
        }

        // =============== Resolve & Reload ===============
        private void ResolveAndReload(string reason)
        {
            CancelReload();
            ClearListFast();

            // Don’t bind until initial read finished (except our explicit LoadedInitial call)
            if (!_initialValuesReady && !string.Equals(reason, "LoadedInitial", StringComparison.Ordinal))
            {
                LogInfo(() => $"LateralMenuControl ResolveAndReload: initial values not ready (reason={reason}); skipping.");
                return;
            }

            var node = ResolveNodeForCurrentPath(CurrentPath);
            ParentNode = node;

            if (ParentNode == null)
            {
                LogInfo(() => $"LateralMenuControl ResolveAndReload({reason}): ParentNode is null for CurrentPath='{CurrentPath ?? ""}'. Showing empty.");
                ApplyHostPropsToChild();
                return;
            }

            ApplyConditionalPresentation(ParentNode);
            ApplyHostPropsToChild();

            _ = ReloadViewSliceAsync(reason);
        }

        private TreeNode ResolveNodeForCurrentPath(string path)
        {
            // Decide effective path (clamp to StartPath if outside)
            var effectivePath = string.IsNullOrWhiteSpace(path) ? (StartPath ?? string.Empty) : path;
            if (string.IsNullOrWhiteSpace(effectivePath)) return null;

            if (!string.IsNullOrWhiteSpace(StartPath) &&
                !effectivePath.StartsWith(StartPath, StringComparison.Ordinal))
            {
                LogInfo(() => $"LateralMenuControl.ResolveNodeForCurrentPath: '{effectivePath}' is outside StartPath '{StartPath}', clamping.");
                effectivePath = StartPath;
            }

            // Skip reload if path unchanged and we already have a node
            if (!string.IsNullOrEmpty(_lastResolvedPath) &&
                string.Equals(_lastResolvedPath, effectivePath, StringComparison.Ordinal) &&
                ParentNode != null)
                return ParentNode;

            var svc = EnsureBootstrap();

            if (!_fullTreeLoaded)
            {
                LogError(() => "LateralMenuControl.ResolveNodeForCurrentPath: full tree not loaded yet.");
                return null;
            }

            if (svc.TryGetNodeByPath(effectivePath, out var node) && node != null)
            {
                _lastResolvedPath = effectivePath;
                return node;
            }

            LogError(() => $"LateralMenuControl.ResolveNodeForCurrentPath: node not found in preloaded tree for path='{effectivePath}'.");
            return null;
        }

        private async Task ReloadViewSliceAsync(string reason)
        {
            _reloadCts = new CancellationTokenSource();
            var ct = _reloadCts.Token;

            try
            {
                LogInfo(() => $"LateralMenuControl ReloadViewSliceAsync: start. Reason={reason}, DepthMode={DepthMode}, ShowParentTitle={ShowParentTitle}.");

                var list = ListControl;
                if (list == null)
                {
                    LogError(() => "LateralMenuControl ReloadViewSliceAsync: List (InstallationsListControl) not found (x:Name missing?).");
                    return;
                }

                await Task.Yield(); // keep method async-friendly

                if (!_initialValuesReady)
                {
                    LogInfo(() => "LateralMenuControl ReloadViewSliceAsync: initial values not ready; skipping UI bind.");
                    return;
                }

                // Assign ParentNode to child (flip UI from empty -> new in one go)
                list.ParentNode = ParentNode;

                // Optional probe (comment out if noisy)
                if (EnableLogs && ParentNode != null)
                {
                    try
                    {
                        int shownCount = (DepthMode == ListDepthMode.FirstLevel)
                            ? (ParentNode.Items?.Count ?? 0)
                            : (ParentNode.Items?.Sum(c => c?.Items?.Count ?? 0) ?? 0);

                        LogInfo(() => $"LateralMenuControl ReloadViewSliceAsync: UI bound. Parent='{ParentNode.Path ?? "(null)"}', Depth={DepthMode}, ShownItems~={shownCount}.");
                    }
                    catch { /* ignore */ }
                }

                LogInfo(() => "LateralMenuControl ReloadViewSliceAsync: completed (no per-slice value read).");
            }
            catch (OperationCanceledException)
            {
                LogInfo(() => "LateralMenuControl ReloadViewSliceAsync: canceled.");
            }
            catch (Exception ex)
            {
                LogError(() => $"LateralMenuControl ReloadViewSliceAsync: exception: {ex.Message}", ex);
            }
        }

        private void ClearListFast()
        {
            var list = ListControl;
            if (list != null)
                list.ParentNode = null; // clear first to avoid stale rows
        }

        private void CancelReload()
        {
            if (_reloadCts != null)
            {
                try { _reloadCts.Cancel(); }
                catch { /* ignored */ }
                finally { _reloadCts = null; }
            }
        }

        /// <summary>
        /// RULES: set DepthMode/ShowParentTitle/GroupByValue based on ParentNode.Value.
        /// - "Ámbito"  : no group, no parent title, FirstLevel
        /// - "Sistema" : group, show parent title, TwoLevelsFlat (grandchildren)
        /// - default   : group, no parent title, FirstLevel
        /// </summary>
        private void ApplyConditionalPresentation(TreeNode node)
        {
            if (node == null) return;

            var v = node.Value ?? string.Empty;

            if (string.Equals(v, "Ámbito", StringComparison.OrdinalIgnoreCase))
            {
                GroupByValue = false;
                ShowParentTitle = false;
                DepthMode = ListDepthMode.FirstLevel;
            }
            else if (string.Equals(v, "Sistema", StringComparison.OrdinalIgnoreCase))
            {
                GroupByValue = true;
                ShowParentTitle = true;
                DepthMode = ListDepthMode.TwoLevelsFlat; // grandchildren
            }
            else
            {
                GroupByValue = true;
                ShowParentTitle = false;
                DepthMode = ListDepthMode.FirstLevel;
            }
        }

        // =============== Child Control Wiring ===============
        private void HookChildControlEvents()
        {
            var list = ListControl;
            if (list == null)
            {
                LogError(() => "LateralMenuControl HookChildControlEvents: List (InstallationsListControl) not found. Ensure x:Name=\"List\" in XAML.");
                return;
            }

            list.ItemInvoked -= OnItemInvoked;
            list.FavoriteToggleRequested -= OnFavoriteToggleRequested;

            list.ItemInvoked += OnItemInvoked;
            list.FavoriteToggleRequested += OnFavoriteToggleRequested;

            LogInfo(() => "LateralMenuControl: Child control events hooked.");
        }

        private void ApplyHostPropsToChild()
        {
            var list = ListControl;
            if (list == null) return;

            list.DepthMode = DepthMode;
            list.ShowParentTitle = ShowParentTitle;
            list.GroupByValue = GroupByValue;
            list.SortBySeverity = SortBySeverity;
            list.SeveritySortDescending = SeveritySortDescending;
            // ParentNode is assigned during ReloadViewSliceAsync
        }

        private void OnItemInvoked(object sender, TreeNodeEventArgs e)
        {
            if (e?.Node == null) return;

            LogInfo(() => $"LateralMenuControl OnItemInvoked: path='{e.Node.Path ?? ""}'.");
            // Write to CurrentPath so the main app navigates
            CurrentPath = e.Node.Path;

        }

        private void OnFavoriteToggleRequested(object sender, TreeNodeEventArgs e)
        {
            if (e?.Node == null) return;

            LogInfo(() => $"LateralMenuControl OnFavoriteToggleRequested: path='{e.Node.Path ?? ""}'.");
            // TODO: persist favorite toggle and reflect IsFavorite change on e.Node
        }

        private InstallationsListControl ListControl
        {
            get => this.FindName("List") as InstallationsListControl;
        }

        // =============== Logging Helpers ===============
        private void LogInfo(Func<string> msgFactory)
        {
            if (EnableLogs && msgFactory != null)
                Logger.LogInfo(msgFactory);
        }

        private void LogError(Func<string> msgFactory, Exception ex = null)
        {
            if (EnableLogs && msgFactory != null)
                Logger.LogError(msgFactory, ex);
        }

        // =============== INotifyPropertyChanged ===============
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
