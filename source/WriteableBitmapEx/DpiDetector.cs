using System;
using System.Drawing;

namespace System.Windows.Media.Imaging
{
    public static class DpiDetector
    {
        private static readonly object SyncRoot = new object();
        private static double? _dpiXKoef;
        private static double? _dpiYKoef;

        public static double DpiXKoef
        {
            get
            {
                lock (SyncRoot)
                {
                    return GetKoef(ref _dpiXKoef, false);
                }
            }
        }

        public static double DpiYKoef
        {
            get
            {
                lock (SyncRoot)
                {
                    return GetKoef(ref _dpiYKoef, true);
                }
            }
        }

        private static double GetKoef(ref double? koef, bool vertical)
        {
            if (koef == null)
            {
                using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    koef = (vertical ? graphics.DpiY : graphics.DpiX) / 96.0;
                }
            }

            return koef.Value > 0 ? koef.Value : 1.0;
        }

        /// <summary>
        /// Updates the cached scale factors, for example when the control moves to a monitor with a
        /// different DPI or an RDP session changes its scaling. Returns true when they changed.
        /// </summary>
        public static bool SetKoefs(double dpiXKoef, double dpiYKoef)
        {
            if (!(dpiXKoef > 0)) dpiXKoef = 1.0;
            if (!(dpiYKoef > 0)) dpiYKoef = 1.0;

            lock (SyncRoot)
            {
                if (_dpiXKoef == dpiXKoef && _dpiYKoef == dpiYKoef) return false;
                _dpiXKoef = dpiXKoef;
                _dpiYKoef = dpiYKoef;
                return true;
            }
        }

        /// <summary>Forgets the cached values; the next access probes the display again.</summary>
        public static void Reset()
        {
            lock (SyncRoot)
            {
                _dpiXKoef = null;
                _dpiYKoef = null;
            }
        }
    }
}
