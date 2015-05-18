using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moto
{
    public class RouteKey
    {
        public RouteKey(string routeName, wptType start, wptType end)
        {
            this.RouteName = routeName;
            this.Start = new GeoCoordinate((double)start.lat, (double)start.lon);
            this.End = new GeoCoordinate((double)end.lat, (double)end.lon);
        }


        public GeoCoordinate Start { get; private set; }
        public GeoCoordinate End { get; private set; }
        public string RouteName { get; set; }

        //public double LatStart { private get; private set; }
        //public double LongStart { private get; private set; }
        //public double LatEnd { private get; private set; }
        //public double LongEnd { private get; private set; }
    }

    public class MyRoute
    {
        public RouteKey Key { get; private set; }
        public gpxType GPX { get; private set; }
        // Where do I get this
        public double LengthInMetres { get; private set; }
    }

    // Don't need this  - use GeoCoordinate
    //public class LatLong
    //{
    //    public LatLong(double latitude, double longitude)
    //    {
    //        this.Latitude = latitude;
    //        this.Longitude = longitude;
    //    }

    //    public double Latitude { get; private set; }

    //    public double Longitude { get; private set; }
    //}
}
