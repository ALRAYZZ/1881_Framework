using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace armory.Server
{
	public class PickupInfo
	{
		public int Id { get; }
		public string WeaponName { get; }
		public int Ammo { get; }
		public Vector3 Pos { get; }
		public bool Collected { get; set; }
		public PickupInfo(int id, string weaponName, int ammo, Vector3 pos)
		{
			Id = id;
			WeaponName = weaponName;
			Ammo = ammo;
			Pos = pos;
			Collected = false;
		}
	}

	public class PickupService
	{
		private readonly WeaponService _weaponService;
		private readonly Dictionary<int, PickupInfo> _pickups = new Dictionary<int, PickupInfo>();
		private int _nextPickupId = 1;

		public PickupService(WeaponService weaponService)
		{
			_weaponService = weaponService;
		}

		public void SpawnWeaponPickup(Player player, string weapon, int ammo, Vector3? position = null)
		{
			var pos = position ?? GetEntityCoords(GetPlayerPed(player.Handle));
			int id = _nextPickupId++;

			_pickups[id] = new PickupInfo(id, weapon, ammo, pos);
			BaseScript.TriggerClientEvent("Armory:CreateWeaponPickup", id, weapon, ammo, pos.X, pos.Y, pos.Z);
			Debug.WriteLine($"[Armory|Server] Created pickup #{id} ({weapon}) at {pos.X}, {pos.Y}, {pos.Z}");
		}

		public void RemoveAllWeaponPickups()
		{
			if (_pickups.Count == 0)
			{
				Debug.WriteLine("[Armory|Server] No weapon pickups to remove.");
				BaseScript.TriggerClientEvent("Armory:RemoveAllWeaponPickups");
				return;
			}

			foreach (var kvp in _pickups)
			{
				kvp.Value.Collected = true;
			}
			_pickups.Clear();

			BaseScript.TriggerClientEvent("Armory:RemoveAllWeaponPickups");
			Debug.WriteLine("[Armory|Server] Removed all weapon pickups.");
		}

		public void TryCollectWeaponPickup(Player player, int id)
		{
			Debug.WriteLine($"[Armory|Server] TryCollectWeaponPickup called - Player: {player?.Handle ?? "null"}, Name: {player?.Name ?? "null"}, ID: {id}");

			if (player == null)
			{
				Debug.WriteLine($"[Armory|Server] ERROR: Player is null for pickup id={id}");
				return;
			}

			if (!_pickups.TryGetValue(id, out var p))
			{
				Debug.WriteLine($"[Armory|Server] Pickup id={id} not found in dictionary. Current pickups: {string.Join(", ", _pickups.Keys)}");
				return;
			}

			if (p.Collected)
			{
				Debug.WriteLine($"[Armory|Server] Pickup id={id} already collected by {player.Name}");
				return;
			}

			p.Collected = true;
			_pickups.Remove(id);

			BaseScript.TriggerClientEvent("Armory:RemoveWeaponPickup", id);
			_weaponService.GiveWeapon(player, p.WeaponName);

			Debug.WriteLine($"[Armory|Server] {player.Name} collected pickup #{id} ({p.WeaponName}, ammo={p.Ammo})");
		}
	}
}
