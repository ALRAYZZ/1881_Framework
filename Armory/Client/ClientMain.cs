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
	/// <summary>
	/// Client entry point. Wires client services and logs initialization.
	/// </summary>
	public class ClientMain : BaseScript
	{
		private readonly WeaponServiceClient _weaponService;
		private readonly PickupServiceClient _pickupService;

		/// <summary>
		/// FiveM entrypoint. Composes concrete dependencies.
		/// </summary>
		public ClientMain() : this(
			weaponService: null,
			pickupService: null)
		{
		}

		/// <summary>
		/// DI-friendly entrypoint. Any null dependency will be constructed with defaults.
		/// </summary>
		/// <param name="weaponService">Optional prebuilt weapon client service.</param>
		/// <param name="pickupService">Optional prebuilt pickup client service.</param>
		internal ClientMain(WeaponServiceClient weaponService, PickupServiceClient pickupService)
		{
			Debug.WriteLine("[Armory|Client] Loaded from 1881 Framework.");
			_weaponService = weaponService ?? new WeaponServiceClient();
			_pickupService = pickupService ?? new PickupServiceClient();
		}
	}
}