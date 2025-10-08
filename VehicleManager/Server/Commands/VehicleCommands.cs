using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using VehicleManager.Server.Interfaces;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Server.Commands
{
	public class VehicleCommands
	{
		private readonly IVehicleManager _vehicleManager;
		private readonly PlayerList _players;

		public VehicleCommands(IVehicleManager vehicleManager, PlayerList players)
		{
			_vehicleManager = vehicleManager;
			_players = players;


			Debug.WriteLine("[VehicleManager] Registering server commands...");

			RegisterCommand("car", new Action<int, List<object>, string>((src, args, raw) =>
			{
				var player = _players[src];
				var model = args.Count > 0 ? args[0].ToString() : "adder";
				_vehicleManager.RequestSpawnVehicle(player, model);
			}), false);

			RegisterCommand("delcar", new Action<int, List<object>, string>((src, args, raw) =>
			{
				var player = _players[src];
				_vehicleManager.RequestDeleteVehicle(player);
			}), false);
		}
	}
}
