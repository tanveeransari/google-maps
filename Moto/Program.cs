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
        public static int MAXDETOUR_CROW_PERTWISTY = 25;
        public static double MAX_EXTRA_LENGTH_MILES = 250;

        private const AvoidWay AVOID = /*AvoidWay.Tolls | */AvoidWay.Highways;

        private static string FilesDirPath;
        #region Members
        // Max distance I am willing to deviate from my route in metres
        // Start Address
        private static string origin = "916 N Taylor Ave, Oak park, IL";
        // End Address
        private static string destination = "new orleans, LA";

        //All Twisties in USA
        private static Dictionary<TwistyKey, Moto.RankedRoute> routes;

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


            var drivingDirectionRequest = new DirectionsRequest
            {
                Origin = origin,
                Destination = destination,
                Avoid = Program.AVOID
            };

            // Get Driving Directions from Google
            var googDirns = GoogleMaps.Directions.Query(drivingDirectionRequest);

            // Guard condition
            if (googDirns.Routes == null || !googDirns.Routes.Any() || googDirns.Routes.First().Legs == null
                || !googDirns.Routes.First().Legs.Any())
            {
                Console.WriteLine("Unable to find driving directions. Exiting ");
                Console.ReadKey();
                return;
            }

            //gpxType drivDirnGPX = GpxManipulator.ConvertGoogleDirectionsToGpx(drivingDirections);
            //RouteReader.WriteGPXFile(drivDirnGPX, FilesDirPath + "\\test.gpx");

            List<Moto.RankedRoute> twistyAlreadyInRoute = new List<RankedRoute>();
            List<INode> directions = new List<INode>();
            directions.Add(new MyDirectionsResponse(googDirns));
            AddTwisties(ref directions, ref twistyAlreadyInRoute);
            Console.WriteLine("-------------------------------------" + Environment.NewLine);
            directions.ForEach(d =>
            {
                if (d.NodeType == NodeType.DrivingDirection)
                {
                    PrintDirections(((MyDirectionsResponse)d).Directions);
                }
                else Console.WriteLine(" {0} ***   {1}    ***  {0} {0}", "\n", d);
            });

            string fileName = origin + destination + ".gpx";
            RouteReader.WriteGPXFile(GpxManipulator.ConvertToSingleGPX(directions), FilesDirPath + "\\" + fileName);
            Console.ReadKey();
        }

        private static void AddTwisties(ref List<INode> nodes, ref List<Moto.RankedRoute> twistysAlreadyInRoute)
        {
            if (nodes.Count != 1) throw new InvalidOperationException("AddTwisties was passed a route list with count <> 1");

            Location finalDestLoc = ((MyDirectionsResponse)nodes[0]).Directions.Routes.First().Legs.Last().EndLocation;
            GeoCoordinate finalDest = new GeoCoordinate(finalDestLoc.Latitude, finalDestLoc.Longitude);

            double milesAddedToRoute = 0;

            for (int i = 1; i <= nodes.Count; i++)
            {
                INode node = nodes[i - 1];

                if (node.NodeType != NodeType.DrivingDirection) continue;
                DirectionsResponse drivingDirections = ((MyDirectionsResponse)node).Directions;
                if (drivingDirections.Routes.Count() != 1) Debugger.Break();

                Debug.Assert(drivingDirections.Routes.Count() == 1, " Directions have zero or more than one route");
                Debug.Assert(drivingDirections.Routes.First().Legs.Count() == 1, "Route has zero or more than one leg");

                Dictionary<TwistyKey, SortedDictionary<double, Step>> twistyDistanceToLeg = new Dictionary<TwistyKey, SortedDictionary<double, Step>>();
                Leg leg = drivingDirections.Routes.First().Legs.First();

                // Find a twisty near earliest step. Then find step closest to that twisty
                if (milesAddedToRoute < MAX_EXTRA_LENGTH_MILES)
                {
                    foreach (var step in leg.Steps)
                    {
                        GeoCoordinate stepStart = new GeoCoordinate(step.StartLocation.Latitude, step.StartLocation.Longitude);
                        double distance;
                        TwistyKey routeNearStep = FindTwisty(stepStart, finalDest, twistysAlreadyInRoute,
                            MAX_EXTRA_LENGTH_MILES * 5, out distance);

                        if (routeNearStep != null)
                        {
                            // If we have already found a twisty all we are now doing is finding the nearest leg to that twisty
                            if (twistyDistanceToLeg.Keys.Count >= 1)
                            {
                                // If we are looking at the same twisty already chosen, add distance from this step
                                if (twistyDistanceToLeg.Keys.First() == routeNearStep)
                                {
                                    if (!twistyDistanceToLeg[routeNearStep].ContainsKey(distance))
                                    {
                                        twistyDistanceToLeg[routeNearStep].Add(distance, step);
                                    }
                                }
                                // Don't go any further - we don't want to add a second twisty here
                                //continue;
                            }
                            else if (!twistyDistanceToLeg.ContainsKey(routeNearStep))
                            {
                                //We are adding the first twisty found
                                twistyDistanceToLeg.Add(routeNearStep, new SortedDictionary<double, Step>());
                                twistyDistanceToLeg[routeNearStep].Add(distance, step);
                                Debug.Assert(twistyDistanceToLeg.Keys.Count == 1, " Twisty count <> 1");
                            }
                            //twistyDistanceToLeg[routeNearStep].Add(distance, step);
                        }
                    }
                }

                Debug.Assert(twistyDistanceToLeg.Keys.Count <= 1, "More than one twisty is being looked at");

                if (twistyDistanceToLeg.Any())
                {
                    var stepToDetourFrom = twistyDistanceToLeg[twistyDistanceToLeg.Keys.First()].First().Value;

                    //Is twisty to be traversed from start->end or end->start (Which endpoint nearer us)
                    GeoCoordinate startCoOrd = new GeoCoordinate(stepToDetourFrom.StartLocation.Latitude, stepToDetourFrom.StartLocation.Longitude);

                    double distanceToTwistyStart = startCoOrd.GetDistanceTo(twistyDistanceToLeg.Keys.First().Start);
                    double distanceToTwistyEnd = startCoOrd.GetDistanceTo(twistyDistanceToLeg.Keys.First().End);

                    Moto.RankedRoute twisty = routes[twistyDistanceToLeg.Keys.First()];
                    gpxType twistyRoute = twisty.Route;

                    string firstRouteStart = Utility.FormatLatLongForWayPoint(startCoOrd.Latitude, startCoOrd.Longitude);
                    string firstRouteEnd = Utility.FormatLatLongForWayPoint(
                        twistyRoute.trk[0].trkseg[0].trkpt.First().lat,
                        twistyRoute.trk[0].trkseg[0].trkpt.First().lon);

                    string secondRouteStart = Utility.FormatLatLongForWayPoint(
                        twistyRoute.trk[0].trkseg[0].trkpt.Last().lat,
                        twistyRoute.trk[0].trkseg[0].trkpt.Last().lon
                        );
                    // Add route to twisty list so its not added again
                    twistysAlreadyInRoute.Add(twisty);
                    // If we have to travel twisty from end to start, reverse the GPX route
                    if (distanceToTwistyEnd < distanceToTwistyStart)
                    {
                        string tmp = secondRouteStart;
                        secondRouteStart = firstRouteEnd;
                        firstRouteEnd = tmp;
                        twisty = GpxManipulator.ReverseRoute(twisty);
                    }

                    double oldDistance = drivingDirections.Routes.First().Legs.First().Distance.Value * GpxManipulator.METERS_TO_MILES;

                    System.Threading.Thread.Sleep(400);
                    var drivingDirectionRequest1 = new DirectionsRequest
                    {
                        Origin = drivingDirections.Routes.First().Legs.First().Steps.First().StartLocation.LocationString,
                        Destination = firstRouteEnd,
                        Avoid = Program.AVOID
                    };
                    var ddResp1 = GoogleMaps.Directions.Query(drivingDirectionRequest1);

                    System.Threading.Thread.Sleep(400);
                    var drivingDirectionRequest2 = new DirectionsRequest { Origin = secondRouteStart, Destination = destination, Avoid = Program.AVOID };
                    if (nodes.Count > i)
                    {
                        //We must route second leg to next destination on journey, not to final destination
                        INode nextNode = nodes[i];
                        drivingDirectionRequest2.Destination = GpxManipulator.GetRouteEndPoint(nextNode);
                    }

                    var ddResp2 = GoogleMaps.Directions.Query(drivingDirectionRequest2);
                    double newDistance = ddResp1.Routes.First().Legs.First().Distance.Value * GpxManipulator.METERS_TO_MILES
                        + ddResp2.Routes.First().Legs.First().Distance.Value * GpxManipulator.METERS_TO_MILES
                    + twisty.Length.Value;

                    double increaseInRouteLength = newDistance - oldDistance;
                    if (milesAddedToRoute + increaseInRouteLength < MAX_EXTRA_LENGTH_MILES)
                    {
                        milesAddedToRoute += increaseInRouteLength;
                        nodes.RemoveAt(i - 1);
                        //Add descending so order of the three is maintained
                        nodes.Insert(i - 1, new MyDirectionsResponse(ddResp2));
                        nodes.Insert(i - 1, twisty);
                        nodes.Insert(i - 1, new MyDirectionsResponse(ddResp1));

                        //nodes.Insert(i - 1, new MyDirectionsResponse(ddResp1));
                        //nodes.Insert(i, twisty);
                        //nodes.Insert(i + 1, new MyDirectionsResponse(ddResp2));
                        //i = i - 1; // Don't decrement counter to start routing again from first step - only route from last step
                        Debug.WriteLine("Added ", twisty.Name);
                        Console.WriteLine("Added {0} ", twisty.Name);
                    }
                    else
                    {
                        Console.WriteLine("Didn't add {0} as it would have added {1} to route length",
                            twisty.Name, (int)increaseInRouteLength);
                        // We didn't add this twisty because it increased the route length too much, the next twisty may not
                        i = i - 1;
                    }
                }
                else
                { /**no twisties found**/}
            }
        }

        /// <summary>
        /// Find the nearest twisty to a point, if any
        /// </summary>
        /// <param name="stepStart">Point to compare against</param>
        /// <param name="distanceToTwisty">Max distance as crow flies to start of twisty</param>
        /// <returns></returns>
        private static TwistyKey FindTwisty(GeoCoordinate stepStart, GeoCoordinate destn, List<Moto.RankedRoute> twistysToAvoid,
          double maxTwistyLength, out double distanceToTwisty)
        {
            distanceToTwisty = double.MaxValue;
            double twistyRating = 0.0;

            if (routes == null || routes.Count == 0) return null;
            TwistyKey retVal = null;

            var twistysToConsider = (from r in routes
                                     where !twistysToAvoid.Contains(r.Value)
                                     where r.Value.Length < maxTwistyLength
                                     select r).OrderByDescending(m => m.Value.Rating).ToList();

            foreach (KeyValuePair<TwistyKey, Moto.RankedRoute> kvp in twistysToConsider)
            {
                var twstKey = kvp.Key;
                double startToTwistyHead = stepStart.GetDistanceTo(twstKey.Start) * GpxManipulator.METERS_TO_MILES;
                double startToTwistyTail = stepStart.GetDistanceTo(twstKey.End) * GpxManipulator.METERS_TO_MILES;

                //double twistyHeadToDest = destn.GetDistanceTo(twstKey.Start) * GpxManipulator.METERS_TO_MILES;
                //double twistyTailToDest = destn.GetDistanceTo(twstKey.End) * GpxManipulator.METERS_TO_MILES;

                double iterDistanceToTwisty = Math.Min(startToTwistyHead, startToTwistyTail);
                if (iterDistanceToTwisty < MAXDETOUR_CROW_PERTWISTY)
                {
                    // Does end of twisty bring us closer to the destination than the start of twisty?
                    GeoCoordinate dtrStart = (iterDistanceToTwisty == startToTwistyHead ? twstKey.Start : twstKey.End);
                    GeoCoordinate dtrEnd = (iterDistanceToTwisty == startToTwistyHead ? twstKey.End : twstKey.Start);

                    double dtrStartToDest = dtrStart.GetDistanceTo(destn) * GpxManipulator.METERS_TO_MILES;
                    double dtrEndToDest = dtrEnd.GetDistanceTo(destn) * GpxManipulator.METERS_TO_MILES;

                    bool dtrTakesUsCloserToDestn = dtrEndToDest < dtrStartToDest;

                    string routeName = kvp.Value.Name;
                    if (iterDistanceToTwisty == startToTwistyTail) routeName = routeName + ":REVERSE";

                    if (dtrTakesUsCloserToDestn)
                    {
                        // If this is the first twisty or if this one is nearer than existing and has equal or better ratings , use this
                        if (retVal == null ||
                            (retVal != null && (iterDistanceToTwisty < distanceToTwisty) && kvp.Value.Rating >= twistyRating))
                        {
                            retVal = twstKey;
                            distanceToTwisty = iterDistanceToTwisty;
                            twistyRating = kvp.Value.Rating;
                            Debug.WriteLine("Considering {0} as it takes us {1} closer to destination",
                                routeName, (int)(dtrStartToDest - dtrEndToDest));
                        }
                    }
                    else
                    {

                        Debug.WriteLine("Ignoring {0} as it would take us {1} farther away from destination",
                            routeName, (int)(dtrEndToDest - dtrStartToDest));
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Populate route dictionary with all routes
        /// </summary>
        /// <returns></returns>
        private static Dictionary<TwistyKey, Moto.RankedRoute> ReadRoutes()
        {
            var retVal = new Dictionary<TwistyKey, Moto.RankedRoute>();

            // Read csv file with names, statewise rank and ratings
            List<Moto.RankedRoute> dt = RouteReader.ReadRoutesCsv(FilesDirPath + "\\routeslist.csv");

            Func<gpxType, bool> numPointsAcceptor = delegate(gpxType r)
            {
                return (r.trk.Length > 0 && r.trk[0].trkseg.Length > 0
                    && r.trk[0].trkseg[0].trkpt != null && r.trk[0].trkseg[0].trkpt.Length > 0);
            };

            Dictionary<string, gpxType> gpxFilesIntl = RouteReader.ReadGPXFiles(FilesDirPath);
            Dictionary<string, Moto.RankedRoute> gpxFiles = new Dictionary<string, Moto.RankedRoute>();

            foreach (KeyValuePair<string, gpxType> kvp in gpxFilesIntl)
            {
                if (numPointsAcceptor(kvp.Value))
                {
                    // Find mtaching ranking/name info from csv file
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
                    TwistyKey key = new TwistyKey(kvp.Key, trkPntArr.First(), trkPntArr.Last());
                    retVal.Add(key, kvp.Value);
                    //Debug.WriteLine("{0} has {1} waypoints", kvp.Key, trkPntArr.Length);
                }
            }
            return retVal;
        }

        private static void PrintDirections(DirectionsResponse directions)
        {
            Route route = directions.Routes.First();
            Leg leg = route.Legs.First();

            foreach (Step step in leg.Steps)
            {
                Console.WriteLine(StripHTML(step.HtmlInstructions) + "  : " + (step.Distance.Value * GpxManipulator.METERS_TO_MILES).ToString("N2"));
            }

            Console.WriteLine();
        }

        private static string StripHTML(string html)
        {
            return Regex.Replace(html, @"<(.|\n)*?>", string.Empty);
        }
    }
}
