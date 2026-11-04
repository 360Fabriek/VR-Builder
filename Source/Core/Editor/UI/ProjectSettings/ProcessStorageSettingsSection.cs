using System;
using UnityEditor;
using VRBuilder.Core.Editor.Configuration;
using VRBuilder.Core.Editor.Settings;

namespace VRBuilder.Core.Editor.UI.ProjectSettings
{
    /// <summary>
    /// Project-wide storage format used when saving processes.
    /// </summary>
    public class ProcessStorageSettingsSection : IProjectSettingsSection
    {
        public string Title => "Process Storage";
        public Type TargetPageProvider => typeof(BuilderPageProvider);
        public int Priority => 500;

        public void OnGUI(string searchContext)
        {
            BuilderProjectSettings settings = BuilderProjectSettings.Load();
            bool hasOverride = EditorConfigurator.Instance.GetType()
                .GetProperty(nameof(DefaultEditorConfiguration.ProcessAssetStrategy)).GetMethod.DeclaringType != typeof(DefaultEditorConfiguration);

            using (new EditorGUI.DisabledScope(hasOverride))
            {
                ProcessAssetStorage selected = (ProcessAssetStorage)EditorGUILayout.EnumPopup("Process Asset Strategy", settings.ProcessStorage);
                if (selected != settings.ProcessStorage)
                {
                    Undo.RecordObject(settings, "Change Process Asset Strategy");
                    settings.ProcessStorage = selected;
                    settings.Save();
                }
            }

            if (hasOverride)
            {
                EditorGUILayout.HelpBox("The active custom editor configuration overrides ProcessAssetStrategy. Its override controls process storage instead of this setting.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Single File stores a process in one file. Split Chapters stores each chapter separately for easier version-control merges. Changes apply when a process is saved; existing processes are not converted in bulk.", MessageType.Info);
            }
        }
    }
}
