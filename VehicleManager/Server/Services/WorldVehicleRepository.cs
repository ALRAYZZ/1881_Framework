using System;
using System.Collections.Generic;
using System.Globalization;
using CitizenFX.Core;
using VehicleManager.Server.Models;

namespace VehicleManager.Server.Services
{
	/// <summary>
	/// Handles all database operations for world vehicles
	/// </summary>
	public class WorldVehicleRepository
	{
		private readonly dynamic _db;

		public WorldVehicleRepository(dynamic db)
		{
			_db = db ?? throw new ArgumentNullException(nameof(db));
		}

		public void GetAllWorldVehicles(Action<List<WorldVehicleData>> callback)
		{
			const string sql = @"
				SELECT
					id,
					model,
					vehicle_type,
					plate,
					primary_color,
					secondary_color,
					custom_primary_rgb,
					custom_secondary_rgb,
					CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.x')) AS DOUBLE) AS x,
					CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.y')) AS DOUBLE) AS y,
					CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.z')) AS DOUBLE) AS z,
					CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.heading')) AS DOUBLE) AS heading,
					JSON_UNQUOTE(JSON_EXTRACT(vehicle_data, '$.engine_on')) AS engine_on
				FROM world_vehicles;";

			_db.Query(sql, new Dictionary<string, object>(), new Action<dynamic>(rows =>
			{
				var vehicles = new List<WorldVehicleData>();

				foreach (var row in rows)
				{
					try
					{
						vehicles.Add(ParseWorldVehicleData(row));
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"[WorldVehicleRepository] Error parsing vehicle data: {ex.Message}");
					}
				}

				callback?.Invoke(vehicles);
			}));
		}

		public void GetVehicleByModelAndPlate(uint modelHash, string plate, Action<WorldVehicleData> callback)
		{
			const string sql = @"
				SELECT 
					id,
					model,
					vehicle_type,
					JSON_UNQUOTE(JSON_EXTRACT(vehicle_data, '$.engine_on')) AS engine_on
				FROM world_vehicles 
				WHERE model = @model AND plate = @plate
				ORDER BY id DESC LIMIT 1;";

			var parameters = new Dictionary<string, object>
			{
				["@model"] = modelHash.ToString(),
				["@plate"] = plate
			};

			_db.Query(sql, parameters, new Action<dynamic>(rows =>
			{
				WorldVehicleData vehicle = null;

				if (rows != null)
				{
					dynamic firstRow = GetFirstRow(rows);
					if (firstRow != null)
					{
						vehicle = new WorldVehicleData
						{
							EntityId = Convert.ToInt32(firstRow.id),
							ModelHash = modelHash,
							VehicleType = firstRow.vehicle_type ?? "automobile",
							EngineOn = ParseJsonBoolean(firstRow.engine_on)
						};
					}
				}

				callback?.Invoke(vehicle);
			}));
		}

		public void UpdateVehiclePosition(int dbId, float x, float y, float z, float heading,
			float rx, float ry, float rz, int primaryColor, int secondaryColor,
			string customPrimaryRGB, string customSecondaryRGB)
		{
			string positionJson = BuildPositionJson(x, y, z, heading);
			string rotationJson = BuildRotationJson(rx, ry, rz);

			const string sql = @"
				UPDATE world_vehicles 
				SET position = @position, 
					rotation = @rotation,
					primary_color = @primary_color, 
					secondary_color = @secondary_color,
					custom_primary_rgb = @custom_primary_rgb, 
					custom_secondary_rgb = @custom_secondary_rgb
				WHERE id = @id;";

			var parameters = new Dictionary<string, object>
			{
				["@id"] = dbId,
				["@position"] = positionJson,
				["@rotation"] = rotationJson,
				["@primary_color"] = primaryColor,
				["@secondary_color"] = secondaryColor,
				["@custom_primary_rgb"] = string.IsNullOrEmpty(customPrimaryRGB) ? null : customPrimaryRGB,
				["@custom_secondary_rgb"] = string.IsNullOrEmpty(customSecondaryRGB) ? null : customSecondaryRGB
			};

			_db.Query(sql, parameters, new Action<dynamic>(_ =>
			{
				Debug.WriteLine($"[WorldVehicleRepository] Updated position and colors for vehicle DB ID {dbId}");
			}));
		}

