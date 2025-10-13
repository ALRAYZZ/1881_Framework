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

        public ServerMain()
        {
            _teleportService = new TeleportServerService(this);

            // Teleport an entity by its entity ID
            EventHandlers["AdminManager:Teleport:EntityID"] += new Action<Player, int, float, float, float>(_teleportService.OnTeleportByEntityId);

			// Teleport an entity by its network ID
            EventHandlers["AdminManager:Teleport:NetID"] += new Action<Player, int, float, float, float>(_teleportService.OnTeleportByNetId);
		}
	}
}