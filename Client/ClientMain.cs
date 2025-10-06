using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PedManager.Client
{
    public class ClientMain : BaseScript
    {
        private readonly IPedModelApplier _pedModelApplier;

        public ClientMain()
        {
            _pedModelApplier = new PedModelApplier();

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
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PedManager] Error applying model '{modelName}': {ex}");
                }
            });
		}
    }
}