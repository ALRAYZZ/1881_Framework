using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace armory.Server
{
	/// <summary>
	/// Abstraction for server-side weapon operations triggered by commands or pickups.
	/// </summary>
	public interface IWeaponService
	{
		/// <summary>
		/// Gives a weapon to the specified player with optional components and tint.
		/// </summary>
		/// <param name="player">Target player.</param>
		/// <param name="weapon">Normalized weapon name (e.g., WEAPON_PISTOL).</param>
		/// <param name="components">Optional list of component names.</param>
		/// <param name="tintIndex">Tint index (or -1 to skip).</param>
		void GiveWeapon(Player player, string weapon, List<string> components = null, int tintIndex = -1);

		/// <summary>
		/// Removes a specific weapon from the specified player.
		/// </summary>
		/// <param name="player">Target player.</param>
		/// <param name="weapon">Normalized weapon name.</param>
		void RemoveWeapon(Player player, string weapon);

		/// <summary>
		/// Removes all weapons from the specified player.
		/// </summary>
		/// <param name="player">Target player.</param>
		void RemoveAllWeapons(Player player);
	}

	/// <summary>
	/// Default server-side implementation for giving/removing weapons.
	/// </summary>
	public class WeaponService : IWeaponService
	{
		private readonly PlayerWeaponTracker _weaponTracker;

		/// <summary>
		/// Creates a weapon service using the provided player weapon tracker.
		/// </summary>
		/// <param name="weaponTracker">Tracker used to maintain server-side weapon state.</param>
		public WeaponService(PlayerWeaponTracker weaponTracker)
		{
			_weaponTracker = weaponTracker ?? throw new ArgumentNullException(nameof(weaponTracker));
		}

		/// <inheritdoc />
		public void GiveWeapon(Player player, string weapon, List<string> components = null, int tintIndex = -1)
		{
			if (!IsPlayerConnected(player, out var reason))
			{
				Debug.WriteLine($"[Armory|Server] GiveWeapon failed: {reason}");
				return;
			}

			Debug.WriteLine($"[Armory|Server] GiveWeapon called with weapon: '{weapon}', components: {components?.Count ?? 0}, tintIndex: {tintIndex}");
			BaseScript.TriggerClientEvent(player, "armory:ReceiveWeapon", weapon, components ?? new List<string>(), tintIndex);
			_weaponTracker.AddWeapon(player, weapon);
			Debug.WriteLine($"[Armory|Server] Gave {weapon} to {player.Name}");
		}

		/// <inheritdoc />
		public void RemoveWeapon(Player player, string weapon)
		{
			if (!IsPlayerConnected(player, out var reason))
			{
				Debug.WriteLine($"[Armory|Server] RemoveWeapon failed: {reason}");
				return;
			}

			BaseScript.TriggerClientEvent(player, "armory:RemoveWeapon", weapon);
			_weaponTracker.RemoveWeapon(player, weapon);
			Debug.WriteLine($"[Armory|Server] Removed {weapon} from {player.Name}");
		}

		/// <inheritdoc />
		public void RemoveAllWeapons(Player player)
		{
			if (!IsPlayerConnected(player, out var reason))
			{
				Debug.WriteLine($"[Armory|Server] RemoveAllWeapons failed: {reason}");
				return;
			}

			BaseScript.TriggerClientEvent(player, "armory:RemoveAllWeapons");
			_weaponTracker.ClearWeapons(player);
			Debug.WriteLine($"[Armory|Server] Cleared all weapons from {player.Name}");
		}

		/// <summary>
		/// Validates the player reference and whether they are connected (ped exists).
		/// </summary>
		/// <param name="player">Player to validate.</param>
		/// <param name="reason">If invalid, contains the reason.</param>
		/// <returns>True if valid and connected; otherwise false.</returns>
		private static bool IsPlayerConnected(Player player, out string reason)
		{
			reason = null;

			try
			{
				if (player == null)
				{
					reason = "Player object is null";
					return false;
				}

				int ped = GetPlayerPed(player.Handle);
				if (ped <= 0)
				{
					reason = $"Player ID {player.Handle} is not online or ped not found";
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				reason = $"Exception checking player connection: {ex.Message}";
				return false;
			}
		}
	}
}
