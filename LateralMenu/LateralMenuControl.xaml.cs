using ArchestrA.Client.MyViewApp;
using ArchestrA.Client.Navigation;       // Navigation, Security
using ArchestrA.Client.RuntimeData;      // IRuntimeDataClient, DataSubscription
using ArchestrA.Diagnostics;             // Logger
using LateralMenu.Controls;              // InstallationsListControl, TreeNodeEventArgs
using LateralMenu.Models;
using LateralMenu.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;


namespace LateralMenu
{
    public partial class LateralMenuControl : UserControl, INotifyPropertyChanged, IRuntimeDataClient
    {
        // =============== Private state ===============
        private ITreeBootstrapService _bootstrap;

        public LateralMenuControl()
        {
            string buildTag = DateTime.Now.ToString("HHmmss.fff");
            Logger.LogInfo(() => $"LateralMenuControl ctor: control actualizado importado correctamente [{buildTag}].");

            if (EnableLogs) Logger.LogInfo(() => "LateralMenuControl ctor: InitializeComponent + hooks.");
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += (_, __) => TreeNode.ResolveSubscription = null;
        }

        // =============== Dependency Properties ===============

        // PRESENTATION FLAGS
        [Category("Behavior")]
        public static readonly DependencyProperty ShowParentTitleProperty =
            DependencyProperty.Register(nameof(ShowParentTitle), typeof(bool),
                typeof(LateralMenuControl), new PropertyMetadata(false, OnPresentationFlagChanged));

        [Category("Behavior")]
        public bool ShowParentTitle
        {
            get { return (bool)GetValue(ShowParentTitleProperty); }
            set { SetValue(ShowParentTitleProperty, value); }
        }

        [Category("Behavior")]
        public static readonly DependencyProperty GroupByValueProperty =
            DependencyProperty.Register(nameof(GroupByValue), typeof(bool),
                typeof(LateralMenuControl), new PropertyMetadata(false, OnPresentationFlagChanged));

        [Category("Behavior")]
        public bool GroupByValue
        {
            get { return (bool)GetValue(GroupByValueProperty); }
            set { SetValue(GroupByValueProperty, value); }
        }

        [Category("Behavior")]
        public static readonly DependencyProperty SortBySeverityProperty =
            DependencyProperty.Register(nameof(SortBySeverity), typeof(bool),
                typeof(LateralMenuControl), new PropertyMetadata(false, OnPresentationFlagChanged));

        [Category("Behavior")]
        public bool SortBySeverity
        {
            get { return (bool)GetValue(SortBySeverityProperty); }
            set { SetValue(SortBySeverityProperty, value); }
        }

        [Category("Behavior")]
        public static readonly DependencyProperty SeveritySortDescendingProperty =
            DependencyProperty.Register(nameof(SeveritySortDescending), typeof(bool),
                typeof(LateralMenuControl), new PropertyMetadata(true, OnPresentationFlagChanged));

        [Category("Behavior")]
        public bool SeveritySortDescending
        {
            get { return (bool)GetValue(SeveritySortDescendingProperty); }
            set { SetValue(SeveritySortDescendingProperty, value); }
        }

        // VALUE READ CONFIG (TagSuffix) → por defecto "sInfoTipo"
        [Category("Configuration")]
        public static readonly DependencyProperty TagSuffixProperty =
            DependencyProperty.Register(nameof(TagSuffix), typeof(string),
                typeof(LateralMenuControl), new PropertyMetadata("sInfoTipo", OnTagSuffixChanged));

        [Category("Configuration")]
        public string TagSuffix
        {
            get { return (string)GetValue(TagSuffixProperty); }
            set { SetValue(TagSuffixProperty, value); }
        }

        // LOGGING
        [Category("Logs")]
        public static readonly DependencyProperty EnableLogsProperty =
            DependencyProperty.Register(nameof(EnableLogs), typeof(bool),
                typeof(LateralMenuControl), new PropertyMetadata(true));

        [Category("Logs")]
        public bool EnableLogs
        {
            get { return (bool)GetValue(EnableLogsProperty); }
            set { SetValue(EnableLogsProperty, value); }
        }


        // =============== Filtering (CSV) ===============

        [Category("Filtering")]
        public static readonly DependencyProperty FilteringEnabledProperty =
            DependencyProperty.Register(
                nameof(FilteringEnabled),
                typeof(bool),
                typeof(LateralMenuControl),
                new PropertyMetadata(false));

        [Category("Filtering")]
        public bool FilteringEnabled
        {
            get => (bool)GetValue(FilteringEnabledProperty);
            set => SetValue(FilteringEnabledProperty, value);
        }

