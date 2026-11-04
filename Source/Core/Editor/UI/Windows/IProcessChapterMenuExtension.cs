namespace VRBuilder.Core.Editor.UI.Windows
{
    /// <summary>Adds chapter actions to the process editor sidebar.</summary>
    public interface IProcessChapterMenuExtension
    {
        void Draw(IProcess process, IChapter currentChapter);
    }
}
