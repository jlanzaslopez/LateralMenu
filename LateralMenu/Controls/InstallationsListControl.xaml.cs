using LateralMenu.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;                         // <-- for SelectMany
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

    // New depth enum
    public enum ListDepthMode
    {
        FirstLevel = 0,       // ParentNode.Items (default)
        TwoLevelsFlat = 1    // ParentNode.Items.SelectMany(c => c.Items)
    }

    public partial class InstallationsListControl : UserControl
    {
        public InstallationsListControl()
        {
            InitializeComponent();
            Loaded += (_, __) => RebindAndRefresh();
        }

        #region ===== PUBLIC API (Inputs / Outputs) =====

        // Data source
        public TreeNode ParentNode
        {
            get => (TreeNode)GetValue(ParentNodeProperty);
            set => SetValue(ParentNodeProperty, value);
        }
        public static readonly DependencyProperty ParentNodeProperty =
            DependencyProperty.Register(nameof(ParentNode), typeof(TreeNode),
                typeof(InstallationsListControl),
                new PropertyMetadata(null, OnDataSourceChanged));

        public IEnumerable<TreeNode> ItemsSource
        {
            get => (IEnumerable<TreeNode>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<TreeNode>),
                typeof(InstallationsListControl),
                new PropertyMetadata(null, OnDataSourceChanged));

        // Behavior toggles
        public bool ShowParentTitle
        {
            get => (bool)GetValue(ShowParentTitleProperty);
            set => SetValue(ShowParentTitleProperty, value);
        }
        public static readonly DependencyProperty ShowParentTitleProperty =
            DependencyProperty.Register(nameof(ShowParentTitle), typeof(bool),
                typeof(InstallationsListControl), new PropertyMetadata(false));

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


        public ListDepthMode DepthMode
        {
            get => (ListDepthMode)GetValue(DepthModeProperty);
            set => SetValue(DepthModeProperty, value);
        }
        public static readonly DependencyProperty DepthModeProperty =
            DependencyProperty.Register(nameof(DepthMode), typeof(ListDepthMode),
                typeof(InstallationsListControl),
                new PropertyMetadata(ListDepthMode.FirstLevel, OnDataSourceChanged));

        // Icon geometries (5)
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

        public Geometry FavoriteIconData
        {
            get => (Geometry)GetValue(FavoriteIconDataProperty);
            set => SetValue(FavoriteIconDataProperty, value);
        }
        public static readonly DependencyProperty FavoriteIconDataProperty =
            DependencyProperty.Register(nameof(FavoriteIconData), typeof(Geometry),
                typeof(InstallationsListControl),
                new PropertyMetadata(Geometry.Parse(DEFAULT_FAVORITE_STAR)));

        // Events
        public event EventHandler<TreeNodeEventArgs> ItemInvoked;
        public event EventHandler<TreeNodeEventArgs> FavoriteToggleRequested;

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

        private const string DEFAULT_FAVORITE_STAR =
            "M11.2691 4.41115C11.5006 3.89177 11.6164 3.63208 11.7776 3.55211C11.9176 3.48263 12.082 3.48263 12.222 3.55211C12.3832 3.63208 12.499 3.89177 12.7305 4.41115L14.5745 8.54808C14.643 8.70162 14.6772 8.77839 14.7302 8.83718C14.777 8.8892 14.8343 8.93081 14.8982 8.95929C14.9705 8.99149 15.0541 9.00031 15.2213 9.01795L19.7256 9.49336C20.2911 9.55304 20.5738 9.58288 20.6997 9.71147C20.809 9.82316 20.8598 9.97956 20.837 10.1342C20.8108 10.3122 20.5996 10.5025 20.1772 10.8832L16.8125 13.9154C16.6877 14.0279 16.6252 14.0842 16.5857 14.1527C16.5507 14.2134 16.5288 14.2807 16.5215 14.3503C16.5132 14.429 16.5306 14.5112 16.5655 14.6757L17.5053 19.1064C17.6233 19.6627 17.6823 19.9408 17.5989 20.1002C17.5264 20.2388 17.3934 20.3354 17.2393 20.3615C17.0619 20.3915 16.8156 20.2495 16.323 19.9654L12.3995 17.7024C12.2539 17.6184 12.1811 17.5765 12.1037 17.56C12.0352 17.5455 11.9644 17.5455 11.8959 17.56C11.8185 17.5765 11.7457 17.6184 11.6001 17.7024L7.67662 19.9654C7.18404 20.2495 6.93775 20.3915 6.76034 20.3615C6.60623 20.3354 6.47319 20.2388 6.40075 20.1002C6.31736 19.9408 6.37635 19.6627 6.49434 19.1064L7.4341 14.6757C7.46898 14.5112 7.48642 14.429 7.47814 14.3503C7.47081 14.2807 7.44894 14.2134 7.41394 14.1527C7.37439 14.0842 7.31195 14.0279 7.18708 13.9154L3.82246 10.8832C3.40005 10.5025 3.18884 10.3122 3.16258 10.1342C3.13978 9.97956 3.19059 9.82316 3.29993 9.71147C3.42581 9.58288 3.70856 9.55304 4.27406 9.49336L8.77835 9.01795C8.94553 9.00031 9.02911 8.99149 9.10139 8.95929C9.16534 8.93081 9.2226 8.8892 9.26946 8.83718C9.32241 8.77839 9.35663 8.70162 9.42508 8.54808L11.2691 4.41115Z";
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
            => ((InstallationsListControl)d).RebindAndRefresh();

        private static void OnGroupingSortingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((InstallationsListControl)d).ApplyGroupingAndSorting();

        private static void OnInternalItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((InstallationsListControl)d).ApplyGroupingAndSorting();

        private void RebindAndRefresh()
        {
            IEnumerable<TreeNode> baseItems = null;

            if (ItemsSource != null)
            {
                baseItems = ItemsSource;
            }
            else if (ParentNode != null)
            {
                if (DepthMode == ListDepthMode.FirstLevel)
                {
                    baseItems = ParentNode.Items;
                }
                else // TwoLevelsFlat
                {
                    baseItems = ParentNode.Items?.SelectMany(c => c.Items);
                }
            }

            // Ignore content nodes everywhere
            _InternalItems = baseItems?
                .Where(n => n != null && !n.IsContent)
                .ToList();

            ApplyGroupingAndSorting();
        }


        private void ApplyGroupingAndSorting()
        {
            if (PART_List == null) return;
            var view = CollectionViewSource.GetDefaultView(_InternalItems);
            if (view == null) return;

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
        }

        #endregion

        #region ===== Interactions (rows) =====

        private void OnSelectionChangedClear(object sender, SelectionChangedEventArgs e)
        {
            var lv = sender as ListView;
            if (lv != null && lv.SelectedItem != null)
                lv.SelectedItem = null;
        }

        private void OnListMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            var lvi = ItemsControl.ContainerFromElement(PART_List, dep) as ListViewItem;
            if (lvi != null && lvi.DataContext is TreeNode node)
            {
                ItemInvoked?.Invoke(this, new TreeNodeEventArgs(node));
                e.Handled = true;
            }
        }

        private void OnFavoriteIconClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // don't change selection
            var fe = sender as FrameworkElement;
            if (fe != null && fe.DataContext is TreeNode node)
                FavoriteToggleRequested?.Invoke(this, new TreeNodeEventArgs(node));
        }

        #endregion

        #region ===== Group header chevron animation (code-behind) =====

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
        }

        private void OnHeaderChecked(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle == null) return;

            var parts = GetChevronParts(toggle);
            AnimateAngle(parts.rt, ChevronExpandedAngle);
        }

        private void OnHeaderUnchecked(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle == null) return;

            var parts = GetChevronParts(toggle);
            AnimateAngle(parts.rt, ChevronCollapsedAngle);
        }

        #endregion
    }
}
