**Project Description**
Fast WPF datagrid control designed to work with large datasets, with mandatory data virtualization.

This repository is a fork of https://fastwpfgrid.codeplex.com/

* fast DataGrid control for .NET - WPF
* designed for large data sets
* both columns and rows are defined in model
* uses MVVM design pattern, but (for performance reasons) does not use classic WPF binding
* works only with data virtualization (UI virtualization is not needed as in other WPF datagrid controls)
* for rendering [WriteableBitmapEx](http://writeablebitmapex.codeplex.com/) library is used

## Features
* Fast scrolling and rendering
* Excel-like mouse-drag selecting
* Hide columns/rows
* Frozen columns/rows
* Own rendering, WPF templates are not used. Supported objects - text (with italic, bold attributes), images, image buttons

![](Home_grid1.png)
![](Home_grid2.png)

## Model implementation
Grid control is bind to model, which controls displayed data. Below is example of model implementation.

{{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastWpfGrid;

namespace CodingConnected.FastWpfGridTest
{
    public class GridModel1 : FastGridModelBase
    {
        private Dictionary<Tuple<int, int>, string> _editedCells = new Dictionary<Tuple<int, int>, string>();
        private static string[]()() _columnBasicNames = new[]()() { "", "Value:", "Long column value:" };

        public override int ColumnCount
        {
            get { return 100; }
        }

        public override int RowCount
        {
            get { return 1000; }
        }

        public override string GetCellText(int row, int column)
        {
            var key = Tuple.Create(row, column);
            if (_editedCells.ContainsKey(key)) return _editedCells[key](key);


            return String.Format("{0}{1},{2}", _columnBasicNames[column % _columnBasicNames.Length](column-%-_columnBasicNames.Length), row + 1, column + 1);
        }

        public override void SetCellText(int row, int column, string value)
        {
            var key = Tuple.Create(row, column);
            _editedCells[key](key) = value;
        }

        public override IFastGridCell GetGridHeader(IFastGridView view)
        {
            var impl = new FastGridCellImpl();
            var btn = impl.AddImageBlock(view.IsTransposed ? "/Images/flip_horizontal_small.png" : "/Images/flip_vertical_small.png");
            btn.CommandParameter = FastWpfGrid.FastGridControl.ToggleTransposedCommand;
            btn.ToolTip = "Swap rows and columns";
            return impl;
        }
    }
}
}}