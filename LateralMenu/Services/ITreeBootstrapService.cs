using ArchestrA.Client.Navigation;
using ArchestrA.Client.RuntimeData;
using LateralMenu.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace LateralMenu.Services
{
    public interface ITreeBootstrapService
    {
        ObservableCollection<TreeNode> Roots { get; }

        string AttributeName { get; set; }
        string SearchableContentType { get; set; }

        /// <summary>Optional one-shot reader delegate. If null, value hydration is skipped.</summary>
        Func<DataSubscription, IReadOnlyList<string>, Task<IDictionary<string, object>>> OneShotReaderAsync { get; set; }

        /// <summary>True when Roots and the internal path index are ready.</summary>
        bool IsTreeLoaded { get; }

        /// <summary>Populate only the subtree under startPath. merge=false replaces Roots; merge=true accumulates.</summary>
        void LoadTreeFromPath(string startPath, bool merge = false);

        /// <summary>Populate only the subtree under startItem. merge=false replaces Roots; merge=true accumulates.</summary>
        void LoadTreeFromItem(NavigationItem startItem, bool merge = false);

        /// <summary>Fast O(1) lookup of an already-built node by its exact path.</summary>
        bool TryGetNodeByPath(string path, out TreeNode node);

        /// <summary>One-shot value hydration for prepared references.</summary>
        Task BootstrapValuesAsync(DataSubscription subscription);

        /// <summary>Clear tree and all internal maps/indexes.</summary>
        void Clear();
    }
}