        [Category("Filtering")]
        public static readonly DependencyProperty FilterValuesProperty =
            DependencyProperty.Register(
                nameof(FilterValues),
                typeof(string),
                typeof(LateralMenuControl),
                new PropertyMetadata(string.Empty /* CSV de valores únicos */));

        [Category("Filtering")]
        public string FilterValues
        {
            get => (string)GetValue(FilterValuesProperty);
            set => SetValue(FilterValuesProperty, value);
        }

        [Category("Filtering")]
        public static readonly DependencyProperty FilterSelectedProperty =
            DependencyProperty.Register(
                nameof(FilterSelected),
                typeof(string),
                typeof(LateralMenuControl),
                new PropertyMetadata(string.Empty /* CSV de valores seleccionados */, OnFilterSelectedChanged));

        [Category("Filtering")]
        public string FilterSelected
        {
            get => (string)GetValue(FilterSelectedProperty);
            set => SetValue(FilterSelectedProperty, value);
        }

        // =============== Data (conteo total de items) ===============
        [Category("Data")]
        public static readonly DependencyProperty ItemCountProperty =
            DependencyProperty.Register(
                nameof(ItemCount),
                typeof(int),
                typeof(LateralMenuControl),
                new PropertyMetadata(0));

        [Category("Data")]
        public int ItemCount
        {
            get => (int)GetValue(ItemCountProperty);
            set => SetValue(ItemCountProperty, value);
        }

        // =============== Runtime Data (IRuntimeDataClient) ===============
        [Category("Runtime Data")]
        public DataSubscription DataSubscription { get; set; }

        /// <summary>Servicio inyectable. Si es null, se crea uno local en runtime.</summary>
        public ITreeBootstrapService Bootstrap
        {
            get { return _bootstrap; }
            set
            {
                _bootstrap = value;
                if (_bootstrap != null)
                {
                    if (_bootstrap.OneShotReaderAsync == null)
                        _bootstrap.OneShotReaderAsync = ValueReader.ReadBulkAsync;
                    _bootstrap.AttributeName = TagSuffix ?? string.Empty;

                    // === ADD: propagar EnableLogs al servicio y al ValueReader ===
                    var tbs = _bootstrap as TreeBootstrapService;
                    if (tbs != null) tbs.EnableLogs = EnableLogs;
                    LateralMenu.Services.ValueReader.EnableLogs = EnableLogs;
                    // ============================================================

                    var list = ListControl;
                    if (list != null)
                        list.ItemsSource = _bootstrap.Roots;
                }
            }
        }


        // =============== Lifecycle ===============
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            // === ADD: pequeño banner de arranque ===
            if (EnableLogs) Logger.LogInfo(() => "LateralMenuControl Loaded: init + RefreshAsync");
            // =======================================

            var svc = EnsureBootstrap();
            BindChildItemsSource();
            ApplyHostPropsToChild();
            HookChildControlEvents();

            try
            {
                await RefreshAsync().ConfigureAwait(true);
                ApplyPresentationFromRootMode();
                ApplyHostPropsToChild();
            }
            catch (Exception ex)
            {
                LogError(() => "LateralMenuControl: RefreshAsync on Loaded threw: " + ex.Message, ex);
            }
        }


        private ITreeBootstrapService EnsureBootstrap()
        {
            if (_bootstrap != null) return _bootstrap;

            var local = new TreeBootstrapService
            {
                AttributeName = TagSuffix ?? string.Empty,
                UIDispatcher = this.Dispatcher   // ⬅️ inyecta Dispatcher de UI
            };
            local.OneShotReaderAsync = ValueReader.ReadBulkAsync;

            // === ADD: propagar EnableLogs al servicio local y al ValueReader ===
            local.EnableLogs = EnableLogs;
            LateralMenu.Services.ValueReader.EnableLogs = EnableLogs;
            // ===================================================================

            Bootstrap = local; // setter enlaza ItemsSource
            return _bootstrap;
        }


        private void BindChildItemsSource()
        {
            var list = ListControl;
            if (list != null && _bootstrap != null)
                list.ItemsSource = _bootstrap.Roots;
        }

