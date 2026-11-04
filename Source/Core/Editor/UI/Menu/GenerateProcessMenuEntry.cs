// Copyright (c) 2026 MindPort GmbH
// Licensed under the Apache License, Version 2.0

using UnityEditor;
using UnityEngine;
using VRBuilder.Core.Editor.ProcessAssets;
using VRBuilder.Core.Entities.Factories;

namespace VRBuilder.Core.Editor.Menu
{
    internal class GenerateProcessMenuEntry : ScriptableWizard
    {
        [SerializeField]
        private string processName = "My VR Process";

        [MenuItem("Tools/VR Builder/Generate new process", false, 2)]
        private static void Open()
        {
            DisplayWizard<GenerateProcessMenuEntry>("Generate new process", "Generate");
        }

        [MenuItem("Tools/VR Builder/Generate new process", true)]
        private static bool ValidateOpen()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode == false;
        }

        private void OnWizardUpdate()
        {
            helpString = "Create a process asset without changing the scene setup or its selected process.";
            isValid = ProcessAssetUtils.CanCreate(processName.Trim(), out string error);
            errorString = error;
        }

        private void OnWizardCreate()
        {
            string name = processName.Trim();
            if (ProcessAssetUtils.CanCreate(name, out string error) == false)
            {
                UnityEngine.Debug.LogError(error);
                return;
            }

            ProcessAssetManager.Import(EntityFactory.CreateProcess(name));
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"Created process \"{name}\" without changing the scene setup.");
        }
    }
}
