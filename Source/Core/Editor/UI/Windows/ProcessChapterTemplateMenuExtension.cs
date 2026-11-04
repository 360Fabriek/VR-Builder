using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRBuilder.Core.Editor.ProcessAssets;
using VRBuilder.Core.Editor.UndoRedo;

namespace VRBuilder.Core.Editor.UI.Windows
{
    /// <summary>
    /// Adds chapter template export and import to the process sidebar.
    /// </summary>
    public static class ProcessChapterTemplateMenuExtension
    {
        internal static void Export(IChapter chapter)
        {
            string fileName = string.Concat((chapter.Data.Name ?? "Chapter").Split(Path.GetInvalidFileNameChars()));
            string path = EditorUtility.SaveFilePanel("Export Chapter Template", Application.dataPath,
                fileName, ProcessChapterTemplates.FileExtension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                ProcessChapterTemplates.Export(chapter, path);
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorUtility.DisplayDialog("Chapter Template Export Failed", exception.Message, "OK");
            }
        }

        internal static void Import(IProcess process, IChapter previousChapter)
        {
            string path = EditorUtility.OpenFilePanel("Import Chapter Template (trusted files only)",
                Application.dataPath, ProcessChapterTemplates.FileExtension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            IChapter chapter;
            try
            {
                // Finish deserialization and cloning before modifying the destination process.
                chapter = ProcessChapterTemplates.Import(path);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorUtility.DisplayDialog("Chapter Template Import Failed", exception.Message, "OK");
                return;
            }

            int index = previousChapter == null ? process.Data.Chapters.Count : process.Data.Chapters.IndexOf(previousChapter) + 1;
            RevertableChangesHandler.Do(new ProcessCommand(
                () =>
                {
                    process.Data.Chapters.Insert(index, chapter);
                    GlobalEditorHandler.RequestNewChapter(chapter);
                },
                () =>
                {
                    process.Data.Chapters.Remove(chapter);
                    GlobalEditorHandler.RequestNewChapter(previousChapter ?? process.Data.Chapters.FirstOrDefault());
                }));
        }
    }
}
