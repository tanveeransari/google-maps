using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Moto
{
    internal static class RouteReader
    {
        /// <summary>
        /// String extension method to remove special characters
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveSpecialCharacters(this string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }


        /// <summary>
        /// Read GPX files from disk
        /// </summary>
        /// <param name="gpxFilePath"></param>
        /// <returns>Dictionary of route name and gpx file</returns>
        internal static Dictionary<string, gpxType> ReadGPXFiles(string gpxFilePath)
        {
            XmlSerializer ser = new XmlSerializer(typeof(gpxType));

            var gpxFiles = new Dictionary<string, gpxType>();
            foreach (string fileName in Directory.EnumerateFiles(gpxFilePath, "*.gpx"))
            {
                //Console.WriteLine(fileName);
                gpxType myGpx = ser.Deserialize(new FileStream(fileName, FileMode.Open)) as gpxType;
                string routeName = Path.GetFileNameWithoutExtension(fileName).RemoveSpecialCharacters();
                if (myGpx != null) gpxFiles.Add(routeName, myGpx);
            }

            return gpxFiles;
        }

        internal static bool WriteGPXFile(gpxType route, string fileName)
        {
            XmlSerializer ser = new XmlSerializer(typeof(gpxType));
            ser.Serialize(new FileStream(fileName, FileMode.Create), route);
            return true;
        }

        internal static List<RankedRoute> ReadRoutesCsv(string csvFilePath)
        {
            List<RankedRoute> routeList = new List<RankedRoute>();
            string[] csvRows = System.IO.File.ReadAllLines(csvFilePath);
            string[] fields = null;

            for (int i = 1; i < csvRows.Length; i++)
            {
                string csvRow = csvRows[i];
                csvRow = csvRow.Replace(".\"", "");
                csvRow = csvRow.Replace("\"", "");

                fields = csvRow.Split('|');
                RankedRoute rt = new RankedRoute(fields);
                var matchingRoute = from x in routeList
                                    where x.Name.Equals(rt.Name)
                                    select x;
                if (!matchingRoute.Any())
                {
                    routeList.Add(rt);
                }
            }

            return routeList;
        }

        internal static void DeleteRouteFiles(string gpxFilePath, List<string> fileNames)
        {
            var gpxFiles = new Dictionary<string, gpxType>();
            foreach (string fileName in Directory.EnumerateFiles(gpxFilePath, "*.gpx"))
            {
                string routeName = Path.GetFileNameWithoutExtension(fileName).RemoveSpecialCharacters();
                if (fileNames.Contains(routeName))
                {
                    File.Delete(fileName);
                }
            }
        }
    }
}
