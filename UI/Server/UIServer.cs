using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace UI.Server
{
    public class UIServer : BaseScript
    {
        public UIServer()
        {
            Debug.WriteLine("[UI:Server] UIServer INITIALIZING");

			// Prove the resource started
			EventHandlers["onResourceStart"] += new Action<string>((res) =>
			{
				if (res == GetCurrentResourceName())
					Debug.WriteLine($"[UI:Server] onResourceStart for '{res}'");
			});

            EventHandlers["UI:SelectedItem"] += new Action<Player, string, string>(OnSelectedItem);
			Debug.WriteLine("[UI:Server] EventHandlers registered");
		}

        private void OnSelectedItem([FromSource] Player player, string type, string name)
        {
            if (player == null)
            {
                Debug.WriteLine("[UI:Server] ERROR: Player is null");
                return;
            }

            Debug.WriteLine($"[UI:Server] OnSelectedItem triggered by player {player.Name} (ID: {player.Handle}) with type={type}, name={name}");

            switch (type)
            {
                case "ped":
                    HandlePedSelection(player, name);
                    break;
                case "weapon":
                    HandleWeaponSelection(player, name);
                    break;
                default:
                    Debug.WriteLine($"[UI:Server] WARNING: Unknown type '{type}' received from player {player.Name}");
                    break;
            }
		}

        private void HandlePedSelection(Player player, string pedName)
        {
            Debug.WriteLine($"[UI:Server] Appliying ped '{pedName}' to player {player.Name}");
            TriggerEvent("PedManager:Server:SetPed", player.Handle, pedName);
        }

        private void HandleWeaponSelection(Player player, string weaponName)
        {
            Debug.WriteLine($"[UI:Server] Giving weapon '{weaponName}' to player {player.Name}");
            TriggerEvent("WeaponManager:Server:GiveWeapon", player.Handle, weaponName);
        }


		private void OnSelectedItemSrc(int src, string type, string name)
		{
			Debug.WriteLine($"[UI:Server] (Src) Received UI:SelectedItem from src={src} type={type}, name={name}");
			var handle = src.ToString(System.Globalization.CultureInfo.InvariantCulture);
			RouteSelection(handle, type, name);
		}

		private void RouteSelection(string handle, string type, string name)
		{
			switch ((type ?? string.Empty).ToLowerInvariant())
			{
				case "ped":
					Debug.WriteLine($"[UI:Server] Trigger PedManager:Server:SetPed({handle}, {name})");
					TriggerEvent("PedManager:Server:SetPed", handle, name);
					break;
				case "weapon":
					Debug.WriteLine($"[UI:Server] Trigger WeaponManager:Server:GiveWeapon({handle}, {name})");
					TriggerEvent("WeaponManager:Server:GiveWeapon", handle, name);
					break;
				default:
					Debug.WriteLine($"[UI:Server] WARNING: Unknown type '{type}'");
					break;
			}
		}
	}
}