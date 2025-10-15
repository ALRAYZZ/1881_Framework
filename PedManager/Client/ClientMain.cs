using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PedManager.Client
{
    public class ClientMain : BaseScript
    {
        private readonly IPedModelApplier _pedModelApplier;
        private readonly PersistentPedClient _persistentPedClient;
        private readonly PedEvents _pedEvents;

        public ClientMain()
        {
            Debug.WriteLine("[PedManager] ClientMain initialized.");

            _persistentPedClient = new PersistentPedClient(EventHandlers);
            _pedModelApplier = new PedModelApplier();
            _pedEvents = new PedEvents(EventHandlers);

            Debug.WriteLine("[PedManager] Event handlers being registered.");

            bool spawned = false;
            EventHandlers["playerSpawned"] +=  new Action<dynamic>((_) => spawned = true);

            // Server authoritative event to set ped model
            EventHandlers["PedManager:Client:SetPed"] += new Action<string>(async (modelName) =>
            {
                try
                {
					// Wait untill playerSpawned is triggered
                    while (!spawned)
                    {
                        await Delay(100);
                    }

                    await Delay(250);
					await _pedModelApplier.ApplyAsync(modelName);

					// After any model swap, weapons are wiped. Ask Armory to re-equip
                    TriggerServerEvent("Armory:Server:ReloadWeapons");
				}
				catch (Exception ex)
                {
                    Debug.WriteLine($"[PedManager] Error applying model '{modelName}': {ex}");
                }
            });

			// NEW: Server authoritative event to set ped model DURING GAMEPLAY (no wait on playerSpawned)
			EventHandlers["PedManager:Client:ApplyPedNow"] += new Action<string>(async (modelName) =>
			{
				Debug.WriteLine($"[PedManager] Client received ApplyPedNow event with model: '{modelName}'");
				try
				{
					await _pedModelApplier.ApplyAsync(modelName);

                    // Ensure Armory reloads after runtime ped swaps
                    TriggerServerEvent("Armory:Server:ReloadWeapons");
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[PedManager] Error applying model '{modelName}': {ex}");
				}
			});
		}
    }
}