		public void UpdateEngineState(int dbId, bool engineOn)
		{
			const string sql = @"
				UPDATE world_vehicles
				SET vehicle_data = JSON_SET(COALESCE(vehicle_data, JSON_OBJECT()), '$.engine_on', CAST(@engine_on AS JSON))
				WHERE id = @id;";

			var parameters = new Dictionary<string, object>
			{
				["@id"] = dbId,
				["@engine_on"] = engineOn ? "true" : "false"
			};

			_db.Query(sql, parameters, new Action<dynamic>(_ =>
			{
				Debug.WriteLine($"[WorldVehicleRepository] Persisted engine_on={engineOn} for DB ID {dbId}");
			}));
		}

		public void EnsureVehicleDataExists(int dbId, bool defaultEngineOn)
		{
			const string sql = @"
				UPDATE world_vehicles
				SET vehicle_data = JSON_SET(
					COALESCE(vehicle_data, JSON_OBJECT()),
					'$.engine_on',
					COALESCE(JSON_EXTRACT(vehicle_data, '$.engine_on'), CAST(@engine_on AS JSON))
				)
				WHERE id = @id;";

			var parameters = new Dictionary<string, object>
			{
				["@id"] = dbId,
				["@engine_on"] = defaultEngineOn ? "true" : "false"
			};

			_db.Query(sql, parameters, new Action<dynamic>(_ =>
			{
				Debug.WriteLine($"[WorldVehicleRepository] Ensured vehicle_data JSON for DB ID {dbId}");
			}));
		}

		private WorldVehicleData ParseWorldVehicleData(dynamic row)
		{
			string modelStr = row.model;
			uint modelHash;
			if (!uint.TryParse(modelStr, out modelHash))
			{
				modelHash = (uint)CitizenFX.Core.Native.API.GetHashKey(modelStr);
			}

			return new WorldVehicleData
			{
				EntityId = Convert.ToInt32(row.id),
				ModelHash = modelHash,
				VehicleType = row.vehicle_type ?? "automobile",
				Plate = row.plate,
				X = Convert.ToSingle(row.x, CultureInfo.InvariantCulture),
				Y = Convert.ToSingle(row.y, CultureInfo.InvariantCulture),
				Z = Convert.ToSingle(row.z, CultureInfo.InvariantCulture),
				Heading = Convert.ToSingle(row.heading, CultureInfo.InvariantCulture),
				PrimaryColor = row.primary_color != null ? Convert.ToInt32(row.primary_color) : 0,
				SecondaryColor = row.secondary_color != null ? Convert.ToInt32(row.secondary_color) : 0,
				CustomPrimaryRGB = row.custom_primary_rgb,
				CustomSecondaryRGB = row.custom_secondary_rgb,
				EngineOn = ParseJsonBoolean(row.engine_on)
			};
		}

		private string BuildPositionJson(float x, float y, float z, float heading)
		{
			string J(double v) => v.ToString(CultureInfo.InvariantCulture);
			return $"{{\"x\":{J(x)},\"y\":{J(y)},\"z\":{J(z)},\"heading\":{J(heading)}}}";
		}

		private string BuildRotationJson(float rx, float ry, float rz)
		{
			string J(double v) => v.ToString(CultureInfo.InvariantCulture);
			return $"{{\"x\":{J(rx)},\"y\":{J(ry)},\"z\":{J(rz)}}}";
		}

		private dynamic GetFirstRow(dynamic rows)
		{
			if (rows is System.Collections.IEnumerable enumerable)
			{
				var enumerator = enumerable.GetEnumerator();
				if (enumerator.MoveNext())
				{
					return enumerator.Current;
				}
			}
			else
			{
				return rows;
			}
			return null;
		}

		private static bool ParseJsonBoolean(object raw)
		{
			if (raw == null) return false;
			var s = Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim();
			if (string.IsNullOrEmpty(s)) return false;
			s = s.Trim('"');
			if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
			if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
			if (int.TryParse(s, out var n)) return n != 0;
			return false;
		}
	}
}