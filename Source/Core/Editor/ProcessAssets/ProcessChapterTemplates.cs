using System;
using System.IO;
using VRBuilder.Core.Editor.Configuration;

namespace VRBuilder.Core.Editor.ProcessAssets
{
    /// <summary>
    /// Stores reusable chapters using the configured process serializer.
    /// </summary>
    public static class ProcessChapterTemplates
    {
        public const string FileExtension = "chaptertemplate";

        /// <summary>
        /// Exports a detached copy so serialization cannot modify the chapter being edited.
        /// </summary>
        public static void Export(IChapter chapter, string path)
        {
            if (chapter == null)
            {
                throw new ArgumentNullException(nameof(chapter));
            }

            IChapter copy = EditorConfigurator.Instance.EntityCloner.Clone(chapter);
            byte[] data = EditorConfigurator.Instance.Serializer.ChapterToByteArray(copy);
            if (data == null || data.Length == 0)
            {
                throw new InvalidOperationException("The configured serializer returned no chapter template data.");
            }

            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// Loads a trusted template as an independent chapter with fresh entity IDs and remapped internal references.
        /// Scene object references retain their original identifiers; dependencies are not bundled.
        /// </summary>
        public static IChapter Import(string path)
        {
            IChapter chapter = EditorConfigurator.Instance.Serializer.ChapterFromByteArray(File.ReadAllBytes(path));
            if (chapter?.Data?.Steps == null)
            {
                throw new InvalidDataException("The selected file does not contain a chapter template.");
            }

            return EditorConfigurator.Instance.EntityCloner.Clone(chapter);
        }
    }
}
