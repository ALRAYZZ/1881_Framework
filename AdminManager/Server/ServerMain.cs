using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using AdminManager.Server.Services;

namespace AdminManager.Server
{
    public class ServerMain : BaseScript
    {
        private readonly TeleportServerService _teleportService;
        private readonly VehicleServerService _vehicleService;
        private readonly PedServerService _pedService;

        public ServerMain()
        {
            _teleportService = new TeleportServerService();
            _vehicleService = new VehicleServerService();
            _pedService = new PedServerService();

			// Teleport an entity by its network ID
            EventHandlers["AdminManager:Teleport:NetID"] += new Action<Player, int, float, float, float>(_teleportService.OnTeleportByNetId);

            // Go to an entity by its network ID
            EventHandlers["AdminManager:Teleport:GoToEntity"] += new Action<Player, int>(_teleportService.OnGoToEntity);

			// Bring an entity by its network ID
            EventHandlers["AdminManager:Teleport:BringEntity"] += new Action<Player, int>(_teleportService.OnBringEntity);

			// Get vehicle info by its network ID
            EventHandlers["AdminManager:Vehicle:GetInfo"] += new Action<Player, int, float>(_vehicleService.OnGetVehicleInfo);

			// Get ped info by its network ID
			EventHandlers["AdminManager:Ped:GetInfo"] += new Action<Player, int, float>(_pedService.OnGetPedInfo);

			Debug.WriteLine("[AdminManager] ServerMain initialized.");

		}
	}
}