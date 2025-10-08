using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

namespace armory.Client.Weapons
{
	/// Client-side event handlers that apply weapon changes received from the server.
	public class WeaponServiceClient : BaseScript 
	{
		/// Registers client event handlers for giving and removing weapons.
		public WeaponServiceClient()
		{
			EventHandlers["armory:ReceiveWeapon"] += new Action<string, dynamic, int>(OnReceiveWeapon);
			EventHandlers["armory:RemoveWeapon"] += new Action<string>(OnRemoveWeapon);
			EventHandlers["armory:RemoveAllWeapons"] += new Action(OnRemoveAllWeapons);
		}

		/// Spawns the specified weapon on the local player with optional components and tint.
		private void OnReceiveWeapon(string name, dynamic componentsObj, int tintIndex)
		{
			Debug.WriteLine($"[Armory|Client] OnReceiveWeapon called with: '{name}', tintIndex: {tintIndex}");
			var ped = PlayerPedId();
			uint hash = (uint)GetHashKey(name);

			if (!IsWeaponValid(hash))
			{
				Debug.WriteLine($"[Armory|Client] WARNING: Weapon hash {hash} is NOT valid!");
				return;
			}

			// Give the weapon
			GiveWeaponToPed(ped, hash, 250, false, true);

			// Apply components if provided
			if (componentsObj != null)
			{
				try
				{
					// Handle both List<string> and array types
					var componentsList = new List<string>();

					if (componentsObj is IEnumerable<object> enumerable)
					{
						foreach (var item in enumerable)
						{
							if (item != null)
								componentsList.Add(item.ToString());
						}
					}

					foreach (var component in componentsList)
					{
						if (string.IsNullOrWhiteSpace(component)) continue;

						var componentHash = (uint)GetHashKey(component);
						if (DoesWeaponTakeWeaponComponent(hash, componentHash))
						{
							GiveWeaponComponentToPed(ped, hash, componentHash);
							Debug.WriteLine($"[Armory|Client] Applied component: {component}");
						}
						else
						{
							Debug.WriteLine($"[Armory|Client] WARNING: Component {component} is not compatible with {name}");
						}
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[Armory|Client] Error applying components: {ex.Message}");
				}
			}

			// Apply tint if provided (valid range is typically 0-7)
			if (tintIndex >= 0)
			{
				var maxTints = GetWeaponTintCount(hash);
				if (tintIndex < maxTints)
				{
					SetPedWeaponTintIndex(ped, hash, tintIndex);
					Debug.WriteLine($"[Armory|Client] Applied tint index: {tintIndex}");
				}
				else
				{
					Debug.WriteLine($"[Armory|Client] WARNING: Tint index {tintIndex} is invalid for {name} (max: {maxTints - 1})");
				}
			}

			SetCurrentPedWeapon(ped, hash, true);
			Debug.WriteLine($"[Armory|Client] Successfully gave weapon {name} to player");
		}

		/// Removes the specified weapon from the local player.
		private void OnRemoveWeapon(string name)
		{
			var ped = PlayerPedId();
			uint hash = (uint)GetHashKey(name);
			if (!IsWeaponValid(hash)) return;

			uint current = (uint)GetSelectedPedWeapon(ped);
			RemoveWeaponFromPed(ped, hash);
			if (current == hash)
				SetCurrentPedWeapon(ped, (uint)GetHashKey("WEAPON_UNARMED"), true);
		}

		/// Removes all weapons from the local player and switches to unarmed.
		private void OnRemoveAllWeapons()
		{
			var ped = PlayerPedId();
			RemoveAllPedWeapons(ped, true);
			SetCurrentPedWeapon(ped, (uint)GetHashKey("WEAPON_UNARMED"), true);
		}
	}
}
