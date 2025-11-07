using ArchestrA.Client.Navigation;
using ArchestrA.Client.RuntimeData;
using LateralMenu.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace LateralMenu.Services
{
    /// <summary>
    /// Servicio de bootstrap/proyección:
    /// - Lee el value del root (no se pinta) y decide si proyectar HIJOS o NIETOS.
    /// - Aplica seguridad por roles (vacío/"Unconfigured" => público).
    /// - Hidrata values de los nodos proyectados.
    /// - Proyección plana: Roots contiene SOLO los elementos a pintar.
    /// </summary>
    public interface ITreeBootstrapService
    {
        ObservableCollection<TreeNode> Roots { get; }

        /// <summary>Sufijo de atributo para construir las referencias de valor (p.ej. "TEXT").</summary>
        string AttributeName { get; set; }

        RootMode LastRootMode { get; set; }

        /// <summary>Delegate opcional para lecturas bulk (DataSubscription). Si es null, se saltan lecturas.</summary>
        Func<DataSubscription, IReadOnlyList<string>, Task<IDictionary<string, object>>> OneShotReaderAsync { get; set; }

        /// <summary>Indica si hay datos proyectados en Roots.</summary>
        bool IsTreeLoaded { get; }

        /// <summary>
        /// 1) Obtiene root por path y lee su value.
        /// 2) Si value == "Sistema": proyecta NIETOS (saltando hijos no visibles).
        ///    Si no: proyecta HIJOS.
        /// 3) Hidrata los values de Roots.
        /// </summary>
        Task LoadAndProject(string startPath, string userRolesCsv, DataSubscription subscription);

        /// <summary>Vacía Roots y libera recursos (Dispose de nodos) si aplica.</summary>
        void Clear();
    }
}
