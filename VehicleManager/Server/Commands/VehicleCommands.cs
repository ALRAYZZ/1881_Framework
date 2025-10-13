using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using VehicleManager.Server.Interfaces;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Server.Commands
{
	// Registers server commands for vehicle management
	public class VehicleCommands
	{
		private readonly IVehicleManager _vehicleManager;
		private readonly PlayerList _players;
		private readonly dynamic _db;

		public VehicleCommands(IVehicleManager vehicleManager, PlayerList players, dynamic db)
		{
			_vehicleManager = vehicleManager;
			_players = players;
			_db = db;

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

			RegisterCommand("park", new Action<int, List<object>, string>((src, args, raw) =>
			{
				var player = _players[src];
				// Request vehicle data from client - client will send it back via event
				player.TriggerEvent("VehicleManager:Client:RequestParkVehicle");
			}), false);

			RegisterCommand("unpark", new Action<int, List<object>, string>((src, args, raw) =>
			{
				var player = _players[src];
				// Request vehicle data from client
				player.TriggerEvent("VehicleManager:Client:RequestUnparkVehicle");
			}), false);

			// Toggle engine state of vehicle being driven
			RegisterCommand("engine", new Action<int, List<object>, string>((src, args, raw) =>
			{
				var player = _players[src];
				player.TriggerEvent("VehicleManager:Client:ToggleEngine");
			}), false);

			RegisterCommand("vehcolor", new Action<int, List<object>, string>((src, args, raw) =>
			{
				var player = _players[src];

				if (args.Count < 2)
				{
					player.TriggerEvent("chat:addMessage", new { args = new[] { "[VehicleManager] Usage: /vehcolor [primaryColor] [secondaryColor]" } });
					return;
				}

				if (!int.TryParse(args[0].ToString(), out int primaryColor) || !int.TryParse(args[1].ToString(), out int secondaryColor))
				{
					player.TriggerEvent("chat:addMessage", new { args = new[] { "[VehicleManager] Color values must be integers." } });
					return;
				}

				if (primaryColor < 0 || primaryColor > 160 || secondaryColor < 0 || secondaryColor > 159)
				{
					player.TriggerEvent("chat:addMessage", new { args = new[] { "[VehicleManager] Color values must be between 0 and 160." } });
					return;
				}

				// Trigger client to apply colors and check if world vehicle
				player.TriggerEvent("VehicleManager:Client:RequestColorChange", primaryColor, secondaryColor);
			}), false);
		}


		// Called by server with vehicle data - entity ID is NOT saved to database
		public void SaveVehicleToDatabase(Player player, uint modelHash, string vehicleType, string plate, 
    float x, float y, float z, float heading, float rx, float ry, float rz, int entityId,
    int primaryColor, int secondaryColor, string customPrimaryRGB, string customSecondaryRGB)
		{
			// Build compact JSON
			string J(double v) => v.ToString(CultureInfo.InvariantCulture);
			string positionJson = $"{{\"x\":{J(x)},\"y\":{J(y)},\"z\":{J(z)},\"heading\":{J(heading)}}}";
			string rotationJson = $"{{\"x\":{J(rx)},\"y\":{J(ry)},\"z\":{J(rz)}}}";

			const string sql = @"
				INSERT INTO world_vehicles (model, vehicle_type, plate, position, rotation, props, 
					primary_color, secondary_color, custom_primary_rgb, custom_secondary_rgb)
				VALUES (@model, @vehicle_type, @plate, @position, @rotation, @props,
					@primary_color, @secondary_color, @custom_primary_rgb, @custom_secondary_rgb);";

			var parameters = new Dictionary<string, object>
			{
				["@model"] = modelHash.ToString(),
				["@vehicle_type"] = vehicleType,
				["@plate"] = string.IsNullOrWhiteSpace(plate) ? null : plate,
				["@position"] = positionJson,
				["@rotation"] = rotationJson,
				["@props"] = "{}",
				["@primary_color"] = primaryColor,
				["@secondary_color"] = secondaryColor,
				["@custom_primary_rgb"] = string.IsNullOrEmpty(customPrimaryRGB) ? null : customPrimaryRGB,
				["@custom_secondary_rgb"] = string.IsNullOrEmpty(customSecondaryRGB) ? null : customSecondaryRGB
			};

			_db.Insert(sql, parameters, new Action<dynamic>(newId =>
			{
				player.TriggerEvent("chat:addMessage", new { args = new[] { $"Parked {vehicleType} with colors (ID: {newId})" } });
				Debug.WriteLine($"[VehicleManager] Saved new world vehicle to database (ID: {newId}, Colors: {primaryColor}/{secondaryColor})");
			}));
		}

		// Remove vehicle from wold vehicles database (does not delete physical entity)
		public void RemoveVehicleFromDatabase(Player player, uint modelHash, string plate)
		{
			const string sql = @"
				DELETE FROM world_vehicles
				WHERE model = @model AND plate = @plate
				LIMIT 1;";

			var parameters = new Dictionary<string, object>
			{
				["@model"] = modelHash.ToString(),
				["@plate"] = string.IsNullOrWhiteSpace(plate) ? null : plate
			};

			_db.Query(sql, parameters, new Action<dynamic>(_ =>
			{
				player.TriggerEvent("chat:addMessage", new { args = new[] { $"Unparked vehicle (Plate: {plate})" } });
				Debug.WriteLine($"[VehicleManager] Removed world vehicle from database (Plate: {plate})");
			}));
		}

		// Update vehicle colors in database (called by server event)
		public void UpdateVehicleColors(int dbId, int primaryColor, int secondaryColor)
		{
			const string sql = @"
				UPDATE world_vehicles 
				SET primary_color = @primary_color, 
				    secondary_color = @secondary_color,
				    custom_primary_rgb = NULL,
				    custom_secondary_rgb = NULL
				WHERE id = @id;";

			var parameters = new Dictionary<string, object>
			{
				["@id"] = dbId,
				["@primary_color"] = primaryColor,
				["@secondary_color"] = secondaryColor
			};

			_db.Query(sql, parameters, new Action<dynamic>(_ =>
			{
				Debug.WriteLine($"[VehicleManager] Updated vehicle colors in database (ID: {dbId}, Colors: {primaryColor}/{secondaryColor})");
			}));
		}	

	}
}
