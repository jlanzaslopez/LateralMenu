using ArchestrA.Client.MyViewApp;
using ArchestrA.Client.Navigation;       // Navigation.CurrentPath
using ArchestrA.Client.RuntimeData;      // DataSubscription
// OJO: ajusta el using del Security según tu proyecto (p.ej. ArchestrA.Client.Security)
using ArchestrA.Diagnostics;             // Logger
using LateralMenu.Models;                // TreeNode
using LateralMenu.Services;              // ITreeBootstrapService
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace LateralMenu.ViewModels
{
    /// <summary>
    /// VM del menú lateral, acoplado a OMI:
    /// - Lee Navigation.CurrentPath y Security.LoggedInUserRoles internamente.
    /// - Llama al servicio para proyectar hijos/nietos y leer valores.
    /// </summary>
    public sealed class LateralMenuViewModel : INotifyPropertyChanged
    {
        private readonly ITreeBootstrapService _bootstrap;

        public LateralMenuViewModel(ITreeBootstrapService bootstrap)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        }

        public ObservableCollection<TreeNode> RootItems => _bootstrap.Roots;

        public bool IsTreeLoaded => _bootstrap.IsTreeLoaded;

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

        /// <summary>
        /// Refresca leyendo el contexto OMI internamente:
        /// - Navigation.CurrentPath
        /// - Security.LoggedInUserRoles
        /// </summary>
        public async Task RefreshAsync(DataSubscription subscription)
        {
            try
            {
                var startPath = Navigation.CurrentPath;           
                var userRolesCsv = Security.LoggedInUserRoles;    

                await _bootstrap.LoadAndProject(startPath, userRolesCsv, subscription).ConfigureAwait(false);

                OnPropertyChanged(nameof(RootItems));
                OnPropertyChanged(nameof(IsTreeLoaded));
            }
            catch (Exception ex)
            {
                Logger.LogError(() => $"LateralMenuViewModel.RefreshAsync falló: {ex.Message}", ex);
            }
        }

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
                Logger.LogError(() => "LateralMenuViewModel.Clear falló: " + ex.Message, ex);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
