using System;
using System.Threading.Tasks;
using CitizenFX.Core;


namespace UI.Server
{
    public class UIServer : BaseScript
    {
        public UIServer()
        {
            Debug.WriteLine("[UI] Server initialized");

			// Open menu for specific player
			EventHandlers["UI:OpenMenu"] += new Action<Player, string>((player, menuType) =>
            {
                TriggerClientEvent(player.Handle, "UI:OpenMenu", menuType);
            });


            // UI sends back selection
            EventHandlers["UI:SelectedItem"] += new Action<Player, string, string>((player, type, name) =>
            {
                Debug.WriteLine($"[UI] Player {player.Name} selected {name} from {type} menu");

                if (type == "ped")
                {
                    TriggerEvent("PedManager:Server:ApplyPed", player.Handle.ToString(), name);
                }
                else if (type == "weapon")
                {
                    TriggerEvent("Armory:Server:GiveWeapon", player.Handle, ToString(), name);
                }
            });
		}
	}
}