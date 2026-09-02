using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Maps
{
    public class MapObject
    {
        public virtual MapObjType Type { get { return MapObjType.None; } }
        public virtual ushort CickID { get; set; }
        public virtual ushort X { get; set; }
        public virtual ushort Y { get; set; }
        public bool IsVisible { get; set; }

        public virtual void Process()
        {
        }
    }
}
