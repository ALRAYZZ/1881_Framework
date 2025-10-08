using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Client.Services
{
	public class VehicleFactory
	{
		private readonly EventHandlerDictionary _eventHandlers;
		public VehicleFactory(EventHandlerDictionary eventHandlers)
		{
			_eventHandlers = eventHandlers;
		}

		public async Task<int> SpawnVehicleAsync(string modelName)
		{
			uint hash = (uint)GetHashKey(modelName);

			if (!IsModelInCdimage(hash) || !IsModelAVehicle(hash))
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Invalid vehicle model." } });
				return 0;
			}

			RequestModel(hash);
			while (!HasModelLoaded(hash))
			{
				await BaseScript.Delay(100);
			}

			var ped = Game.PlayerPed;
			var pos = ped.Position + ped.ForwardVector * 5f;
			var heading = ped.Heading;

			int veh = CreateVehicle(hash, pos.X, pos.Y, pos.Z, heading, true, false);
			SetPedIntoVehicle(ped.Handle, veh, -1);
			SetVehicleNumberPlateText(veh, "VehicleManager");
			SetEntityAsMissionEntity(veh, true, true);
			SetModelAsNoLongerNeeded(hash);

			BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { $"Spawned vehicle: {modelName}" } });
			return veh;
		}

		public void DeleteVehicle(int vehicleHandle)
		{
			if (vehicleHandle == 0) return;

			DeleteEntity(ref vehicleHandle);
		}
	}
}
