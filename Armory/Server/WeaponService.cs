using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace armory.Server
{
	/// Abstraction for server-side weapon operations triggered by commands or pickups.
	public interface IWeaponService
	{
		/// Gives a weapon to the specified player with optional components and tint.
		void GiveWeapon(Player player, string weapon, List<string> components = null, int tintIndex = -1);

		/// Removes a specific weapon from the specified player.
		void RemoveWeapon(Player player, string weapon);

		/// Removes all weapons from the specified player.
		void RemoveAllWeapons(Player player);

		/// Loads and applies all weapons for the specified player from the database.
		void LoadWeaponsForPlayer(Player player);
	}

	/// Default server-side implementation for giving/removing weapons.
	public class WeaponService : IWeaponService
	{
		private readonly PlayerWeaponTracker _weaponTracker;
		private readonly dynamic _db;

		/// Creates a weapon service using the provided player weapon tracker and database export.
		public WeaponService(PlayerWeaponTracker weaponTracker, dynamic db)
		{
			_weaponTracker = weaponTracker ?? throw new ArgumentNullException(nameof(weaponTracker));
			_db = db;

			if (_db == null)
			{
				Debug.WriteLine("[Armory|Server] WARNING: Database export is null. Player weapon persistence disabled.");
			}
		}

		public void GiveWeapon(Player player, string weapon, List<string> components = null, int tintIndex = -1)
		{
			if (!IsPlayerConnected(player, out var reason))
			{
				Debug.WriteLine($"[Armory|Server] GiveWeapon failed: {reason}");
				return;
			}

			Debug.WriteLine($"[Armory|Server] GiveWeapon called with weapon: '{weapon}', components: {components?.Count ?? 0}, tintIndex: {tintIndex}");
			BaseScript.TriggerClientEvent(player, "Armory:ReceiveWeapon", weapon, components ?? new List<string>(), tintIndex);
			_weaponTracker.AddWeapon(player, weapon);
			
			// Persist to database
			PersistWeaponToDatabase(player, weapon, 0, components, null);
			
			Debug.WriteLine($"[Armory|Server] Gave {weapon} to {player.Name}");
		}

		public void RemoveWeapon(Player player, string weapon)
		{
			if (!IsPlayerConnected(player, out var reason))
			{
				Debug.WriteLine($"[Armory|Server] RemoveWeapon failed: {reason}");
				return;
			}

			BaseScript.TriggerClientEvent(player, "Armory:RemoveWeapon", weapon);
			_weaponTracker.RemoveWeapon(player, weapon);
			
			// Remove from database
			RemoveWeaponFromDatabase(player, weapon);
			
			Debug.WriteLine($"[Armory|Server] Removed {weapon} from {player.Name}");
		}

		public void RemoveAllWeapons(Player player)
		{
			if (!IsPlayerConnected(player, out var reason))
			{
				Debug.WriteLine($"[Armory|Server] RemoveAllWeapons failed: {reason}");
				return;
			}

			BaseScript.TriggerClientEvent(player, "Armory:RemoveAllWeapons");
			_weaponTracker.ClearWeapons(player);
			
			// Remove all from database
			RemoveAllWeaponsFromDatabase(player);
			
			Debug.WriteLine($"[Armory|Server] Cleared all weapons from {player.Name}");
		}

		public void LoadWeaponsForPlayer(Player player)
		{
			if (!IsPlayerConnected(player, out var reason))
			{
				Debug.WriteLine($"[Armory|Server] LoadWeaponsForPlayer failed: {reason}");
				return;
			}

			if (_db == null)
			{
				Debug.WriteLine("[Armory|Server] Database not available. Skipping weapon load.");
				return;
			}

			var identifier = GetStableIdentifier(player);
			if (string.IsNullOrWhiteSpace(identifier))
			{
				Debug.WriteLine("[Armory|Server] Could not resolve stable identifier for weapon load.");
				return;
			}

			var parameters = new Dictionary<string, object>
			{
				{ "@identifier", identifier }
			};

			try
			{
				_db.Query(
					"SELECT weapon, ammo, components FROM player_weapons WHERE identifier = @identifier",
					parameters,
					new Action<dynamic>((rows) =>
					{
						if (rows == null || rows.Count == 0)
						{
							Debug.WriteLine($"[Armory|Server] No stored weapons found for {identifier}");
							return;
						}

						Debug.WriteLine($"[Armory|Server] Loading {rows.Count} weapon(s) for {player.Name}");

						foreach (var row in rows)
						{
							string weaponName = row.weapon;
							int ammo = row.ammo ?? 0;
							
							// Parse components JSON
							List<string> components = new List<string>();
							try
							{
								if (row.components != null)
								{
									string componentsJson = row.components.ToString();
									if (!string.IsNullOrWhiteSpace(componentsJson) && componentsJson != "[]")
									{
										// Simple JSON parsing for array of strings
										componentsJson = componentsJson.Trim('[', ']').Replace("\"", "");
										if (!string.IsNullOrWhiteSpace(componentsJson))
										{
											components.AddRange(componentsJson.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
										}
									}
								}
							}
							catch (Exception ex)
							{
								Debug.WriteLine($"[Armory|Server] Error parsing components: {ex.Message}");
							}

							// Give weapon without persisting again
							BaseScript.TriggerClientEvent(player, "Armory:ReceiveWeapon", weaponName, components, -1);
							_weaponTracker.AddWeapon(player, weaponName);
							
							Debug.WriteLine($"[Armory|Server] Loaded weapon {weaponName} with {components.Count} component(s)");
						}
					})
				);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Armory|Server] Error loading weapons: {ex.Message}");
			}
		}

		/// Persists weapon data to both player_inventory and player_weapons tables.
		private void PersistWeaponToDatabase(Player player, string weapon, int ammo, List<string> components, string grantedBy)
		{
			if (_db == null) return;

			var identifier = GetStableIdentifier(player);
			if (string.IsNullOrWhiteSpace(identifier))
			{
				Debug.WriteLine("[Armory|Server] Could not resolve stable identifier; skipping weapon save.");
				return;
			}

			// Convert components list to JSON string
			string componentsJson = "[]";
			if (components != null && components.Count > 0)
			{
				componentsJson = "[\"" + string.Join("\",\"", components) + "\"]";
			}

			// First, insert into player_weapons
			var weaponParams = new Dictionary<string, object>
			{
				{ "@identifier", identifier },
				{ "@weapon", weapon },
				{ "@ammo", ammo },
				{ "@components", componentsJson },
				{ "@granted_by", grantedBy ?? "system" }
			};

			try
			{
				_db.Insert(
					"INSERT INTO player_weapons (identifier, weapon, ammo, components, granted_by, date_granted) " +
					"VALUES (@identifier, @weapon, @ammo, @components, @granted_by, NOW()) " +
					"ON DUPLICATE KEY UPDATE ammo = @ammo, components = @components",
					weaponParams,
					new Action<dynamic>((weaponId) =>
					{
						Debug.WriteLine($"[Armory|Server] Saved weapon {weapon} to player_weapons, id: {weaponId}");

						// Now insert into player_inventory
						var inventoryParams = new Dictionary<string, object>
						{
							{ "@identifier", identifier },
							{ "@item_type", "weapon" },
							{ "@item_id", weaponId },
							{ "@quantity", 1 }
						};

						_db.Insert(
							"INSERT INTO player_inventory (identifier, item_type, item_id, quantity, added_at) " +
							"VALUES (@identifier, @item_type, @item_id, @quantity, NOW()) " +
							"ON DUPLICATE KEY UPDATE quantity = @quantity",
							inventoryParams,
							new Action<dynamic>((inventoryId) =>
							{
								Debug.WriteLine($"[Armory|Server] Saved weapon to player_inventory, id: {inventoryId}");
							})
						);
					})
				);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Armory|Server] Error persisting weapon: {ex.Message}");
			}
		}

		/// Removes weapon from both player_inventory and player_weapons tables.
		private void RemoveWeaponFromDatabase(Player player, string weapon)
		{
			if (_db == null) return;

			var identifier = GetStableIdentifier(player);
			if (string.IsNullOrWhiteSpace(identifier))
			{
				Debug.WriteLine("[Armory|Server] Could not resolve stable identifier; skipping weapon removal.");
				return;
			}

			var parameters = new Dictionary<string, object>
			{
				{ "@identifier", identifier },
				{ "@weapon", weapon }
			};

			try
			{
				// First get the weapon ID, then remove from inventory
				_db.Query(
					"SELECT id FROM player_weapons WHERE identifier = @identifier AND weapon = @weapon",
					parameters,
					new Action<dynamic>((rows) =>
					{
						if (rows != null && rows.Count > 0)
						{
							var weaponId = rows[0].id;

							// Remove from player_inventory
							var invParams = new Dictionary<string, object>
							{
								{ "@identifier", identifier },
								{ "@item_id", weaponId }
							};

							_db.Query(
								"DELETE FROM player_inventory WHERE identifier = @identifier AND item_type = 'weapon' AND item_id = @item_id",
								invParams,
								new Action<dynamic>((_) =>
								{
									Debug.WriteLine($"[Armory|Server] Removed weapon from player_inventory");
								})
							);

							// Remove from player_weapons
							_db.Query(
								"DELETE FROM player_weapons WHERE identifier = @identifier AND weapon = @weapon",
								parameters,
								new Action<dynamic>((_) =>
								{
									Debug.WriteLine($"[Armory|Server] Removed weapon {weapon} from database");
								})
							);
						}
					})
				);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Armory|Server] Error removing weapon from database: {ex.Message}");
			}
		}

		/// Removes all weapons from both tables for the specified player.
		private void RemoveAllWeaponsFromDatabase(Player player)
		{
			if (_db == null) return;

			var identifier = GetStableIdentifier(player);
			if (string.IsNullOrWhiteSpace(identifier))
			{
				Debug.WriteLine("[Armory|Server] Could not resolve stable identifier; skipping all weapons removal.");
				return;
			}

			var parameters = new Dictionary<string, object>
			{
				{ "@identifier", identifier }
			};

			try
			{
				// Remove from player_inventory first
				_db.Query(
					"DELETE FROM player_inventory WHERE identifier = @identifier AND item_type = 'weapon'",
					parameters,
					new Action<dynamic>((_) =>
					{
						Debug.WriteLine($"[Armory|Server] Removed all weapons from player_inventory");
					})
				);

				// Remove from player_weapons
				_db.Query(
					"DELETE FROM player_weapons WHERE identifier = @identifier",
					parameters,
					new Action<dynamic>((_) =>
					{
						Debug.WriteLine($"[Armory|Server] Removed all weapons from player_weapons");
					})
				);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Armory|Server] Error removing all weapons from database: {ex.Message}");
			}
		}

		/// Gets the identifier from player; prefers license2, then license, then first available.
		private static string GetStableIdentifier(Player player)
		{
			if (player == null) return null;

			string license = null, fallback = null;
			var count = GetNumPlayerIdentifiers(player.Handle);
			for (int i = 0; i < count; i++)
			{
				var id = GetPlayerIdentifier(player.Handle, i);
				if (string.IsNullOrEmpty(id)) continue;

				if (id.StartsWith("license2:", StringComparison.OrdinalIgnoreCase)) return id;
				if (id.StartsWith("license:", StringComparison.OrdinalIgnoreCase) && license == null) license = id;
				if (fallback == null) fallback = id;
			}
			return license ?? fallback;
		}

		/// Validates the player reference and whether they are connected (ped exists).
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
