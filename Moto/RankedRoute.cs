using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moto
{
    public class RankedRoute : INode
    {
        #region ctor
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
        #endregion

        public string Name { get; set; }

        public int Rank { get; set; }

        public double Rating { get; set; }

        public List<String> States { get; set; }

        public gpxType Route { get; set; }

        public double? Length { get; set; }

        public override string ToString()
        {
            if (Length.HasValue)
            {
                return string.Format("{0} : Rating {1} Length: {2} miles", Name, Rating.ToString("N1"), Length.Value.ToString("N0"));
            }
            return string.Format("{0} : Rating {1}", Name, Rating.ToString("N0"));
        }

        public NodeType GetNodeType()
        {
            return NodeType.GPX;
        }

        public NodeType NodeType
        {
            get { return NodeType.GPX; }
        }
    }
}
