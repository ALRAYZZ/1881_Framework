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
			_playerWeaponTracker = playerWeaponTracker ?? new PlayerWeaponTracker();
			_weaponService = weaponService ?? new WeaponService(_playerWeaponTracker);
			_pickupService = pickupService ?? new PickupService(_weaponService);
			var messenger = chat ?? new ChatMessenger(Players, "[Armory]");

			_commands = new CommandHandler(_weaponService, _pickupService, Players, messenger);

			_commands.RegisterCommands();
			EventHandlers["playerDropped"] += new Action<Player, string>(_playerWeaponTracker.OnPlayerDropped);
			EventHandlers["armory:TryCollectWeaponPickup"] += new Action<Player, int>(OnTryCollectWeaponPickup);

			Debug.WriteLine("[Armory|Server] Armory initialized.");
		}

		/// <summary>
		/// Server-side handler invoked when a client attempts to collect a weapon pickup.
		/// Delegates to the pickup service.
		/// </summary>
		private void OnTryCollectWeaponPickup([FromSource] Player player, int id)
		{
			_pickupService.TryCollectWeaponPickup(player, id);
		}
	}
}