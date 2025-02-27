using System.Collections.Generic;
using System.Data;

namespace FastWpfGrid
{
    public class DataTableGridModel : FastGridModelBase
    {
        private DataTable _dataSource;
        public List<ExplicitColumnDefinition> ExplicitColumns;

        public override int ColumnCount
        {
            get
            {
                if (ExplicitColumns != null) return ExplicitColumns.Count;
                if (_dataSource != null) return _dataSource.Columns.Count;
                return 0;
            }
        }

        public override int RowCount => _dataSource != null ? _dataSource.Rows.Count : 0;

        public override string GetCellText(int row, int column)
        {
            if (_dataSource != null && row < _dataSource.Rows.Count)
            {
                if (ExplicitColumns != null)
                {
                    if (column < ExplicitColumns.Count)
                    {
                        var value = _dataSource.Rows[row][ExplicitColumns[column].DataField];
                        if (value != null) return value.ToString();
                    }
                }
                else
                {
                    var value = _dataSource.Rows[row][column];
                    if (value != null) return value.ToString();
                }
            }
            return "";
        }

        public override string GetColumnHeaderText(int column)
        {
            if (ExplicitColumns != null)
            {
                if (column < ExplicitColumns.Count) return ExplicitColumns[column].HeaderText;
                return "";
            }
            if (_dataSource != null && column < _dataSource.Columns.Count)
            {
                return _dataSource.Columns[column].ColumnName;
            }
            return "";
        }

        public DataTable DataSource
        {
            get => _dataSource;
            set
            {
                _dataSource = value;
                NotifyRefresh();
            }
        }
    }
}
