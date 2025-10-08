using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using VehicleManager.Server.Interfaces;

namespace VehicleManager.Server.Services
{
	// Triggers client events to spawn or delete vehicles
	public class VehicleManager : IVehicleManager
	{
		public void RequestSpawnVehicle(Player player, string modelName)
		{
			player.TriggerEvent("VehicleManager:Client:SpawnVehicle", modelName);
		}

		public void RequestDeleteVehicle(Player player)
		{
			player.TriggerEvent("VehicleManager:Client:DeleteNearestVehicle");
		}
	}
}
