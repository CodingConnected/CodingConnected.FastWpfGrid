using System;

namespace CodingConnected.FastWpfGrid
{
    public class RowClickEventArgs : EventArgs
    {
        public int Row;
        public FastGridControl Grid;
        public bool Handled;
    }

    public class ColumnClickEventArgs : EventArgs
    {
        public int Column;
        public FastGridControl Grid;
        public bool Handled;
    }
}
