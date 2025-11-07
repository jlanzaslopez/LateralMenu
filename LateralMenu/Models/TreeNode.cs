using ArchestrA.Client.Navigation;  // AlarmData (ajusta el namespace si difiere)
using ArchestrA.Client.RuntimeData;
using ArchestrA.Client.ViewApp;
using ArchestrA.Diagnostics; // Logger (ajusta el namespace si difiere)  
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;               // Application
using System.Windows.Threading;     // Dispatcher

namespace LateralMenu.Models
{
    public enum AlarmState
    {
        Ok = 0,
        Warning = 1,
        Alarm = 2
    }

    public sealed class TreeNode : INotifyPropertyChanged, IDisposable
    {
        // =============== Identidad / Display ===============
        public Guid Id { get; } = Guid.NewGuid();


        public static Func<ArchestrA.Client.RuntimeData.DataSubscription> ResolveSubscription { get; set; }
        public bool EnableLogs { get; set; } = false; // Controla si se registran logs detallados   

        public string Title { get; set; }
        public string Name { get; }              // Referencia OMI inmutable (para valores/alarma)
        public string Path { get; set; }         // Ruta lógica/física (para navegación)
        public string ParentTitle { get; set; }  // Para tooltips cuando proyectamos nietos

        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        // =============== Seguridad / Acceso ===============
        /// <summary>Roles requeridos por este nodo. Lista vacía o "Unconfigured" => público.</summary>
        public List<string> LoggedInUserRoles { get; } = new List<string>();

        // =============== Estado de alarma ===============
        private AlarmState _alarmState = AlarmState.Ok;
        public AlarmState AlarmState
        {
            get => _alarmState;
            private set
            {
                if (_alarmState != value)
                {
                    _alarmState = value;
                    OnPropertyChanged(nameof(AlarmState));
                }
            }
        }

        private AlarmData _alarmData;

        public TreeNode(string title, string name, string path, string parentTitle = null, bool enableLogs = false)
        {
            Title = string.IsNullOrWhiteSpace(title) ? name : title;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Path = path;
            ParentTitle = parentTitle;

            EnableLogs = enableLogs; // Establecemos EnableLogs

            // Log: Comienza la inicialización de AlarmData
            if (EnableLogs) Logger.LogInfo(() => $"TreeNode ctor: Inicializando AlarmData para {Name} en {Path}");

            InitializeAlarmData();
        }


        // ======= AlarmData wiring (reemplaza todo tu Initialize/Update/OnChanged) =======
        // En TreeNode (en la clase):
        // 

        private void InitializeAlarmData()
        {
            DisposeAlarmData();

            // Preferimos la ruta completa si existe; si no, el Name.
            string refTried = Name;

            try
            {
                Logger.LogInfo(() =>
                    $"TreeNode.InitializeAlarmData: intentando crear AlarmData con ref='{refTried}'.");

                // 1) Obtener la DataSubscription desde el host (LateralMenuControl debe asignar ResolveSubscription)
                var sub = ResolveSubscription != null ? ResolveSubscription() : null;
                if (sub == null)
                {
                    Logger.LogWarning(() =>
                        $"TreeNode.InitializeAlarmData: DataSubscription NULL (ResolveSubscription no configurado). " +
                        $"No se puede crear AlarmData para '{refTried}'.");
                    AlarmState = AlarmState.Ok;
                    return;
                }

                // 2) Obtener el provider real desde ViewApplication
                var app = ArchestrA.Client.ViewApp.ViewApplication.Application;
                var provider = app.GetService("ArchestrA.Client.Navigation.Internal.AlarmDataProviderInternal");
                if (provider == null)
                {
                    Logger.LogWarning(() =>
                        $"TreeNode.InitializeAlarmData: provider NULL. reference='{refTried}'.");
                    AlarmState = AlarmState.Ok;
                    return;
                }

                // 3) Resolver Create(string, DataSubscription)
                var createMethod = provider.GetType().GetMethod(
                    "Create",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                    binder: null,
                    types: new[] {
                typeof(string),
                typeof(ArchestrA.Client.RuntimeData.DataSubscription)
                    },
                    modifiers: null);

                if (createMethod == null)
                {
                    Logger.LogWarning(() =>
                        $"TreeNode.InitializeAlarmData: método Create(string, DataSubscription) no encontrado en '{provider.GetType().FullName}'.");
                    AlarmState = AlarmState.Ok;
                    return;
                }

                // 4) Invocar Create(ref, subscription)
                _alarmData = (ArchestrA.Client.Navigation.AlarmData)
                    createMethod.Invoke(provider, new object[] { refTried, sub });

                if (_alarmData == null)
                {
                    Logger.LogWarning(() =>
                        $"TreeNode.InitializeAlarmData: Create devolvió NULL para '{refTried}'.");
                    AlarmState = AlarmState.Ok;
                    return;
                }

                // 5) Suscribirse y lectura inicial
                _alarmData.DataChanged += OnAlarmDataChanged;
                Logger.LogInfo(() =>
                    $"TreeNode.InitializeAlarmData: AlarmData creado y suscrito OK para '{refTried}'.");

                try
                {
                    UpdateAlarmStateFromAlarmData();
                }
                catch (Exception exInit)
                {
                    Logger.LogWarning(() =>
                        $"TreeNode.InitializeAlarmData: lectura inicial no crítica fallida. {exInit.Message}");
                }
            }
            catch (System.Reflection.TargetInvocationException tiex)
            {
                Logger.LogError(() =>
                    $"TreeNode.InitializeAlarmData: error de invocación Create(ref, sub) para '{refTried}': {tiex.InnerException?.Message ?? tiex.Message}", tiex);
                _alarmData = null;
                AlarmState = AlarmState.Ok;
            }
            catch (Exception ex)
            {
                Logger.LogError(() =>
                    $"TreeNode.InitializeAlarmData: EX creando AlarmData para '{refTried}': {ex.Message}", ex);
                _alarmData = null;
                AlarmState = AlarmState.Ok;
            }
        }

