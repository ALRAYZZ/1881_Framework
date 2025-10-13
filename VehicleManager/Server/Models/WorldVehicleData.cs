using System;
using System.Collections.Generic;
using System.Text;

namespace VehicleManager.Server.Models
{
	public class WorldVehicleData
	{
		public int NetId { get; set; }
		public int EntityId { get; set; }
		public uint ModelHash { get; set; }
		public string VehicleType { get; set; }
		public string Plate { get; set; }
		public float X { get; set; }
		public float Y { get; set; }
		public float Z { get; set; }
		public float Heading { get; set; }

		// Color properties
		public int PrimaryColor { get; set; }
		public int SecondaryColor { get; set; }
		public string CustomPrimaryRGB { get; set; }
		public string CustomSecondaryRGB { get; set; }

		// Vehicle State Data
		public bool EngineOn { get; set; }
	}
}
