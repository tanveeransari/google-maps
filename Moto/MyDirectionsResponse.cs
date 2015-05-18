using GoogleMapsApi.Entities.Directions.Response;
using System;
using System.Linq;

namespace Moto
{
    public class MyDirectionsResponse : INode
    {
        public MyDirectionsResponse(GoogleMapsApi.Entities.Directions.Response.DirectionsResponse r)
        {
            this.Directions = r;
        }

        public NodeType NodeType
        {
            get { return NodeType.DrivingDirection; }
        }

        public DirectionsResponse Directions { get; private set; }

        public override string ToString()
        {
            if (this.Directions != null && this.Directions.Routes != null)
            {
                return this.Directions.Routes.First().Summary;
            }

            return "Mdr";
        }
    }
}
