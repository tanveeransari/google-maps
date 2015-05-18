//#define ASK_FOR_INPUT
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

using Directory = System.IO.Directory;

namespace Moto
{
    class Program
    {
        public const int MIN_RATING = 3;
        private const int MAX_WAYPOINTS = 8;
        public static int MAXDETOUR_CROW_PERTWISTY = 20 * 1000;

        private static string FilesDirPath;
        #region Members
        // Max distance I am willing to deviate from my route in metres
        // Start Address
        private static string origin = "916 N Taylor Ave, Oak park, IL";
        // End Address
        private static string destination = "St Louis, MO";

        //All Twisties in USA
        private static Dictionary<RouteKey, Moto.RankedRoute> routes;

        #endregion

        static void Main(string[] args)
        {
            Program.FilesDirPath = Directory.GetParent(Directory.GetParent(Environment.CurrentDirectory).ToString()).ToString();
            Program.FilesDirPath = FilesDirPath + "\\gpx";

            routes = Program.ReadRoutes();

            #region Ask user For Origin and Destination;
#if ASK_FOR_INPUT
            Console.WriteLine("Enter Starting Address");
            string ipOrigin = Console.ReadLine();
            if (ipOrigin.Trim().Length > 0) origin = ipOrigin;

            Console.WriteLine("Enter Ending Address");
            string ipDest = Console.ReadLine();
            if (ipDest.Trim().Length > 0) destination = ipDest;

#endif
            #endregion

            // Get directions
            // Find first twisty near route
            // Make 3 step direction  - dirn to twisty start, twisty and direction from twisty end to destination
            // Repeat with each direction ignoring twisty already being driven

            var drivingDirectionRequest = new DirectionsRequest
            {
                Origin = origin,
                Destination = destination,
                Avoid = AvoidWay.Highways
            };

            // Get Driving Directions from Google
            DirectionsResponse drivingDirections = GoogleMaps.Directions.Query(drivingDirectionRequest);

            // Guard condition
            if (drivingDirections.Routes == null || !drivingDirections.Routes.Any() || drivingDirections.Routes.First().Legs == null
                || !drivingDirections.Routes.First().Legs.Any())
            {
                Console.WriteLine("Unable to find driving directions. Exiting ");
                Console.ReadKey();
                return;
            }

            //gpxType drivDirnGPX = GpxManipulator.ConvertGoogleDirectionsToGpx(drivingDirections);
            //RouteReader.WriteGPXFile(drivDirnGPX, FilesDirPath + "\\test.gpx");

            List<Moto.RankedRoute> twistyAlreadyInRoute = new List<RankedRoute>();
            SplitDirections split = null;

            if (AddTwisty(drivingDirections, twistyAlreadyInRoute, out split))
            {
                twistyAlreadyInRoute.Add(split.Twisty);
                SplitDirections split1 = null;
                if (AddTwisty(split.FirstRoute, twistyAlreadyInRoute, out split1))
                {
                    twistyAlreadyInRoute.Add(split1.Twisty);
                }

                SplitDirections split2 = null;
                if (AddTwisty(split.LastRoute, twistyAlreadyInRoute, out split2))
                {
                    twistyAlreadyInRoute.Add(split2.Twisty);
                }
            }


            #if IterateLegsFindTwisties

            // Twisties mapped to Direction Legs, sorted by distance
            Dictionary<RouteKey, SortedDictionary<double, Step>> twistyDistanceToLeg = new Dictionary<RouteKey, SortedDictionary<double, Step>>();
            Debug.Assert(drivingDirections.Routes.First().Legs.Count() <= 1, "Found directions with more than one leg");

            // Iterate over all legs and find the earliest leg and the nearest twisty to that leg
            foreach (var leg in drivingDirections.Routes.First().Legs)
            {
                foreach (var step in leg.Steps)
                {
                    GeoCoordinate startCoOrd = new GeoCoordinate(step.StartLocation.Latitude, step.StartLocation.Longitude);
                    double distance; ;
                    RouteKey routeNearStep = FindNearestRouteWithinDistance(startCoOrd, twistyAlreadyInRoute, out distance);
                    if (routeNearStep != null)
                    {
                        // If we have already found a twisty all we are now doing is finding the nearest leg to that twisty
                        if (twistyDistanceToLeg.Keys.Count >= 1)
                        {
                            if (twistyDistanceToLeg.Keys.First() == routeNearStep)
                            {
                                twistyDistanceToLeg[routeNearStep].Add(distance, step);
                            }
                            Debug.WriteLine("Ignoring twisty {0} as we already have{1}", routeNearStep.RouteName, twistyDistanceToLeg.First().Key.RouteName);
                            continue;
                        }

                        if (!twistyDistanceToLeg.ContainsKey(routeNearStep))
                        {
                            twistyDistanceToLeg.Add(routeNearStep, new SortedDictionary<double, Step>());
                        }

                        twistyDistanceToLeg[routeNearStep].Add(distance, step);

                        Debug.WriteLine(" *****  ****  Found twisty on our route");
                        Debug.WriteLine("{0} is {1} km from {2}", routeNearStep.RouteName,
                        (distance / 1000).ToString("N2"),
                        StripHTML(step.HtmlInstructions));

                        if (twistyDistanceToLeg.Keys.Count > 1)
                        {
                            throw new InvalidOperationException("We will only consider one twisty at a time");
                        }
                    }
                }
            }

            // We are limited to using only 8 waypoints max
            Debug.Assert(twistyDistanceToLeg.Keys.Count <= 1, "More than one twisty is being looked at");
            if (twistyDistanceToLeg.Any())
            {
                var legToDetourFrom = twistyDistanceToLeg[twistyDistanceToLeg.Keys.First()].First().Value;

                //Is twisty to be traversed from start->end or end->start (Which endpoint nearer us)
                GeoCoordinate startCoOrd = new GeoCoordinate(legToDetourFrom.StartLocation.Latitude, legToDetourFrom.StartLocation.Longitude);

                double distanceToTwistyStart = startCoOrd.GetDistanceTo(twistyDistanceToLeg.Keys.First().Start);
                double distanceToTwistyEnd = startCoOrd.GetDistanceTo(twistyDistanceToLeg.Keys.First().End);

                Moto.RankedRoute twisty = routes[twistyDistanceToLeg.Keys.First()];
                gpxType twistyRoute = twisty.Route;

                string wayPointLegStart = Utility.FormatLatLongForWayPoint(startCoOrd.Latitude, startCoOrd.Longitude);
                string firstRouteEnd = Utility.FormatLatLongForWayPoint(
                    twistyRoute.trk[0].trkseg[0].trkpt[0].lat,
                    twistyRoute.trk[0].trkseg[0].trkpt[0].lon);

                string secondRouteStart = Utility.FormatLatLongForWayPoint(
                    twistyRoute.trk[0].trkseg[0].trkpt.Last().lat,
                    twistyRoute.trk[0].trkseg[0].trkpt.Last().lon
                    );

                if (distanceToTwistyEnd < distanceToTwistyStart)
                {
                    string tmp = secondRouteStart;
                    secondRouteStart = firstRouteEnd;
                    firstRouteEnd = tmp;
                    // gpxType rvrsRoute = GpxManipulator.ReverseRoute(twistyRoute);
                }

                drivingDirectionRequest = new DirectionsRequest
                {
                    Origin = origin,
                    Destination = firstRouteEnd,
                    Avoid = AvoidWay.Highways
                };

                //Don't add a waypoint to step start - head straight to the twisty
                //(drivingDirectionRequest.Waypoints = new string[1])[0] = wayPointLegStart;

                var drivingDirectionRequest2 = new DirectionsRequest
                {
                    Origin = secondRouteStart,
                    Destination = destination,
                    Avoid = AvoidWay.Highways
                };
                // Get Driving Directions from Google
                drivingDirections = GoogleMaps.Directions.Query(drivingDirectionRequest);
                DirectionsResponse drivingDirections2 = GoogleMaps.Directions.Query(drivingDirectionRequest);
                PrintDirections(drivingDirections);
                PrintDirections(drivingDirections2);
            }
            else
            {
                PrintDirections(drivingDirections);
            }

            #endif

            Console.ReadKey();
        }

