using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moto
{
    class Utility
    {
        public static string FormatLatLongForWayPoint(double latitude, double longitude)
        {
            return string.Format("({0},{1})", latitude, longitude);
        }

        public static string FormatLatLongForWayPoint(decimal latitude, decimal longitude)
        {
            return string.Format("({0},{1})", latitude, longitude);
        }
    }
}
