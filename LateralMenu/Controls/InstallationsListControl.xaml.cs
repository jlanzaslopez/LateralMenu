using LateralMenu.Models;
using ArchestrA.Diagnostics;              // Logger
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;  // ToggleButton
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;      // DoubleAnimation
using System.Windows.Shapes;               // Path

namespace LateralMenu.Controls
{
    public class TreeNodeEventArgs : EventArgs
    {
        public TreeNodeEventArgs(TreeNode node) => Node = node;
        public TreeNode Node { get; }
    }

    public partial class InstallationsListControl : UserControl
    {
        public InstallationsListControl()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                LogInfo(() => "InstallationsListControl.Loaded → RebindAndRefresh()");
                RebindAndRefresh();
            };
        }

        public void ForceRebindAndRefresh()
        {
            LogInfo(() => "InstallationsListControl.ForceRebindAndRefresh() → RebindAndRefresh()");
            RebindAndRefresh();
        }

        public event EventHandler<string> FilterValuesChanged;
        public event EventHandler<int> ItemCountChanged;

        #region ===== Logging flag & helpers =====
        public static readonly DependencyProperty EnableLogsProperty =
            DependencyProperty.Register(nameof(EnableLogs), typeof(bool),
                typeof(InstallationsListControl), new PropertyMetadata(false));

        public bool EnableLogs
        {
            get => (bool)GetValue(EnableLogsProperty);
            set => SetValue(EnableLogsProperty, value);
        }

        private void LogInfo(Func<string> f)
        {
            if (EnableLogs && f != null) Logger.LogInfo(f);
        }

        // Cache local de selección ya normalizada durante el último rebind
        private HashSet<string> _filterSelectedSet;

        // Normaliza CSV a conjunto minúsculas, sin vacíos.
        private static HashSet<string> BuildSetFromCsv(string csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(csv)) return set;

            var parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var s = parts[i];
                if (s == null) continue;
                s = s.Trim();
                if (s.Length == 0) continue;
                set.Add(s);
            }
            return set;
        }


        #endregion

        #region ===== PUBLIC API (Inputs / Outputs) =====

        // Fuente de datos principal: lista plana de TreeNode
        public IEnumerable<TreeNode> ItemsSource
        {
            get => (IEnumerable<TreeNode>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<TreeNode>),
                typeof(InstallationsListControl),
                new PropertyMetadata(null, OnDataSourceChanged));

        // Mostrar o no el ParentTitle (se usa en el DataTemplate)
        public bool ShowParentTitle
        {
            get => (bool)GetValue(ShowParentTitleProperty);
            set => SetValue(ShowParentTitleProperty, value);
        }
        public static readonly DependencyProperty ShowParentTitleProperty =
            DependencyProperty.Register(nameof(ShowParentTitle), typeof(bool),
                typeof(InstallationsListControl), new PropertyMetadata(false));

        // Agrupación y ordenación
        public bool GroupByValue
        {
            get => (bool)GetValue(GroupByValueProperty);
            set => SetValue(GroupByValueProperty, value);
        }
        public static readonly DependencyProperty GroupByValueProperty =
            DependencyProperty.Register(nameof(GroupByValue), typeof(bool),
                typeof(InstallationsListControl), new PropertyMetadata(false, OnGroupingSortingChanged));

        public bool SortBySeverity
        {
            get => (bool)GetValue(SortBySeverityProperty);
            set => SetValue(SortBySeverityProperty, value);
        }
        public static readonly DependencyProperty SortBySeverityProperty =
            DependencyProperty.Register(nameof(SortBySeverity), typeof(bool),
                typeof(InstallationsListControl), new PropertyMetadata(false, OnGroupingSortingChanged));

        public bool SeveritySortDescending
        {
            get => (bool)GetValue(SeveritySortDescendingProperty);
            set => SetValue(SeveritySortDescendingProperty, value);
        }
        public static readonly DependencyProperty SeveritySortDescendingProperty =
            DependencyProperty.Register(nameof(SeveritySortDescending), typeof(bool),
                typeof(InstallationsListControl), new PropertyMetadata(true, OnGroupingSortingChanged));


        // --- Filtering ---
        public bool FilteringEnabled
        {
            get { return (bool)GetValue(FilteringEnabledProperty); }
            set { SetValue(FilteringEnabledProperty, value); }
        }
        public static readonly DependencyProperty FilteringEnabledProperty =
            DependencyProperty.Register(nameof(FilteringEnabled), typeof(bool),
                typeof(InstallationsListControl),
                new PropertyMetadata(false, OnFilterChanged));

        public string FilterSelected
        {
            get { return (string)GetValue(FilterSelectedProperty); }
            set { SetValue(FilterSelectedProperty, value); }
        }
        public static readonly DependencyProperty FilterSelectedProperty =
            DependencyProperty.Register(nameof(FilterSelected), typeof(string),
                typeof(InstallationsListControl),
                new PropertyMetadata(string.Empty, OnFilterChanged));

        private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Reaplicar todo (rebind + filtro + grupo/orden)
            ((InstallationsListControl)d).RebindAndRefresh();
        }


        // Iconos de estado
        public Geometry StatusBackIconData
        {
            get => (Geometry)GetValue(StatusBackIconDataProperty);
            set => SetValue(StatusBackIconDataProperty, value);
        }
        public static readonly DependencyProperty StatusBackIconDataProperty =
            DependencyProperty.Register(nameof(StatusBackIconData), typeof(Geometry),
                typeof(InstallationsListControl),
                new PropertyMetadata(Geometry.Parse(DEFAULT_STATUS_BACK_PATH)));

        public Geometry StatusOkIconData
        {
            get => (Geometry)GetValue(StatusOkIconDataProperty);
            set => SetValue(StatusOkIconDataProperty, value);
        }
        public static readonly DependencyProperty StatusOkIconDataProperty =
            DependencyProperty.Register(nameof(StatusOkIconData), typeof(Geometry),
                typeof(InstallationsListControl),
                new PropertyMetadata(Geometry.Parse(DEFAULT_STATUS_OK_GLYPH)));

        public Geometry StatusWarningIconData
        {
            get => (Geometry)GetValue(StatusWarningIconDataProperty);
            set => SetValue(StatusWarningIconDataProperty, value);
        }
        public static readonly DependencyProperty StatusWarningIconDataProperty =
            DependencyProperty.Register(nameof(StatusWarningIconData), typeof(Geometry),
                typeof(InstallationsListControl),
                new PropertyMetadata(Geometry.Parse(DEFAULT_STATUS_WARNING_GLYPH)));

        public Geometry StatusAlarmIconData
        {
            get => (Geometry)GetValue(StatusAlarmIconDataProperty);
            set => SetValue(StatusAlarmIconDataProperty, value);
        }
        public static readonly DependencyProperty StatusAlarmIconDataProperty =
            DependencyProperty.Register(nameof(StatusAlarmIconData), typeof(Geometry),
                typeof(InstallationsListControl),
                new PropertyMetadata(Geometry.Parse(DEFAULT_STATUS_ALARM_GLYPH)));

        // Evento de selección / doble clic
        public event EventHandler<TreeNodeEventArgs> ItemInvoked;

        #endregion

        #region ===== DEFAULT ICON PATHS =====
        private const string DEFAULT_STATUS_BACK_PATH =
            "M9,1c4.4,0,8,3.6,8,8s-3.6,8-8,8S1,13.4,1,9,4.6,1,9,1Z";

        private const string DEFAULT_STATUS_OK_GLYPH =
            "M14.2,5.7l-.8-.8c-.3-.3-.8-.3-1.1,0l-4.9,4.9-2.2-2.2h0c-.3-.3-.8-.3-1.1,0l-.8.8h0c-.2.3-.2.8,0,1l3.5,3.5h0c.2.2.4.2.6.2s.4,0,.6-.2l6.1-6.1h.1c.1-.2.2-.4.2-.6,0-.2,0-.4-.2-.6Z";

        private const string DEFAULT_STATUS_WARNING_GLYPH =
            "M 126.546 48.662 L 128.546 48.662 C 128.822 48.662 129.046 48.886 129.046 49.162 L 129.046 56.162 C 129.046 56.438 128.822 56.662 128.546 56.662 L 126.546 56.662 C 126.27 56.662 126.046 56.438 126.046 56.162 L 126.046 49.162 C 126.046 48.886 126.27 48.662 126.546 48.662 Z M 129.046 59.162 C 129.046 59.99 128.374 60.662 127.546 60.662 C 126.718 60.662 126.046 59.99 126.046 59.162 C 126.046 58.334 126.718 57.662 127.546 57.662 C 128.374 57.662 129.046 58.334 129.046 59.162 Z";

        private const string DEFAULT_STATUS_ALARM_GLYPH =
            "M10.8,9l3-3c.1-.1.2-.3.2-.5s0-.4-.2-.5l-.9-.9c-.3-.3-.7-.3-1,0l-3,3-3-3c-.1-.1-.3-.2-.5-.2h0c-.2,0-.4,0-.5.2l-.9.9c-.1.1-.2.3-.2.5s0,.3.2.5l3,3-3,3c-.3.3-.3.7,0,1l.9.9c.1.1.3.2.5.2h0c.2,0,.4,0,.5-.2l3-3,3,3c.3.3.7.3,1,0l.9-.9c.3-.3.3-.7,0-1l-3-3Z";
        #endregion

        #region ===== Internal binding pivot & view shaping =====

        internal IEnumerable<TreeNode> _InternalItems
        {
            get => (IEnumerable<TreeNode>)GetValue(InternalItemsProperty);
            set => SetValue(InternalItemsProperty, value);
        }
        private static readonly DependencyProperty InternalItemsProperty =
            DependencyProperty.Register(nameof(_InternalItems), typeof(IEnumerable<TreeNode>),
                typeof(InstallationsListControl), new PropertyMetadata(null, OnInternalItemsChanged));

        private static void OnDataSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (InstallationsListControl)d;
            ctl.LogInfo(() =>
            {
                var oldCnt = (e.OldValue as IEnumerable<TreeNode>)?.Count() ?? 0;
                var newCnt = (e.NewValue as IEnumerable<TreeNode>)?.Count() ?? 0;
                return $"List: ItemsSource changed. oldCount={oldCnt}, newCount={newCnt}";
            });
            ctl.RebindAndRefresh();
        }

        private static void OnGroupingSortingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (InstallationsListControl)d;
            ctl.LogInfo(() => $"List: flag changed → {e.Property.Name}={(e.NewValue ?? "(null)")}. Reapplying grouping/sorting.");
            ctl.ApplyGroupingAndSorting();
        }

        private static void OnInternalItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (InstallationsListControl)d;
            ctl.LogInfo(() =>
            {
                var cnt = (e.NewValue as IEnumerable<TreeNode>)?.Count() ?? 0;
                return $"List: _InternalItems changed. count={cnt}";
            });
            ctl.ApplyGroupingAndSorting();
        }

        private void RebindAndRefresh()
        {
            // 1) Cargar ItemsSource base
            IEnumerable<TreeNode> sourceItems = ItemsSource;

            // 2) Construir CSV de valores disponibles para filtro (antes de aplicar filtro)
            var availableValuesCsv = BuildAvailableValuesCsv(sourceItems);

            // 3) Construir set del filtro seleccionado (normalizado una sola vez por rebind)
            _filterSelectedSet = BuildSetFromCsv(FilterSelected);

            // 4) Aplicar filtro (reglas: si FilteringEnabled == false OR set vacío => no filtra)
            IEnumerable<TreeNode> filteredItems = sourceItems;
            if (FilteringEnabled && _filterSelectedSet != null && _filterSelectedSet.Count > 0 && filteredItems != null)
            {
                // Filtramos por TreeNode.Value ∈ set
                filteredItems = filteredItems.Where(n =>
                    n != null &&
                    !string.IsNullOrWhiteSpace(n.Value) &&
                    _filterSelectedSet.Contains(n.Value.Trim()));
            }

            // 5) Persistir pivot interno y re-aplicar shaping
            _InternalItems = filteredItems?.Where(n => n != null).ToList();
            ApplyGroupingAndSorting();

            // 6) Notificar salidas al host (valores únicos + conteo)
            RaiseFilterValuesChanged(availableValuesCsv);
            RaiseItemCountChanged(_InternalItems?.Count() ?? 0);
        }

        private string BuildAvailableValuesCsv(IEnumerable<TreeNode> items)
        {
            if (items == null) return string.Empty;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in items)
            {
                var value = node?.Value;
                if (string.IsNullOrWhiteSpace(value)) continue;

                set.Add(value.Trim());
            }

            if (set.Count == 0) return string.Empty;

            return string.Join(
                ",",
                set
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
        }

        private void RaiseFilterValuesChanged(string csv)
        {
            LogInfo(() => $"List: RaiseFilterValuesChanged → '{csv}'");
            FilterValuesChanged?.Invoke(this, csv ?? string.Empty);
        }

        private void RaiseItemCountChanged(int count)
        {
            LogInfo(() => $"List: RaiseItemCountChanged → {count}");
            ItemCountChanged?.Invoke(this, count);
        }



        private void ApplyGroupingAndSorting()
        {
            if (PART_List == null)
            {
                LogInfo(() => "List: ApplyGroupingAndSorting → PART_List NULL (template not applied yet?)");
                return;
            }

            var view = CollectionViewSource.GetDefaultView(_InternalItems);
            if (view == null)
            {
                LogInfo(() => "List: ApplyGroupingAndSorting → no view (null _InternalItems).");
                return;
            }

            bool shouldGroup = GroupByValue;

            using (view.DeferRefresh())
            {
                view.GroupDescriptions?.Clear();
                view.SortDescriptions.Clear();

                if (shouldGroup)
                    view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TreeNode.Value)));

                if (SortBySeverity)
                {
                    var dir = SeveritySortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
                    view.SortDescriptions.Add(new SortDescription(nameof(TreeNode.AlarmState), dir));
                    view.SortDescriptions.Add(new SortDescription(nameof(TreeNode.Title), ListSortDirection.Ascending));
                }
            }

            PART_List.GroupStyle.Clear();
            if (shouldGroup)
            {
                var gs = (GroupStyle)Resources["DefaultGroupStyle"];
                PART_List.GroupStyle.Add(gs);
            }

            var count = _InternalItems?.Count() ?? 0;
            LogInfo(() => $"List: ApplyGroupingAndSorting → items={count}, groupByValue={shouldGroup}, sortBySeverity={SortBySeverity}, desc={SeveritySortDescending}");
        }

        #endregion

        #region ===== Interactions =====

        private void OnSelectionChangedClear(object sender, SelectionChangedEventArgs e)
        {
            var lv = sender as ListView;
            if (lv != null && lv.SelectedItem != null)
            {
                LogInfo(() => "List: SelectionChanged → clearing selection");
                lv.SelectedItem = null;
            }
        }

        private void OnListMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            var lvi = ItemsControl.ContainerFromElement(PART_List, dep) as ListViewItem;
            if (lvi != null && lvi.DataContext is TreeNode node)
            {
                LogInfo(() => $"List: DoubleClick → ItemInvoked path='{node?.Path ?? "(null)"}', title='{node?.Title ?? "(null)"}'");
                ItemInvoked?.Invoke(this, new TreeNodeEventArgs(node));
                e.Handled = true;
            }
            else
            {
                LogInfo(() => "List: DoubleClick → no item resolved (hit outside row?)");
            }
        }

        #endregion

        #region ===== Group header chevron animation =====

        private const double ChevronExpandedAngle = 0.0;   // down
        private const double ChevronCollapsedAngle = 90.0; // left
        private static readonly Duration ChevronAnimDuration = TimeSpan.FromMilliseconds(120);

        private static (Path path, RotateTransform rt) GetChevronParts(ToggleButton toggle)
        {
            if (toggle == null) return (null, null);

            var path = toggle.Template?.FindName("PART_ChevronPath", toggle) as Path;
            if (path == null) return (null, null);

            var rt = path.RenderTransform as RotateTransform;
            if (rt == null)
            {
                rt = new RotateTransform(ChevronExpandedAngle);
                path.RenderTransform = rt;
            }
            return (path, rt);
        }

        private static void AnimateAngle(RotateTransform rt, double toAngle)
        {
            if (rt == null) return;

            var da = new DoubleAnimation
            {
                To = toAngle,
                Duration = ChevronAnimDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            rt.BeginAnimation(RotateTransform.AngleProperty, da, HandoffBehavior.SnapshotAndReplace);
        }

        private void OnHeaderToggleLoaded(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle == null) return;

            var parts = GetChevronParts(toggle);
            var rt = parts.rt;
            if (rt == null) return;

            rt.Angle = (toggle.IsChecked == true) ? ChevronExpandedAngle : ChevronCollapsedAngle;
            LogInfo(() => $"List: GroupHeader Loaded → IsExpanded={toggle.IsChecked}");
        }

        private void OnHeaderChecked(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle == null) return;

            var parts = GetChevronParts(toggle);
            AnimateAngle(parts.rt, ChevronExpandedAngle);
            LogInfo(() => "List: GroupHeader Checked → expand");
        }

        private void OnHeaderUnchecked(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle == null) return;

            var parts = GetChevronParts(toggle);
            AnimateAngle(parts.rt, ChevronCollapsedAngle);
            LogInfo(() => "List: GroupHeader Unchecked → collapse");
        }

        #endregion
    }
}
