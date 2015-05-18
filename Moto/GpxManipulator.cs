using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Text.RegularExpressions;
using System.Diagnostics;

using GoogleMapsApi;
using GoogleMapsApi.Entities.Common;
using GoogleMapsApi.Entities.Directions.Request;
using GoogleMapsApi.Entities.Directions.Response;
using GoogleMapsApi.Entities.Elevation.Request;
using GoogleMapsApi.Entities.Geocoding.Request;
using GoogleMapsApi.Entities.Geocoding.Response;
using GoogleMapsApi.StaticMaps;
using GoogleMapsApi.StaticMaps.Entities;


namespace Moto
{
    internal static class GpxManipulator
    {
        public const double METERS_TO_MILES = 0.000621371;
        static internal gpxType ConvertGoogleDirectionsToGpx(DirectionsResponse directions)
        {
            if (!directions.Routes.Any() || !directions.Routes.First().Legs.Any()) return null;

            Debug.Assert(directions.Routes.Count() <= 1, "Driving directions have more than one route");
            Debug.Assert(directions.Routes.First().Legs.Count() <= 1, "Driving directions have more than one leg");

            gpxType rt = new gpxType()
            {
                creator = "Tanveer Ansari",
                trk = new trkType[1]
            };
            ((rt.trk[0] = new trkType()).trkseg = new trksegType[1])[0] = new trksegType();

            List<wptType> wayPoints = new List<wptType>();
            foreach (var step in directions.Routes.First().Legs.First().Steps)
            {
                wptType wptStart = new wptType()
                {
                    lat = (decimal)step.StartLocation.Latitude,
                    lon = (decimal)step.StartLocation.Longitude
                };
                wptType wptEnd = new wptType()
                {
                    lat = (decimal)step.EndLocation.Latitude,
                    lon = (decimal)step.EndLocation.Longitude
                };
                wayPoints.Add(wptStart);
                wayPoints.Add(wptEnd);
                //step.StartLocation.Latitude, step.StartLocation.Longitude
            }

            rt.trk[0].trkseg[0].trkpt = wayPoints.ToArray();
            return rt;
        }

        static internal Moto.RankedRoute ReverseRoute(Moto.RankedRoute route)
        {
            return new RankedRoute()
            {
                Length = route.Length,
                Name = route.Name + ":REVERSE",
                Rank = route.Rank,
                Rating = route.Rating,
                Route = ReverseRoute(route.Route),
                States = route.States
            };
        }

        static private gpxType ReverseRoute(gpxType gpx)
        {
            wptType[] wayPoints = gpx.trk[0].trkseg[0].trkpt;
            wptType[] reversedWpt = new wptType[wayPoints.Length];
            Array.Copy(wayPoints, reversedWpt, wayPoints.Length);
            Array.Reverse(reversedWpt, 0, wayPoints.Length);

            gpxType rt = new gpxType()
            {
                creator = gpx.creator,
                extensions = gpx.extensions,

                metadata = gpx.metadata,
                version = gpx.version,
                trk = new trkType[1]
            };

            (rt.trk[0] = new trkType()).trkseg = new trksegType[1];
            (rt.trk[0].trkseg[0] = new trksegType()).trkpt = reversedWpt;

            return rt;

            //wptType[] reversed = new wptType[wayPoints.Length];
            //for (int i = 0; i < wayPoints.Length; i++)
            //{reversed[wayPoints.Length - i - 1] = wayPoints[i];}
        }

        static internal double CalculateRouteLength(gpxType map)
        {
            double totDistInMetres = 0;

            if (map.trk.Length > 0 && map.trk[0].trkseg.Any()
            && map.trk[0].trkseg[0].trkpt != null
            && map.trk[0].trkseg[0].trkpt.Length > 0)
            {
                wptType[] wayPoints = map.trk[0].trkseg[0].trkpt;
                for (int i = 0; i < wayPoints.Length - 1; i++)
                {
                    GeoCoordinate firstPoint = new GeoCoordinate((double)wayPoints[i].lat, (double)wayPoints[i].lon);
                    GeoCoordinate secondPoint = new GeoCoordinate((double)wayPoints[i + 1].lat, (double)wayPoints[i + 1].lon);
                    double distInMetres = firstPoint.GetDistanceTo(secondPoint);
                    totDistInMetres += distInMetres;
                }
            }
            double distInMiles = METERS_TO_MILES * totDistInMetres;
            return distInMiles;
        }

        internal static gpxType ConvertToSingleGPX(List<INode> directions)
        {
            List<wptType> totalRoute = new List<wptType>();

            gpxType rt = new gpxType()
            {
                creator = "Tanveer Ansari",
                trk = new trkType[1]
            };
            ((rt.trk[0] = new trkType()).trkseg = new trksegType[1])[0] = new trksegType();

            foreach (INode node in directions)
            {
                gpxType oneRoute = null;
                switch (node.NodeType)
                {
                    case NodeType.GPX:
                        oneRoute = ((Moto.RankedRoute)node).Route;
                        break;
                    case NodeType.DrivingDirection:
                        oneRoute = ConvertGoogleDirectionsToGpx(((MyDirectionsResponse)node).Directions);
                        break;
                    default:
                        break;
                }

                wptType[] wayPoints = oneRoute.trk[0].trkseg[0].trkpt;
                totalRoute.AddRange(wayPoints);
            }

            rt.trk[0].trkseg[0].trkpt = totalRoute.ToArray();
            return rt;
        }

        internal static string GetStartPointFromNode(INode nextNode)
        {
            if (nextNode.NodeType == NodeType.DrivingDirection)
            {
                DirectionsResponse directions = ((MyDirectionsResponse)nextNode).Directions;
                return Utility.FormatLatLongForWayPoint(
                directions.Routes.First().Legs.First().Steps.First().StartLocation.Latitude,
                directions.Routes.First().Legs.First().Steps.First().StartLocation.Longitude);
            }
            else if (nextNode.NodeType == NodeType.GPX)
            {
                wptType wpt = ((Moto.RankedRoute)nextNode).Route.trk[0].trkseg[0].trkpt[0];
                return Utility.FormatLatLongForWayPoint(wpt.lat, wpt.lon);
            }
            else
            {
                throw new InvalidOperationException("Wrong node type passed");
            }
        }
    }
}
