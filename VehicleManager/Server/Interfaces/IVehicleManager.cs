using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using VehicleManager.Server.Models;

namespace VehicleManager.Server.Interfaces
{
	public interface IVehicleManager
	{
		void RequestSpawnVehicle(Player player, string modelName);
		void RequestDeleteVehicle(Player player);
	}
}
