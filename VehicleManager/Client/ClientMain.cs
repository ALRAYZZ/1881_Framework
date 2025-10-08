using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using VehicleManager.Client.Events;
using VehicleManager.Client.Services;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Client
{
	public class ClientMain : BaseScript
    {
        private readonly VehicleFactory _vehicleFactory;
        private readonly VehicleEvents _vehicleEvents;

        public ClientMain()
        {
            Debug.WriteLine("[VehicleManager] Client initialized.");

            // Initialize core services
            _vehicleFactory = new VehicleFactory(EventHandlers); // Pass EventHandlers to the factory so we can trigger events from it

			// Register client events, passing the factory
			_vehicleEvents = new VehicleEvents(_vehicleFactory, EventHandlers); // Pass EventHandlers to register event handlers
		}
    }
}