        private static bool AddTwisty(DirectionsResponse drivingDirections, List<Moto.RankedRoute> twistysAlreadyInRoute, out SplitDirections split)
        {
            split = new SplitDirections();
            Debug.Assert(drivingDirections.Routes.Count() == 1, " Directions have zero or more than one route");
            Debug.Assert(drivingDirections.Routes.First().Legs.Count() == 1, "Route has zero or more than one leg");

            Dictionary<RouteKey, SortedDictionary<double, Step>> twistyDistanceToLeg = new Dictionary<RouteKey, SortedDictionary<double, Step>>();
            Leg leg = drivingDirections.Routes.First().Legs.First();

            foreach (var step in leg.Steps)
            {
                GeoCoordinate startCoOrd = new GeoCoordinate(step.StartLocation.Latitude, step.StartLocation.Longitude);
                double distance;
                RouteKey routeNearStep = FindNearestRouteWithinDistance(startCoOrd, twistysAlreadyInRoute, out distance);
                if (routeNearStep != null)
                {
                    // If we have already found a twisty all we are now doing is finding the nearest leg to that twisty
                    if (twistyDistanceToLeg.Keys.Count >= 1)
                    {
                        if (twistyDistanceToLeg.Keys.First() == routeNearStep)
                        {
                            twistyDistanceToLeg[routeNearStep].Add(distance, step);
                        }
                        Console.WriteLine("Ignoring twisty {0} as we already have{1}", routeNearStep.RouteName, twistyDistanceToLeg.First().Key.RouteName);
                        continue;
                    }

                    if (!twistyDistanceToLeg.ContainsKey(routeNearStep))
                    {
                        twistyDistanceToLeg.Add(routeNearStep, new SortedDictionary<double, Step>());
                    }

                    twistyDistanceToLeg[routeNearStep].Add(distance, step);

                    Console.WriteLine(" *****  ****  Found twisty on our route");
                    Console.WriteLine("{0} is {1} km from {2}", routeNearStep.RouteName,
                    (distance / 1000).ToString("N2"),
                    StripHTML(step.HtmlInstructions));

                    if (twistyDistanceToLeg.Keys.Count > 1)
                    {
                        throw new InvalidOperationException("We will only consider one twisty at a time");
                    }
                }
            }

            Debug.Assert(twistyDistanceToLeg.Keys.Count <= 1, "More than one twisty is being looked at");


            if (twistyDistanceToLeg.Any())
            {
                var legToDetourFrom = twistyDistanceToLeg[twistyDistanceToLeg.Keys.First()].First().Value;

                //Is twisty to be traversed from start->end or end->start (Which endpoint nearer us)
                GeoCoordinate startCoOrd = new GeoCoordinate(legToDetourFrom.StartLocation.Latitude, legToDetourFrom.StartLocation.Longitude);

                double distanceToTwistyStart = startCoOrd.GetDistanceTo(twistyDistanceToLeg.Keys.First().Start);
                double distanceToTwistyEnd = startCoOrd.GetDistanceTo(twistyDistanceToLeg.Keys.First().End);

                Moto.RankedRoute twisty = routes[twistyDistanceToLeg.Keys.First()];
                gpxType twistyRoute = twisty.Route;

                string wayPointLegStart = Utility.FormatLatLongForWayPoint(startCoOrd.Latitude, startCoOrd.Longitude);
                string firstRouteEnd = Utility.FormatLatLongForWayPoint(
                    twistyRoute.trk[0].trkseg[0].trkpt[0].lat,
                    twistyRoute.trk[0].trkseg[0].trkpt[0].lon);

                string secondRouteStart = Utility.FormatLatLongForWayPoint(
                    twistyRoute.trk[0].trkseg[0].trkpt.Last().lat,
                    twistyRoute.trk[0].trkseg[0].trkpt.Last().lon
                    );

                if (distanceToTwistyEnd < distanceToTwistyStart)
                {
                    string tmp = secondRouteStart;
                    secondRouteStart = firstRouteEnd;
                    firstRouteEnd = tmp;
                    split.TwistyInReverse = true;
                    //gpxType rvrsRoute = GpxManipulator.ReverseRoute(twistyRoute);
                    twisty = GpxManipulator.ReverseRoute(twisty);
                }

                var drivingDirectionRequest = new DirectionsRequest
                {
                    Origin = origin,
                    Destination = firstRouteEnd,
                    Avoid = AvoidWay.Highways
                };

                //Don't add a waypoint to step start - head straight to the twisty
                //(drivingDirectionRequest.Waypoints = new string[1])[0] = wayPointLegStart;

                var drivingDirectionRequest2 = new DirectionsRequest
                {
                    Origin = secondRouteStart,
                    Destination = destination,
                    Avoid = AvoidWay.Highways
                };

                // Get Driving Directions from Google
                split.FirstRoute = GoogleMaps.Directions.Query(drivingDirectionRequest);
                split.LastRoute = GoogleMaps.Directions.Query(drivingDirectionRequest);
                split.Twisty = twisty;
                return true;
            }
            else
            {
                return false;
            }
        }

