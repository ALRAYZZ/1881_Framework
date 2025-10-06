using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;

namespace armory.Client.Pickups
{
	public class PickupClient
	{
		public int Id { get; set; }
		public Vector3 Pos { get; set; }
		public string WeaponName { get; set; }
		public int Ammo { get; set; }
		public int ObjectHandle { get; set; }
	}
}
