using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace armory.Server
{
	/// Registers and handles server commands for the armory (weapons and pickups).
	public class CommandHandler
	{
		private readonly IWeaponService _weaponService;
		private readonly PickupService _pickupService;
		private readonly PlayerList _players;
		private readonly IChatMessenger _chat;

		/// Creates a command handler that delegates to weapon/pickup services and replies to invokers.
		public CommandHandler(IWeaponService weaponService, PickupService pickupService, PlayerList players, IChatMessenger chat)
		{
			_weaponService = weaponService ?? throw new ArgumentNullException(nameof(weaponService));
			_pickupService = pickupService ?? throw new ArgumentNullException(nameof(pickupService));
			_players = players ?? throw new ArgumentNullException(nameof(players));
			_chat = chat ?? throw new ArgumentNullException(nameof(chat));
		}

		/// Registers all commands exposed by the armory on the server.
		public void RegisterCommands()
		{
			// USAGE: /giveweapon [targetId] [weaponName] [components] [tintIndex]
			// components format: "comp1,comp2,comp3" or empty
			// tintIndex: 0-7 or -1 for default
			RegisterCommand("giveweapon", new Action<int, List<object>, string>(HandleGiveWeapon), true);
			// USAGE: /removeweapon [targetId] [weaponName]
			RegisterCommand("removeweapon", new Action<int, List<object>, string>(HandleRemoveWeapon), true);
			// USAGE: /removeweaponall [targetId]
			RegisterCommand("removeweaponall", new Action<int, List<object>, string>(HandleRemoveAllWeapons), true);
			// USAGE: /spawnweaponpickup [weaponName] [ammo] [x] [y] [z]
			RegisterCommand("spawnweaponpickup", new Action<int, List<object>, string>(HandleWeaponPickup), true);
			// USAGE: /clearweaponpickups
			RegisterCommand("clearweaponpickups", new Action<int, List<object>, string>(HandleClearWeaponPickups), true);
		}

		/// Command handler: gives a weapon to a target player with optional components and tint.
		private void HandleGiveWeapon(int src, List<object> args, string raw)
		{
			// If no args, open weapon selection menu
			// FiveM passes an empty string as first arg if no args provided
			bool hasNoArgs = args.Count == 0 || (args.Count == 1 && string.IsNullOrWhiteSpace(args[0]?.ToString()));
			if (hasNoArgs)
			{
				Debug.WriteLine($"[Armory|Server] No arguments provided, opening weapon menu.");
				OpenWeaponMenu(src);
				return;
			}

			if (!TryResolveTargetWithExtras(src, args, out var player, out var weapon, out var components, out var tintIndex)) return;

			_weaponService.GiveWeapon(player, weapon, components, tintIndex);
			_chat.Info(src, $"Gave {weapon} to {player?.Name ?? "unknown"}.");
		}

		/// Command handler: removes a weapon (or all weapons if none provided) from a player.
		private void HandleRemoveWeapon(int src, List<object> args, string raw)
		{
			if (!TryResolveTarget(src, args, out var player, out var weapon)) return;

			if (string.IsNullOrEmpty(weapon))
			{
				_weaponService.RemoveAllWeapons(player);
				_chat.Info(src, $"Removed all weapons from {player?.Name ?? "unknown"}.");
			}
			else
			{
				_weaponService.RemoveWeapon(player, weapon);
				_chat.Info(src, $"Removed {weapon} from {player?.Name ?? "unknown"}.");
			}
		}

		/// Command handler: removes all weapons from a target player (or the executor).
		private void HandleRemoveAllWeapons(int src, List<object> args, string raw)
		{
			Player target;
			int targetId = src;

			// If targetId provided, use it; otherwise use the command executor
			if (args.Count > 0 && int.TryParse(args[0]?.ToString(), out var parsedId))
			{
				targetId = parsedId;
			}

			try
			{
				target = _players[targetId];
			}
			catch
			{
				target = null;
			}

			if (target == null)
			{
				_chat.Error(src, $"Player ID {targetId} not found or not online.");
				return;
			}

			_weaponService.RemoveAllWeapons(target);
			_chat.Info(src, $"Removed all weapons from {target.Name}.");
		}

		/// Command handler: spawns a weapon pickup near the player or at given coordinates.
		private void HandleWeaponPickup(int src, List<object> args, string raw)
		{
			var player = _players[src];
			string requested = args.ElementAtOrDefault(0)?.ToString() ?? "WEAPON_PISTOL";
			if (!WeaponValidator.TryNormalizeWeaponName(requested, out var weapon, out _))
			{
				_chat.Error(src, $"Invalid weapon for pickup: {requested}");
				return;
			}

			int ammo = int.TryParse(args.ElementAtOrDefault(1)?.ToString(), out var a) ? a : 250;

			// Parse coordinates if provided (args 2, 3, 4 = x, y, z)
			Vector3? position = null;
			if (args.Count >= 5)
			{
				bool xValid = float.TryParse(args[2]?.ToString(), out var x);
				bool yValid = float.TryParse(args[3]?.ToString(), out var y);
				bool zValid = float.TryParse(args[4]?.ToString(), out var z);

				if (xValid && yValid && zValid)
				{
					position = new Vector3(x, y, z);
				}
				else
				{
					_chat.Warn(src, "Invalid coordinates provided; spawning at your position.");
				}
			}

			_pickupService.SpawnWeaponPickup(player, weapon, ammo, position);
			_chat.Info(src, $"Spawned pickup {weapon} ({ammo} ammo).");
		}

		/// Command handler: clears all existing weapon pickups.
		private void HandleClearWeaponPickups(int src, List<object> args, string raw)
		{
			_pickupService.RemoveAllWeaponPickups();
			_chat.Info(src, "Cleared all weapon pickups.");
		}


		// Opens a weapon selection menu for the player to choose a weapon to receive. (Is called by HandleGiveWeapon with no args)
		private void OpenWeaponMenu(int src)
		{
			try
			{
				var player = _players[src];
				if (player == null)
				{
					Debug.WriteLine($"[Armory|Server] ERROR: Could not find player {src}");
					return;
				}

				var weaponList = WeaponValidator.GetAllWeapons();
				Debug.WriteLine($"[Armory|Server] Opening weapon menu for player {src} ({player.Name}) with {weaponList.Count} weapons");

				// Trigger client event to open the weapon selection menu
				BaseScript.TriggerClientEvent(player, "UI:OpenWeaponMenu", weaponList);
				Debug.WriteLine($"[Armory|Server] UI:OpenWeaponMenu event triggered for player {src}");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Armory|Server] ERROR in OpenWeaponMenu: {ex.Message}");
			}
		}

		private bool TryResolveTarget(int src, List<object> args, out Player target, out string weapon)
		{
			target = null;
			weapon = "WEAPON_PISTOL";
			int targetId = src;

			if (args.Count > 0)
			{
				if (int.TryParse(args[0]?.ToString(), out var parsedId))
				{
					targetId = parsedId;
				}
				else
				{
					weapon = args[0]?.ToString();
				}
			}

			if (args.Count > 1 && !int.TryParse(args[0]?.ToString(), out _))
			{
				weapon = args[1]?.ToString();
			}

			// Try to get the player
			try
			{
				target = _players[targetId];
			}
			catch
			{
				target = null;
			}

			if (target == null)
			{
				_chat.Error(src, $"Player ID {targetId} not found or not online.");
				return false;
			}

			if (!WeaponValidator.TryNormalizeWeaponName(weapon, out var normalized, out _))
			{
				_chat.Error(src, $"Invalid weapon name: {weapon}");
				return false;
			}

			weapon = normalized;
			return true;
		}

		/// Attempts to resolve a target, weapon, optional components, and tint index from args.
		/// Replies with errors on failure.
		private bool TryResolveTargetWithExtras(int src, List<object> args, out Player target, out string weapon, out List<string> components, out int tintIndex)
		{
			target = null;
			weapon = "WEAPON_PISTOL";
			components = null;
			tintIndex = -1;
			int targetId = src;
			int weaponArgIndex = 0;

			if (args.Count > 0)
			{
				if (int.TryParse(args[0]?.ToString(), out var parsedId))
				{
					targetId = parsedId;
					weaponArgIndex = 1;
				}
			}

			// Get weapon name
			if (args.Count > weaponArgIndex)
			{
				weapon = args[weaponArgIndex]?.ToString();
			}

			// Try to get the player
			try
			{
				target = _players[targetId];
			}
			catch
			{
				target = null;
			}

			if (target == null)
			{
				_chat.Error(src, $"Player ID {targetId} not found or not online.");
				return false;
			}

			if (!WeaponValidator.TryNormalizeWeaponName(weapon, out var normalized, out _))
			{
				_chat.Error(src, $"Invalid weapon name: {weapon}");
				return false;
			}

			weapon = normalized;

			// Parse components (comma-separated)
			if (args.Count > weaponArgIndex + 1)
			{
				var componentsArg = args[weaponArgIndex + 1]?.ToString();
				if (!string.IsNullOrWhiteSpace(componentsArg))
				{
					components = new List<string>(componentsArg.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(c => c.Trim().ToUpperInvariant())
						.Where(c => !string.IsNullOrWhiteSpace(c)));
				}
			}

			// Parse tint index (0-7 typically)
			if (args.Count > weaponArgIndex + 2)
			{
				if (int.TryParse(args[weaponArgIndex + 2]?.ToString(), out var parsedTint))
				{
					tintIndex = parsedTint;
				}
			}

			return true;
		}
	}
}
