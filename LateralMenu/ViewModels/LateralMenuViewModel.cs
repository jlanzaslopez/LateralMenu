using ArchestrA.Client.Navigation;       // NavigationItem
using ArchestrA.Client.RuntimeData;      // DataSubscription
using ArchestrA.Diagnostics;             // Logger (only for error cases)
using LateralMenu.Models;                   // TreeNode
using LateralMenu.Services;                 // ITreeBootstrapService
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace LateralMenu.ViewModels
{
    /// <summary>
    /// Shared navigation VM:
    /// - Thin façade over ITreeBootstrapService (single shared instance across the app).
    /// - No verbose logging; only error logs on exceptional cases.
    /// - Designed to back multiple navigation controls (list, tree, tiles…) that render the same model.
    /// </summary>
    public sealed class LateralMenuViewModel : INotifyPropertyChanged
    {
        private readonly ITreeBootstrapService _bootstrap;

        public LateralMenuViewModel(ITreeBootstrapService bootstrap)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        }

        /// <summary>Live collection of roots coming from the shared service.</summary>
        public ObservableCollection<TreeNode> RootItems => _bootstrap.Roots;

        /// <summary>True when Roots and the internal path index are ready.</summary>
        public bool IsTreeLoaded => _bootstrap.IsTreeLoaded;

        /// <summary>Suffix used to build value references (e.g., "TEXT"). Empty => skip value hydration.</summary>
        public string TagSuffix
        {
            get => _bootstrap.AttributeName;
            set
            {
                var newVal = string.IsNullOrEmpty(value) ? string.Empty : value;
                if (!string.Equals(_bootstrap.AttributeName, newVal, StringComparison.Ordinal))
                {
                    _bootstrap.AttributeName = newVal;
                    OnPropertyChanged(nameof(TagSuffix));
                }
            }
        }

        /// <summary>Content type to include when building the tree. Empty => skip content.</summary>
        public string SearchableContentType
        {
            get => _bootstrap.SearchableContentType;
            set
            {
                var newVal = string.IsNullOrEmpty(value) ? string.Empty : value;
                if (!string.Equals(_bootstrap.SearchableContentType, newVal, StringComparison.Ordinal))
                {
                    _bootstrap.SearchableContentType = newVal;
                    OnPropertyChanged(nameof(SearchableContentType));
                }
            }
        }

        /// <summary>
        /// Build/refresh only the subtree under the given path.
        /// merge=false replaces Roots; merge=true accumulates subtrees inside the shared model.
        /// </summary>
        public void LoadFromPath(string startPath, bool merge = false)
        {
            try
            {
                _bootstrap.LoadTreeFromPath(startPath, merge);
                OnPropertyChanged(nameof(RootItems));
                OnPropertyChanged(nameof(IsTreeLoaded));
            }
            catch (Exception ex)
            {
                Logger.LogError(() => $"LateralMenuViewModel.LoadFromPath('{startPath ?? ""}') failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Build/refresh only the subtree under the given NavigationItem.
        /// merge=false replaces Roots; merge=true accumulates.
        /// </summary>
        public void LoadFromItem(NavigationItem startItem, bool merge = false)
        {
            try
            {
                _bootstrap.LoadTreeFromItem(startItem, merge);
                OnPropertyChanged(nameof(RootItems));
                OnPropertyChanged(nameof(IsTreeLoaded));
            }
            catch (Exception ex)
            {
                Logger.LogError(() => "LateralMenuViewModel.LoadFromItem failed: " + ex.Message, ex);
            }
        }

        /// <summary>Fast resolver using the shared service's path index.</summary>
        public bool TryResolveNode(string path, out TreeNode node)
            => _bootstrap.TryGetNodeByPath(path, out node);

        /// <summary>Optional one-shot value hydration via the shared service.</summary>
        public Task BootstrapValuesAsync(DataSubscription subscription)
        {
            try
            {
                return _bootstrap.BootstrapValuesAsync(subscription);
            }
            catch (Exception ex)
            {
                Logger.LogError(() => "LateralMenuViewModel.BootstrapValuesAsync failed: " + ex.Message, ex);
                return Task.CompletedTask;
            }
        }

        /// <summary>Clear tree and internal maps in the shared service.</summary>
        public void Clear()
        {
            try
            {
                _bootstrap.Clear();
                OnPropertyChanged(nameof(RootItems));
                OnPropertyChanged(nameof(IsTreeLoaded));
            }
            catch (Exception ex)
            {
                Logger.LogError(() => "LateralMenuViewModel.Clear failed: " + ex.Message, ex);
            }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
    }
}