        // =============== Refresh (carga/proyección plana) ===============
        public async Task RefreshAsync()
        {
            if (_bootstrap == null) EnsureBootstrap();

            if (_bootstrap == null)
            {
                LogError(() => "LateralMenuControl.RefreshAsync: Bootstrap service is NULL.");
                return;
            }

            if (DataSubscription == null)
            {
                LogError(() => "LateralMenuControl.RefreshAsync: DataSubscription is NULL.");
                return;
            }

            // >>> EXÁCTAMENTE AQUÍ: expón la suscripción al modelo TreeNode
            LateralMenu.Models.TreeNode.ResolveSubscription = () => this.DataSubscription;

            string startPath;
            string userRolesCsv;
            try
            {
                startPath = Navigation.CurrentPath;
                userRolesCsv = Security.LoggedInUserRoles;
            }
            catch (Exception ex)
            {
                LogError(() => "LateralMenuControl.RefreshAsync: failed to read OMI context: " + ex.Message, ex);
                return;
            }

            try
            {
                await _bootstrap.LoadAndProject(startPath, userRolesCsv, DataSubscription).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(() => "LateralMenuControl.RefreshAsync: LoadAndProject threw: " + ex.Message, ex);
            }
        }



        // =============== Presentación condicionada desde RootMode ===============
        private void ApplyPresentationFromRootMode()
        {
            if (_bootstrap == null) return;

            // Reglas:
            // - Ambito  → no group, no parent title
            // - Sistema → group, show parent title
            // - Generico(default) → group, no parent title
            var mode = _bootstrap.LastRootMode;

            if (mode == RootMode.Ambito)
            {
                GroupByValue = false;
                ShowParentTitle = false;
                FilteringEnabled = true;
            }
            else if (mode == RootMode.Sistema)
            {
                GroupByValue = true;
                ShowParentTitle = true;
                FilteringEnabled = true;
            }
            else
            {
                GroupByValue = true;
                ShowParentTitle = false;
                FilteringEnabled = true;
            }

            ResetFilterSelection();
            ApplyHostPropsToChild();

            var list = ListControl;
            list?.ForceRebindAndRefresh();
        }

        // =============== DP Change Handlers ===============
        private static void OnPresentationFlagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.ApplyHostPropsToChild();
        }

        private static void OnTagSuffixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            var newVal = e.NewValue as string ?? string.Empty;
            if (host._bootstrap != null)
                host._bootstrap.AttributeName = newVal;
        }

        private void ResetFilterSelection()
        {
            if (!string.Equals(FilterSelected ?? string.Empty, string.Empty, StringComparison.Ordinal))
                FilterSelected = string.Empty;
        }

        private void ApplyHostPropsToChild()
        {
            var list = ListControl;
            if (list == null) return;

            list.EnableLogs = EnableLogs;
            list.ShowParentTitle = ShowParentTitle;
            list.GroupByValue = GroupByValue;
            list.SortBySeverity = SortBySeverity;
            list.SeveritySortDescending = SeveritySortDescending;

            // === Nuevo: propagamos filtrado ===
            list.FilteringEnabled = FilteringEnabled;
            ApplyFilterSelectedToChild(FilterSelected);
        }

        private static void OnFilterSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (LateralMenuControl)d;
            host.ApplyFilterSelectedToChild(e.NewValue as string ?? string.Empty);
        }

        private void ApplyFilterSelectedToChild(string csv)
        {
            var list = ListControl;
            if (list == null) return;

            var normalized = csv ?? string.Empty;
            if (!string.Equals(list.FilterSelected ?? string.Empty, normalized, StringComparison.Ordinal))
                list.FilterSelected = normalized;
        }


        // =============== Child Control Wiring ===============
        private void HookChildControlEvents()
        {
            var list = ListControl;
            if (list == null)
            {
                LogError(() => "LateralMenuControl: List (InstallationsListControl) not found. Ensure x:Name=\"List\" in XAML.");
                return;
            }

            list.ItemInvoked -= OnItemInvoked;
            list.ItemInvoked += OnItemInvoked;

            list.FilterValuesChanged -= OnListFilterValuesChanged;
            list.FilterValuesChanged += OnListFilterValuesChanged;

            list.ItemCountChanged -= OnListItemCountChanged;
            list.ItemCountChanged += OnListItemCountChanged;
        }

        private void OnItemInvoked(object sender, TreeNodeEventArgs e)
        {
            if (e == null || e.Node == null) return;
            try
            {
                // Navegación directa (sin DP): escribe en OMI
                Navigation.CurrentPath = e.Node.Path;
            }
            catch (Exception ex)
            {
                LogError(() => "LateralMenuControl.OnItemInvoked: failed to set Navigation.CurrentPath: " + ex.Message, ex);
            }
        }

        private void OnListFilterValuesChanged(object sender, string csv)
        {
            FilterValues = csv ?? string.Empty;
        }

        private void OnListItemCountChanged(object sender, int count)
        {
            ItemCount = count;
        }

        private InstallationsListControl ListControl
        {
            get { return this.FindName("List") as InstallationsListControl; }
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
