using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace armory.Server
{
    /// Server entry point. Wires services and registers events/commands.
    /// Uses a parameterless constructor for FiveM and an internal DI-friendly constructor for tests.
    public class ArmoryServerMain : BaseScript
	{
		private readonly WeaponService _weaponService;
		private readonly PickupService _pickupService;
		private readonly PlayerWeaponTracker _playerWeaponTracker;
		private readonly CommandHandler _commands;

		/// FiveM entrypoint. Composes concrete dependencies.
		public ArmoryServerMain() : this(
			weaponService: null,
			pickupService: null,
			playerWeaponTracker: null,
			chat: null)
		{
		}

		/// DI-friendly entrypoint. Any null dependency will be constructed with defaults.
		internal ArmoryServerMain(WeaponService weaponService, PickupService pickupService, PlayerWeaponTracker playerWeaponTracker, IChatMessenger chat)
		{
			// Get database connection from global
			var db = Exports["Database"];

			_playerWeaponTracker = playerWeaponTracker ?? new PlayerWeaponTracker();
			_weaponService = weaponService ?? new WeaponService(_playerWeaponTracker, db);
			_pickupService = pickupService ?? new PickupService(_weaponService);
			var messenger = chat ?? new ChatMessenger(Players, "[Armory]");

			_commands = new CommandHandler(_weaponService, _pickupService, Players, messenger);

			_commands.RegisterCommands();

			// Handle player drop robustly and persist weapons
			EventHandlers["playerDropped"] += new Action<Player, string>(OnPlayerDropped);

			EventHandlers["Armory:TryCollectWeaponPickup"] += new Action<Player, int>(OnTryCollectWeaponPickup);
			EventHandlers["UI:SelectedItem"] += new Action<Player, string, string>(OnUISelectedItem);

			// Armory API events 
			EventHandlers["Armory:Server:LoadWeapons"] += new Action<string>(OnLoadWeapons);
			EventHandlers["Armory:Server:ReloadWeapons"] += new Action<Player>(OnReloadWeapons);
			EventHandlers["Armory:Server:RemoveAllWeapons"] += new Action<string>(OnRemoveAllWeapons);

			// Allow PlayerCore (or others) to request a persistence snapshot pre-drop
			EventHandlers["Armory:Server:PersistWeaponsNow"] += new Action<string>(OnPersistWeaponsNow);

			// PlayerCore fires OnSpawned after spawnmanager finishes
			EventHandlers["PlayerCore:Server:OnSpawned"] += new Action<Player>(OnPlayerCoreSpawned);

			// Clear weapons on death
			EventHandlers["PlayerCore:Server:PlayerDied"] += new Action<Player>(OnPlayerCorePlayerDied);

			Debug.WriteLine("[Armory|Server] Armory initialized.");
		}

		/// Loads weapons for a player when they first join (called by PlayerCore).
		private void OnLoadWeapons(string serverId)
		{
			var player = Players.FirstOrDefault(p => p.Handle == serverId);
			if (player == null)
			{
				Debug.WriteLine($"[Armory|Server] LoadWeapons: player '{serverId}' not found.");
				return;
			}

			_weaponService.LoadWeaponsForPlayer(player);
			Debug.WriteLine($"[Armory|Server] Loaded weapons for {player.Name}");
		}

		private void OnRemoveAllWeapons(string serverId)
		{	
			var player = Players.FirstOrDefault(p => p.Handle == serverId);
			if (player == null)
			{
				Debug.WriteLine($"[Armory|Server] RemoveAllWeapons: player '{serverId}' not found.");
				return;
			}

			_weaponService.RemoveAllWeapons(player);
			Debug.WriteLine($"[Armory|Server] Removed all weapons from {player.Name}");
		}

		// Allow an explicit persist request (e.g., before Drop)
		private void OnPersistWeaponsNow(string serverId)
		{
			try
			{
				var player = Players.FirstOrDefault(p => p.Handle == serverId);
				if (player == null)
				{
					Debug.WriteLine($"[Armory|Server] PersistWeaponsNow: player '{serverId}' not found.");
					return;
				}

				_weaponService.SaveWeaponsForPlayer(player);
				Debug.WriteLine($"[Armory|Server] Persisted weapons for {player.Name} (explicit request)");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Armory|Server] PersistWeaponsNow error: {ex}");
			}
		}

		// Realods weapons (from DB) after any spawn mananged by PlayerCore
		private void OnPlayerCoreSpawned([FromSource] Player player)
		{
			if (player == null)
			{
				Debug.WriteLine("[Armory|Server] OnPlayerCoreSpawned: player is null!");
				return;
			}
			Debug.WriteLine($"[Armory|Server] OnPlayerCoreSpawned triggered by {player.Name} ({player.Handle})");
			_weaponService.LoadWeaponsForPlayer(player);
			Debug.WriteLine($"[Armory|Server] Loaded weapons for {player.Name} after spawn");
		}

		// Clear weapons on death
		private void OnPlayerCorePlayerDied([FromSource] Player player)
		{
			if (player == null)
			{
				Debug.WriteLine("[Armory|Server] OnPlayerCorePlayerDied: player is null!");
				return;
			}
			_weaponService.RemoveAllWeapons(player);
			Debug.WriteLine($"[Armory|Server] Removed all weapons from {player.Name} after death");
		}

		/// Reloads weapons after a ped model change (called by PedManager client).
		private void OnReloadWeapons([FromSource] Player player)
		{
			if (player == null)
			{
				Debug.WriteLine("[Armory|Server] ReloadWeapons: player is null!");
				return;
			}

			Debug.WriteLine($"[Armory|Server] ReloadWeapons triggered by {player.Name} ({player.Handle})");
			_weaponService.LoadWeaponsForPlayer(player);
			Debug.WriteLine($"[Armory|Server] Reloaded weapons for {player.Name} after ped change");
		}

		/// Server-side handler invoked when a client attempts to collect a weapon pickup.
		/// Delegates to the pickup service.
		private void OnTryCollectWeaponPickup([FromSource] Player player, int id)
		{
			_pickupService.TryCollectWeaponPickup(player, id);
		}

		private void OnUISelectedItem([FromSource] Player player, string type, string name)
		{
			if (player == null)
			{
				Debug.WriteLine("[Armory|Server] OnUISelectedItem: player is null!");
				return;
			}

			Debug.WriteLine($"[Armory|Server] OnUISelectedItem called by player {player.Handle} with type '{type}' and name '{name}'");

			if (type == "weapon")
			{
				if (WeaponValidator.TryNormalizeWeaponName(name, out var normalized, out _))
				{
					_weaponService.GiveWeapon(player, normalized);
					Debug.WriteLine($"[Armory|Server] Gave weapon '{normalized}' to player {player.Handle}");
				}
				else 
				{
					Debug.WriteLine($"[Armory|Server] Invalid weapon name selected: '{name}'");
				}
			}
		}

		// Handles native drop event; persists and clears tracker with full safety.
		private void OnPlayerDropped([FromSource] Player player, string reason)
		{
			try
			{
				if (player == null)
				{
					Debug.WriteLine("[Armory|Server] playerDropped: player is null!");
					return;
				}

				// Persist current weapons snapshot (best-effort)
				_weaponService.SaveWeaponsForPlayer(player);

				// Always clear tracker to avoid leaks
				_playerWeaponTracker.OnPlayerDropped(player, reason);

				Debug.WriteLine($"[Armory|Server] playerDropped handled for {player.Name} ({player.Handle}). Reason: {reason}");
			}
			catch (Exception ex)
			{
				// Prevent the event pipeline from throwing
				Debug.WriteLine($"[Armory|Server] Error in playerDropped handler: {ex}");
			}
		}
	}
}