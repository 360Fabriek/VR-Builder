using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRBuilder.Core.Configuration;
using VRBuilder.Core.Editor.ProcessAssets;
using VRBuilder.Core.Editor.ProcessValidation;
using VRBuilder.Core.SceneObjects;

namespace VRBuilder.Core.Editor.Unity
{
    /// <summary>
    /// Validates the selected process in every scene in the actual player build request.
    /// Runs on every build, including incremental builds whose scene-processing callbacks may be cached.
    /// </summary>
    public sealed class ProcessBuildValidator : BuildPlayerProcessor
    {
        public override int callbackOrder => -1000;

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            // The existing build hooks save/close the process editor too. Flush its data before reading process files.
            GlobalEditorHandler.ProjectIsGoingToSave();
            ValidateScenes(buildPlayerContext.BuildPlayerOptions.scenes);
        }

        internal static void ValidateScenes(IEnumerable<string> scenePaths)
        {
            List<string> issues = new List<string>();
            foreach (string path in scenePaths.Distinct())
            {
                Scene scene = default;
                bool previousSuppression = ProcessSceneObject.SuppressEditorRegistration;
                try
                {
                    // Unity invokes Awake while loading, before setting the scene's preview flag.
                    ProcessSceneObject.SuppressEditorRegistration = true;
                    // Inspect the saved scene without replacing, saving, or closing the user's open scenes.
                    scene = EditorSceneManager.OpenPreviewScene(path);
                    foreach (string issue in ValidateScene(scene)) issues.Add($"Scene '{path}': {issue}");
                }
                catch (Exception exception)
                {
                    issues.Add($"Scene '{path}': validation could not complete ({exception.GetType().Name}): {exception.Message}");
                }
                finally
                {
                    try
                    {
                        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                    }
                    finally { ProcessSceneObject.SuppressEditorRegistration = previousSuppression; }
                }
            }
            if (issues.Count > 0)
                throw new BuildFailedException("Process validation failed. Fix these issues before building:\n" + string.Join("\n", issues));
        }

        private static IEnumerable<string> ValidateScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            RuntimeConfigurator[] configurations = roots.SelectMany(root => root.GetComponentsInChildren<RuntimeConfigurator>(true))
                .Where(configuration => configuration.gameObject.activeInHierarchy).ToArray();
            // Menu and utility scenes without a process configuration are valid build scenes.
            if (configurations.Length == 0) yield break;
            if (configurations.Length > 1)
            {
                yield return "More than one active RuntimeConfigurator is present; the selected process is ambiguous.";
                yield break;
            }

            string selectedProcess = configurations[0].GetSelectedProcess();
            if (string.IsNullOrWhiteSpace(selectedProcess))
            {
                yield return $"RuntimeConfigurator '{configurations[0].name}' has no selected process.";
                yield break;
            }
            IProcess process = ProcessAssetManager.Load(ProcessAssetUtils.GetProcessNameFromPath(selectedProcess), false);
            if (process == null)
            {
                yield return $"Selected process '{selectedProcess}' is missing or could not be deserialized.";
                yield break;
            }

            Dictionary<Guid, List<ISceneObject>> objectsById = new Dictionary<Guid, List<ISceneObject>>();
            HashSet<Guid> primaryIds = new HashSet<Guid>();
            foreach (ISceneObject sceneObject in roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).OfType<ISceneObject>())
            {
                if (sceneObject.Guid == Guid.Empty || primaryIds.Add(sceneObject.Guid) == false)
                    yield return $"Scene object '{sceneObject.GameObject.name}' has an empty or duplicate object ID '{sceneObject.Guid}'.";
                foreach (Guid id in sceneObject.Guids.Append(sceneObject.Guid).Distinct())
                {
                    if (objectsById.TryGetValue(id, out List<ISceneObject> objects) == false)
                    {
                        objects = new List<ISceneObject>();
                        objectsById.Add(id, objects);
                    }
                    objects.Add(sceneObject);
                }
            }

            foreach (string issue in MissingReferenceWarnings.Validate(process,
                id => objectsById.TryGetValue(id, out List<ISceneObject> objects) ? objects : Enumerable.Empty<ISceneObject>()))
                yield return $"Selected process '{selectedProcess}': {issue}";
        }
    }
}
