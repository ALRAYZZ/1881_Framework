using System;
using System.Collections.Generic;
using System.Text;

namespace PedManager.Server.Models
{
	public class PersistentPed
	{
		public int Id { get; set; }
		public string Model { get; set; }
		public float X { get; set; }
		public float Y { get; set; }
		public float Z { get; set; }
		public float Heading { get; set; }
	}
}
