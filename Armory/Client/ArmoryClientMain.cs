using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using armory.Client.Weapons;
using armory.Client.Pickups;

namespace armory.Client
{
	/// Client entry point. Wires client services and logs initialization.
	public class ArmoryClientMain : BaseScript
	{
		private readonly WeaponServiceClient _weaponService;
		private readonly PickupServiceClient _pickupService;

		/// FiveM entrypoint. Concrete dependencies.
		public ArmoryClientMain() : this(weaponService: null, pickupService: null)
		{
		}

		/// DI-friendly entrypoint. Any null dependency will be constructed with defaults.
		internal ArmoryClientMain(WeaponServiceClient weaponService, PickupServiceClient pickupService)
		{
			Debug.WriteLine("[Armory|Client] Loaded from 1881 Framework.");
			_weaponService = weaponService ?? new WeaponServiceClient();
			_pickupService = pickupService ?? new PickupServiceClient();
		}
	}
}