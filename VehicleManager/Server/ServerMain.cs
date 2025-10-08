using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using VehicleManager.Server.Commands;
using VehicleManager.Server.Interfaces;

namespace VehicleManager.Server
{
    public class ServerMain : BaseScript
    {
        public ServerMain()
        {
            IVehicleManager vehicleManager = new Services.VehicleManager();
            new VehicleCommands(vehicleManager, Players);

            Debug.WriteLine("[VehicleManager] Server initialized.");
        }
    }
}