        private void OnAlarmDataChanged(object sender, EventArgs e)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp != null && !disp.CheckAccess())
            {
                // marshall a UI thread
                disp.InvokeAsync(() =>
                {
                    try
                    {
                        Logger.LogInfo(() =>
                            $"TreeNode.OnAlarmDataChanged: DataChanged para '{Title}' ('{Path}'). Actualizando estado...");
                        UpdateAlarmStateFromAlarmData();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(() =>
                            $"TreeNode.OnAlarmDataChanged: EX actualizando estado para '{Title}'. {ex.Message}", ex);
                    }
                }, DispatcherPriority.Background);
            }
            else
            {
                try
                {
                    Logger.LogInfo(() =>
                        $"TreeNode.OnAlarmDataChanged: DataChanged para '{Title}' ('{Path}'). Actualizando estado...");
                    UpdateAlarmStateFromAlarmData();
                }
                catch (Exception ex)
                {
                    Logger.LogError(() =>
                        $"TreeNode.OnAlarmDataChanged (sync): EX actualizando estado para '{Title}'. {ex.Message}", ex);
                }
            }
        }

        private void UpdateAlarmStateFromAlarmData()
        {
            if (_alarmData == null)
            {
                Logger.LogWarning(() =>
                    $"TreeNode.UpdateAlarmState: _alarmData == null para '{Title}'. Dejo estado=Ok.");
                AlarmState = AlarmState.Ok;
                return;
            }

            // Algunos SDKs necesitan que haya “priming” antes de exponer contadores;
            // cualquier getter que falle lo capturamos y dejamos estado=Ok.
            int s1a = 0, s1u = 0, s2a = 0, s2u = 0, s3a = 0, s3u = 0, s4a = 0, s4u = 0;

            try { s1a = Safe(_alarmData.Severity1AckedTotal); } catch { }
            try { s1u = Safe(_alarmData.Severity1UnackedTotal); } catch { }
            try { s2a = Safe(_alarmData.Severity2AckedTotal); } catch { }
            try { s2u = Safe(_alarmData.Severity2UnackedTotal); } catch { }
            try { s3a = Safe(_alarmData.Severity3AckedTotal); } catch { }
            try { s3u = Safe(_alarmData.Severity3UnackedTotal); } catch { }
            try { s4a = Safe(_alarmData.Severity4AckedTotal); } catch { }
            try { s4u = Safe(_alarmData.Severity4UnackedTotal); } catch { }

            int s1 = s1a + s1u;
            int s2 = s2a + s2u;
            int s3 = s3a + s3u;
            int s4 = s4a + s4u;

            Logger.LogInfo(() =>
                $"TreeNode.UpdateAlarmState: '{Title}' S1={s1} (A{s1a}/U{s1u}) S2={s2} (A{s2a}/U{s2u}) S3={s3} (A{s3a}/U{s3u}) S4={s4} (A{s4a}/U{s4u}).");

            if ((s1 + s2) > 0)
                AlarmState = AlarmState.Alarm;
            else if ((s3 + s4) > 0)
                AlarmState = AlarmState.Warning;
            else
                AlarmState = AlarmState.Ok;
        }

        private static int Safe(int v) => v < 0 ? 0 : v;

        private void DisposeAlarmData()
        {
            if (_alarmData != null)
            {
                try
                {
                    _alarmData.DataChanged -= OnAlarmDataChanged;
                    // Log: Desuscripción de eventos de AlarmData
                    Logger.LogInfo(() => $"TreeNode.DisposeAlarmData: Desuscripción de eventos para {Name} - {Path}");
                }
                catch (Exception ex)
                {
                    // Log: Error al desuscribir eventos
                    Logger.LogError(() => $"TreeNode.DisposeAlarmData: Error al desuscribir eventos para {Name} - {Path}: {ex.Message}", ex);
                }

                try
                {
                    _alarmData.Release();
                    // Log: Liberación de AlarmData
                    Logger.LogInfo(() => $"TreeNode.DisposeAlarmData: Liberación de AlarmData para {Name} - {Path}");
                }
                catch (Exception ex)
                {
                    // Log: Error al liberar AlarmData
                    Logger.LogError(() => $"TreeNode.DisposeAlarmData: Error al liberar AlarmData para {Name} - {Path}: {ex.Message}", ex);
                }

                _alarmData = null;
            }
        }


        // =============== INotifyPropertyChanged ===============
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // =============== IDisposable ===============
        public void Dispose()
        {
            DisposeAlarmData();
        }
    }
}
