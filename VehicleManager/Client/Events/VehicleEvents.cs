using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VehicleManager.Client.Services;

namespace VehicleManager.Client.Events
{
	public class VehicleEvents
	{
		private readonly VehicleFactory _vehicleFactory;
		private readonly EventHandlerDictionary _eventHandlers;

		public VehicleEvents(VehicleFactory vehicleFactory, EventHandlerDictionary eventHandlers)
		{
			_vehicleFactory = vehicleFactory;
			_eventHandlers = eventHandlers;

			Debug.WriteLine("[VehicleManager] Registering clcient event handlers...");

			// Register server-triggered events
			_eventHandlers["VehicleManager:Client:SpawnVehicle"] += new Action<string>(async (modelName) =>
			{
				Debug.WriteLine($"[VehicleManager] Client received SpawnModel event for model: {modelName}");
				await _vehicleFactory.SpawnVehicleAsync(modelName);
			});

			_eventHandlers["VehicleManager:Client:DeleteNearestVehicle"] += new Action(() =>
			{
				Debug.WriteLine("[VehicleManager] Client received DeleteNearestVehicle event.");
				DeleteNearestVehicle();
			});

			Debug.WriteLine("[VehicleManager] Client event handlers registered.");
		}

		private void DeleteNearestVehicle()
		{
			var ped = Game.PlayerPed;
			var pos = ped.Position;

			int vehicleHandle = CitizenFX.Core.Native.API.GetClosestVehicle(pos.X, pos.Y, pos.Z, 5.0f, 0, 70);

			if (vehicleHandle != 0)
			{
				_vehicleFactory.DeleteVehicle(vehicleHandle);
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Deleted nearest vehicle." } });
			}
			else
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "No vehicle found nearby." } });
			}
		}
	}
}
