using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moto
{
    public class RankedRoute
    {
        public RankedRoute()
        {

        }

        public RankedRoute(string[] fields)
        {
            this.Rank = (int)double.Parse(fields[0]);

            this.Name = fields[1];
            this.Name = this.Name.RemoveSpecialCharacters();
            if (fields[2].Length > 0)
            {
                this.States = fields[2].Split(',').ToList();
            }
            this.Rating = double.Parse(fields[3]);

        }

        public string Name { get; set; }

        public int Rank { get; set; }

        public double Rating { get; set; }

        public List<String> States { get; set; }

        public gpxType Route { get; set; }

        public double? Length { get; set; }

        public override string ToString()
        {
            return string.Format("{0} : Rating {1}", Name, Rating.ToString("N0"));
        }
    }
}