        private class SplitDirections
        {
            public DirectionsResponse FirstRoute;
            public Moto.RankedRoute Twisty;
            public bool TwistyInReverse;
            public DirectionsResponse LastRoute;
        }

        /// <summary>
        /// Find the nearest twisty to a point, if any
        /// </summary>
        /// <param name="startCoOrd">Point to compare against</param>
        /// <param name="distance">Max distance as crow flies to start of twisty</param>
        /// <returns></returns>
        private static RouteKey FindNearestRouteWithinDistance(GeoCoordinate startCoOrd, List<Moto.RankedRoute> twistysToAvoid,
            out double distance)
        {
            double distanceStart, distanceEnd;
            distance = distanceStart = distanceEnd = 0;

            if (routes == null || routes.Count == 0) return null;
            RouteKey retVal = null;

            var twistysToConsider = (from r in routes where !twistysToAvoid.Contains(r.Value) select r).ToList();
            foreach (KeyValuePair<RouteKey, Moto.RankedRoute> twisty in twistysToConsider)
            {
                var key = twisty.Key;
                double distanceToTwistyStart = startCoOrd.GetDistanceTo(key.Start);
                double distanceToTwistyEnd = startCoOrd.GetDistanceTo(key.End);

                if (Math.Min(distanceToTwistyStart, distanceToTwistyEnd) < MAXDETOUR_CROW_PERTWISTY)
                {
                    if (retVal == null ||
                        // If this twisty is nearer than the one we found before, replace the old one with this
                        (retVal != null && (Math.Min(distanceToTwistyStart, distanceToTwistyEnd) < distance)))
                    {
                        retVal = key;
                        distance = Math.Min(distanceToTwistyStart, distanceToTwistyEnd);
                        distanceStart = distanceToTwistyStart;
                        distanceEnd = distanceToTwistyEnd;
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Populate route dictionary with all routes
        /// </summary>
        /// <returns></returns>
        private static Dictionary<RouteKey, Moto.RankedRoute> ReadRoutes()
        {
            var retVal = new Dictionary<RouteKey, Moto.RankedRoute>();

            List<Moto.RankedRoute> dt = RouteReader.ReadRoutesCsv(FilesDirPath + "\\routeslist.csv");

            #region Create Filters
            Func<gpxType, bool> numPointsAcceptor = delegate(gpxType map)
    {
        return (map.trk.Length > 0 && map.trk[0].trkseg.Any()
            && map.trk[0].trkseg[0].trkpt != null
            && map.trk[0].trkseg[0].trkpt.Length > 0);
    };

            Func<gpxType, string, bool> ratingsAcceptor = delegate(gpxType map, string mapName)
            {
                var matched = from x in dt where x.Name.Equals(mapName, StringComparison.InvariantCultureIgnoreCase) select x;
                if (matched.Any())
                {
                    if (matched.First().Rating > MIN_RATING) return true;
                }

                return false;
            };
            #endregion

            Dictionary<string, gpxType> gpxFilesIntl = RouteReader.ReadGPXFiles(FilesDirPath);
            Dictionary<string, Moto.RankedRoute> gpxFiles = new Dictionary<string, Moto.RankedRoute>();
            foreach (KeyValuePair<string, gpxType> kvp in gpxFilesIntl)
            {
                if (numPointsAcceptor(kvp.Value))
                {
                    var matched = from x in dt where x.Name.Equals(kvp.Key, StringComparison.InvariantCultureIgnoreCase) select x;
                    if (matched.Any())
                    {
                        if (matched.First().Rating > MIN_RATING)
                        {
                            var rt = matched.First();
                            rt.Route = kvp.Value;
                            rt.Length = GpxManipulator.CalculateRouteLength(rt.Route);
                            gpxFiles.Add(kvp.Key, rt);
                        }
                    }
                }
            }

            if (gpxFiles.Count > 0)
            {
                foreach (KeyValuePair<string, Moto.RankedRoute> kvp in gpxFiles)
                {
                    var gpx1 = kvp.Value.Route;
                    var trkPntArr = gpx1.trk[0].trkseg[0].trkpt;
                    RouteKey key = new RouteKey(kvp.Key, trkPntArr.First(), trkPntArr.Last());
                    retVal.Add(key, kvp.Value);
                    //Debug.WriteLine("{0} has {1} waypoints", kvp.Key, trkPntArr.Length);
                }
            }
            return retVal;
        }

        private static void ShowGetDistance()
        {
            //GeoCoordinate firstPoint = new GeoCoordinate(latitude: (double)wptArr.First().lat, longitude: (double)wptArr.First().lon);
            //GeoCoordinate lastPoint = new GeoCoordinate(latitude: (double)wptArr.Last().lat, longitude: (double)wptArr.Last().lon);
            //double distInMetrese = firstPoint.GetDistanceTo(lastPoint);
            //Console.WriteLine("{0} Distance Start:End ~ {1} km", kvp.Key, (distInMetrese / 1000).ToString("N2"));
        }

        private static void PrintDirections(DirectionsResponse directions)
        {
            Route route = directions.Routes.First();
            Leg leg = route.Legs.First();

            foreach (Step step in leg.Steps)
            {
                Console.WriteLine(StripHTML(step.HtmlInstructions));
            }

            Console.WriteLine();
        }

        private static string StripHTML(string html)
        {
            return Regex.Replace(html, @"<(.|\n)*?>", string.Empty);
        }

        //private static Dictionary<RouteKey, double> FindRoutesWithinDistance(GeoCoordinate startCoOrd)
        //{
        //    if (routes == null || routes.Count == 0) return null;

        //    Dictionary<RouteKey, double> retVal = new Dictionary<RouteKey, double>();
        //    foreach (KeyValuePair<RouteKey, gpxType> twisty in routes)
        //    {
        //        var key = twisty.Key;
        //        double distanceToTwistyStart = startCoOrd.GetDistanceTo(twisty.Key.Start);
        //        double distanceToTwistyEnd = startCoOrd.GetDistanceTo(twisty.Key.End);

        //        if (Math.Min(distanceToTwistyStart, distanceToTwistyEnd) < MaxDetourCrowPerTwisty)
        //        {
        //            retVal.Add(twisty.Key, Math.Min(distanceToTwistyStart, distanceToTwistyEnd));
        //        }
        //    }

        //    if (retVal.Count > 0)
        //    {
        //        retVal.OrderBy(x => x.Value).ToList();
        //        return retVal;
        //    }
        //    else return null;
        //}
    }
}
