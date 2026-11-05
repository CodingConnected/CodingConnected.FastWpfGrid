**Project Description**
Fast WPF datagrid control designed to work with large datasets, with mandatory data virtualization.

_*Update 2026-11-05 - the project now targets .NET 10, namespaces/assemblies are `CodingConnected.FastWpfGrid` and `CodingConnected.WriteableBitmapEx.Wpf`, version 1.1.0*_

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

## Changes in 1.1.0

* Target framework: .NET 10 (`net10.0-windows`).
* Assemblies/namespaces renamed to `CodingConnected.FastWpfGrid` / `CodingConnected.WriteableBitmapEx.Wpf`.
* Removed `WriteableBitmap.Freeze()` of the previous draw buffer. It was a .NET Framework-era GC
  workaround; on .NET 8/10 buffers are collected normally, and `Freeze()` destroys the bitmap's native
  surface permanently, which made `RenderGrid` throw `ArgumentNullException: SafeHandle cannot be null`.
* `RenderGrid` now catches rendering failures, raises `FastGridControl.RenderFailed`, recreates the draw
  buffer and re-renders instead of surfacing a `DispatcherUnhandledException`. After 3 consecutive
  failures it stops retrying until the next invalidation.
* `BitmapContext` is now a sealed class instead of a struct, so copying a context can no longer unlock
  a bitmap that another live context still uses. It registers in the static ref-count map only after
  `Lock()` succeeds, `Dispose()` is double-dispose-safe and always unlocks (even if `AddDirtyRect`
  throws), and non-32bpp bitmaps are rejected instead of being indexed with a wrong stride.
* `ToByteArray(offset, count)` now treats `offset` as a **Pixels** index as documented (it used to pass
  bytes), and all byte-array copies are bounds-checked. `Crop` clamps non-positive sizes and the `Blit`
  fast path checks source bounds before its `memcpy`.
* DPI changes are detected from the visual (`DpiDetector.SetKoefs`), so moving to another monitor or
  an RDP scaling change rebuilds the buffer and glyph cache instead of using stale scale factors.
  `FastGridControl.ClearCaches()` also clears the shared `LetterGlyphTool` font cache, which is not
  DPI-keyed.
* ClearType glyph brushes are disposed (GDI handle churn), and build settings (unsafe, `WPF` define)
  no longer depend on the `Platform` name.
* Fixed the copy shortcut: it no longer runs for every key press while Ctrl is held (operator
  precedence bug), guarded scrolling when there is no draw buffer yet, and made empty text/image
  blocks null-safe.

## Model implementation
Grid control is bind to model, which controls displayed data. Below is example of model implementation. Refer to the test WPF app for further details.

```
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodingConnected.FastWpfGrid;

namespace FastWpfGridTest
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
            btn.CommandParameter = CodingConnected.FastWpfGrid.FastGridControl.ToggleTransposedCommand;
            btn.ToolTip = "Swap rows and columns";
            return impl;
        }
    }
}
```