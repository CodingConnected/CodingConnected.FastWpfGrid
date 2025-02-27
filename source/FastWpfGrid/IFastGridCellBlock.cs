using System.Windows.Media;

namespace FastWpfGrid
{
    public enum FastGridBlockType
    {
        Text,
        Image,
    }

    public enum MouseHoverBehaviours
    {
        HideWhenMouseOut,
        HideButtonWhenMouseOut,
        ShowAllWhenMouseOut,
    }

    public interface IFastGridCellBlock
    {
        FastGridBlockType BlockType { get; }

        Color? FontColor { get; }
        bool IsItalic { get; }
        bool IsBold { get; }
        string TextData { get; }

        string ImageSource { get; }
        int ImageWidth { get; }
        int ImageHeight { get; }

        MouseHoverBehaviours MouseHoverBehaviour { get; }
        object CommandParameter { get; }
        string ToolTip { get; }
    }
}
