﻿namespace FastWpfGrid
{
    public struct FastGridCellAddress
    {
        public static readonly FastGridCellAddress Empty = new FastGridCellAddress();
        public static readonly FastGridCellAddress GridHeader = new FastGridCellAddress(null, null, true);

        public bool Equals(FastGridCellAddress other)
        {
            return Row == other.Row && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is FastGridCellAddress && Equals((FastGridCellAddress) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Row.GetHashCode()*397) ^ Column.GetHashCode();
            }
        }

        public readonly int? Row;
        public readonly int? Column;
        public bool IsGridHeader;

        public FastGridCellAddress(int? row, int? col, bool isGridHeader = false)
        {
            Row = row;
            Column = col;
            IsGridHeader = isGridHeader;
        }

        public FastGridCellAddress ChangeRow(int? row)
        {
            return new FastGridCellAddress(row, Column, IsGridHeader);
        }

        public FastGridCellAddress ChangeColumn(int? col)
        {
            return new FastGridCellAddress(Row, col, IsGridHeader);
        }

        public bool IsCell => Row.HasValue && Column.HasValue;

        public bool IsRowHeader => Row.HasValue && !Column.HasValue;

        public bool IsColumnHeader => Column.HasValue && !Row.HasValue;

        public bool IsEmpty => Row == null && Column == null && !IsGridHeader;

        public bool TestCell(int row, int col)
        {
            return row == Row && col == Column;
        }

        public static bool operator ==(FastGridCellAddress a, FastGridCellAddress b)
        {
            return a.Row == b.Row && a.Column == b.Column && a.IsGridHeader == b.IsGridHeader;
        }

        public static bool operator !=(FastGridCellAddress a, FastGridCellAddress b)
        {
            return !(a == b);
        }
    }
}
