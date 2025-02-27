using System.Collections.Generic;

namespace FastWpfGrid
{
    public class ActiveSeries
    {
        public HashSet<int> ScrollVisible = new HashSet<int>();
        public HashSet<int> Selected = new HashSet<int>();
        public HashSet<int> Frozen = new HashSet<int>();
    }
}
