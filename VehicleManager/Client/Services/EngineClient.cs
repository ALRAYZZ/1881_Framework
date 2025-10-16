using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Client.Services
{
	public class EngineClient : BaseScript
	{
		// Track desired engine state for world vehicles by NetId
		private readonly Dictionary<int, bool> _engineStateByNetId = new Dictionary<int, bool>();
		// Pending toggles awaiting IsWorldVehicle check
		private readonly Dictionary<int, bool> _pendingPersistByNetId = new Dictionary<int, bool>();

		// Track last car player was in to detect entering'
		private int _lastVehicle = 0;

		public EngineClient()
		{
			// Server side /engine command forwards to this client event
			EventHandlers["VehicleManager:Client:ToggleEngine"] += new Action(OnToggleEngine);

			// Server answers if a NetID is a tracked world vehicle
			EventHandlers["VehicleManager:Client:OnIsWorldVehicleResult"] += new Action<bool, int, int>(OnIsWorldVehicleResult);

			// Server broadcasts enforced engine state for world vehicles
			EventHandlers["VehicleManager:Client:SetWorldVehicleEngineState"] += new Action<int, bool>((vehicleNetId, engineOn) =>
			{
				_engineStateByNetId[vehicleNetId] = engineOn;
				ApplyEngineStateToVehicle(vehicleNetId, engineOn);
			});

			// Periodically enforce states to prevent auto start for parked vehicles
			Tick += EnforceEngineStateTick;

			// Detect when player enters a vehicle to apply stored engine state
			Tick += OnVehicleEnterTick;
		}

		private void OnToggleEngine()
		{
			try
			{
				int ped = PlayerPedId();
				if (!IsPedInAnyVehicle(ped, false))
				{
					ShowMsg("You are not in a vehicle");
					return;
				}

				int veh = GetVehiclePedIsIn(ped, false);
				if (GetPedInVehicleSeat(veh, -1) != ped)
				{
					ShowMsg("You must be the driver to toggle the engine");
					return;
				}

				bool running = GetIsVehicleEngineRunning(veh);
				bool desired = !running;

				// Toggle locally
				SetVehicleEngineOn(veh, desired, true, true);
				SetVehicleUndriveable(veh, !desired);

				int netId = NetworkGetNetworkIdFromEntity(veh);
				if (netId != 0)
				{
					// Remember desired state
					_engineStateByNetId[netId] = desired;

					// Ask server if this is a tracked world vehicle
					_pendingPersistByNetId[netId] = desired;
					TriggerServerEvent("VehicleManager:Server:IsWorldVehicle", netId, "VehicleManager:Client:OnIsWorldVehicleResult");
				}

				ShowMsg($"Engine turned {(desired ? "on" : "off")}");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in OnToggleEngine: {ex}");
			}
		}	

		private void OnIsWorldVehicleResult(bool isWorld, int dbId, int vehicleNetId)
		{
			try
			{
				if (!isWorld)
				{
					_pendingPersistByNetId.Remove(vehicleNetId);
					return;
				}

				if (_pendingPersistByNetId.TryGetValue(vehicleNetId, out bool desired))
				{
					_pendingPersistByNetId.Remove(vehicleNetId);
					// Ask server to persist and broadcast the engine state
					TriggerServerEvent("VehicleManager:Server:UpdateWorldVehicleEngineState", vehicleNetId, desired);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in OnIsWorldVehicleResult: {ex}");
			}
		}

		private async Task EnforceEngineStateTick()
		{
			try
			{
				foreach (var kvp in _engineStateByNetId)
				{
					ApplyEngineStateToVehicle(kvp.Key, kvp.Value);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in EnforceEngineStateTick: {ex}");
			}
			await Delay(500);
		}

		private async Task OnVehicleEnterTick()
		{
			try
			{
				int ped = PlayerPedId();
				int veh = GetVehiclePedIsIn(ped, false);
				if (veh != 0 && veh != _lastVehicle)
				{
					// Just entered a vehicle
					_lastVehicle = veh;
					int netId = NetworkGetNetworkIdFromEntity(veh);
					if (netId != 0 && _engineStateByNetId.ContainsKey(netId))
					{
						// World Vehicle apply state immediately
						bool desired = _engineStateByNetId[netId];
						ApplyEngineStateToVehicle(netId, desired);
					}
				}
				else if (veh == 0)
				{
					_lastVehicle = 0;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in OnVehicleEnterTick: {ex}");
			}
			await Delay(100);
		}

		private void ApplyEngineStateToVehicle(int netId, bool engineOn)
		{
			int entity = NetworkGetEntityFromNetworkId(netId);
			if (entity == 0 || !DoesEntityExist(entity)) return;

			bool running = GetIsVehicleEngineRunning(entity);
			if (running != engineOn)
			{
				SetVehicleEngineOn(entity, engineOn, true, true);
			}

			// Keep undriveable if engine is off
			SetVehicleUndriveable(entity, !engineOn);
		}

		private void ShowMsg(string msg)
		{
			TriggerEvent("chat:addMessage", new { args = new[] { msg } });
		}
	}
}
