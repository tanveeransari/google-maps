using System.Device.Location;

namespace Moto
{
    public class TwistyKey
    {
        public TwistyKey(string routeName, wptType start, wptType end)
        {
            this.RouteName = routeName;
            this.Start = new GeoCoordinate((double)start.lat, (double)start.lon);
            this.End = new GeoCoordinate((double)end.lat, (double)end.lon);
        }

        public string RouteName { get; set; }

        public GeoCoordinate Start { get; private set; }
        public GeoCoordinate End { get; private set; }

        public override string ToString()
        {
            return this.RouteName;
        }
    }
}
