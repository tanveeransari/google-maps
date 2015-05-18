using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Moto
{
    public enum NodeType
    {
        Unknown=0,
        GPX =1,
        DrivingDirection =2
    }

    interface INode
    {
        NodeType NodeType { get; }
    }
}
