using System;
using System.Collections.Generic;
using System.Text;
using CitizenFX.Core;

namespace armory.Server
{
	public class PlayerWeaponTracker
	{
		private readonly Dictionary<string, HashSet<string>> _playerWeapons = new Dictionary<string, HashSet<string>>();

		public HashSet<string> GetCurrentWeapons(Player player)
		{
			if (player == null || string.IsNullOrEmpty(player.Handle)) return null;

			if (player == null || string.IsNullOrEmpty(player.Handle)) return null;
			return _playerWeapons.TryGetValue(player.Handle, out var set)
				? set
				: _playerWeapons[player.Handle] = new HashSet<string>();
		}

		public void AddWeapon(Player player, string weapon)
			=> GetCurrentWeapons(player)?.Add(weapon);

		public void RemoveWeapon(Player player, string weapon)
			=> GetCurrentWeapons(player)?.Remove(weapon);

		public void ClearWeapons(Player player)
			=> GetCurrentWeapons(player)?.Clear();

		public bool HasWeapon(Player player, string weapon)
			=> GetCurrentWeapons(player)?.Contains(weapon) ?? false;

		public void OnPlayerDropped(Player player, string reason)
		{
			if (player == null) return;
			_playerWeapons.Remove(player.Handle);
		}
	}
}
