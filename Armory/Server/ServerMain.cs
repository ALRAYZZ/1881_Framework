using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace armory.Server
{
    /// <summary>
    /// Server entry point. Wires services and registers events/commands.
    /// Uses a parameterless constructor for FiveM and an internal DI-friendly constructor for tests.
    /// </summary>
    public class ServerMain : BaseScript
	{
		private readonly WeaponService _weaponService;
		private readonly PickupService _pickupService;
		private readonly PlayerWeaponTracker _playerWeaponTracker;
		private readonly CommandHandler _commands;

		/// <summary>
		/// FiveM entrypoint. Composes concrete dependencies.
		/// </summary>
		public ServerMain() : this(
			weaponService: null,
			pickupService: null,
			playerWeaponTracker: null,
			chat: null)
		{
		}

		/// <summary>
		/// DI-friendly entrypoint. Any null dependency will be constructed with defaults.
		/// </summary>
		internal ServerMain(WeaponService weaponService, PickupService pickupService, PlayerWeaponTracker playerWeaponTracker, IChatMessenger chat)
		{
			// Get database connection from global
			var db = Exports["Database"];

			_playerWeaponTracker = playerWeaponTracker ?? new PlayerWeaponTracker();
			_weaponService = weaponService ?? new WeaponService(_playerWeaponTracker, db);
			_pickupService = pickupService ?? new PickupService(_weaponService);
			var messenger = chat ?? new ChatMessenger(Players, "[Armory]");

			_commands = new CommandHandler(_weaponService, _pickupService, Players, messenger);

			_commands.RegisterCommands();
			EventHandlers["playerDropped"] += new Action<Player, string>(_playerWeaponTracker.OnPlayerDropped);
			EventHandlers["armory:TryCollectWeaponPickup"] += new Action<Player, int>(OnTryCollectWeaponPickup);
			EventHandlers["UI:SelectedItem"] += new Action<Player, string, string>(OnUISelectedItem);

			// Load weapons when PlayerCore signals the player is ready (similar to PedManager pattern)
			EventHandlers["Armory:Server:LoadWeapons"] += new Action<string>(OnLoadWeapons);

			// Reload weapons after ped change (SetPlayerModel removes all weapons)
			EventHandlers["Armory:Server:ReloadWeapons"] += new Action<Player>(OnReloadWeapons);

			Debug.WriteLine("[Armory|Server] Armory initialized.");
		}

		/// <summary>
		/// Loads weapons for a player when they first join (called by PlayerCore).
		/// </summary>
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

		/// <summary>
		/// Reloads weapons after a ped model change (called by PedManager client).
		/// </summary>
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

		/// <summary>
		/// Server-side handler invoked when a client attempts to collect a weapon pickup.
		/// Delegates to the pickup service.
		/// </summary>
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
	}
}