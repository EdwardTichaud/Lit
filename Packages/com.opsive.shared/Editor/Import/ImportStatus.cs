/// ---------------------------------------------
/// Opsive Shared
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.Shared.Editor.Import
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Serialization;

    /// <summary>
    /// Small ScriptableObject which shows the import window if it has not been shown.
    /// </summary>
    public class ImportStatus : ScriptableObject
    {
        [Tooltip("Has the Character Controller Update Project Settings window been shown?")]
        [SerializeField] protected bool m_CharacterProjectSettingsShown;
        [Tooltip("Has the Behavior Designer Welcome window been shown?")]
        [SerializeField] protected bool m_BehaviorWindowShown;
        [Tooltip("Has the State Designer Welcome window been shown?")]
        [FormerlySerializedAs("m_InitializedPackageNames")]
        [SerializeField] protected string[] m_StartupWindowShownPackageNames;

        /// <summary>
        /// Gets or sets whether the Character Controller project settings window has been shown.
        /// </summary>
        public bool CharacterProjectSettingsShown { get { return m_CharacterProjectSettingsShown; } set { m_CharacterProjectSettingsShown = value; } }

        /// <summary>
        /// Gets or sets whether the Behavior Designer welcome window has been shown.
        /// </summary>
        public bool BehaviorWindowShown { get { return m_BehaviorWindowShown; } set { m_BehaviorWindowShown = value; } }

        /// <summary>
        /// Gets the package names whose startup window has already been shown.
        /// </summary>
        public string[] StartupWindowShownPackageNames { get { return m_StartupWindowShownPackageNames ?? Array.Empty<string>(); } }

        /// <summary>
        /// Returns true if the specified package name has already had its startup window shown.
        /// </summary>
        /// <param name="packageName">The package name to check.</param>
        /// <returns>True if the package name is already tracked.</returns>
        public bool ContainsStartupWindowShownPackage(string packageName)
        {
            if (string.IsNullOrEmpty(packageName) || m_StartupWindowShownPackageNames == null) {
                return false;
            }

            return Array.IndexOf(m_StartupWindowShownPackageNames, packageName) != -1;
        }

        /// <summary>
        /// Adds the specified package name to the tracked startup-window list if it is not already present.
        /// </summary>
        /// <param name="packageName">The package name to add.</param>
        /// <returns>True if the package name was added.</returns>
        public bool AddStartupWindowShownPackage(string packageName)
        {
            if (string.IsNullOrEmpty(packageName) || ContainsStartupWindowShownPackage(packageName)) {
                return false;
            }

            if (m_StartupWindowShownPackageNames == null) {
                m_StartupWindowShownPackageNames = new[] { packageName };
            } else {
                var packageNames = new string[m_StartupWindowShownPackageNames.Length + 1];
                Array.Copy(m_StartupWindowShownPackageNames, packageNames, m_StartupWindowShownPackageNames.Length);
                packageNames[m_StartupWindowShownPackageNames.Length] = packageName;
                m_StartupWindowShownPackageNames = packageNames;
            }

            EditorUtility.SetDirty(this);
            return true;
        }
    }
}