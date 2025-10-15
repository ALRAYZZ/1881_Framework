using CitizenFX.Core;
using PedManager.Server.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace PedManager.Server.Services
{
	internal sealed class PersistentPedService
	{
		private readonly EventHandlerDictionary _eventHandler;
		private readonly dynamic _db;
		private readonly BaseScript _baseScript;
		private List<PersistentPed> persistentPeds = new List<PersistentPed>();

		public PersistentPedService(dynamic db, EventHandlerDictionary eventHandler)
		{
			_eventHandler = eventHandler;
			_db = db;
			_eventHandler["onResourceStart"] += new Action<string>(OnResourceStart);
			_eventHandler["PlayerCore:Server:OnSpawned"] += new Action<Player>(OnPlayerPostSpawned);
		}

		private void OnResourceStart(string resourceName)
		{
			Debug.WriteLine($"[PedManager] Resource started: {resourceName} triggered OnResourceStart");
			if (GetCurrentResourceName() != resourceName) return;
			// Load persistent peds from database
			LoadPersistentPeds();

			Debug.WriteLine($"[PedManager] Sent {persistentPeds.Count} persistent peds to all connected players.");
		}

		public void AddPersistentPed(string model, float x, float y, float z, float heading, Action<bool, int> callback)
		{
			var query = "INSERT INTO persistent_peds (model, x, y, z, heading) VALUES (@model, @x, @y, @z, @heading)";
			var parameters = new Dictionary<string, object>
			{
				{ "@model", model },
				{ "@x", x },
				{ "@y", y },
				{ "@z", z },
				{ "@heading", heading }
			};

			_db.Insert(query, parameters, new Action<dynamic>((result) =>
			{
				try
				{
					if (result != null)
					{
						int insertedId = Convert.ToInt32(result);

						// Add to in-memory list
						persistentPeds.Add(new PersistentPed
						{
							Id = insertedId,
							Model = model,
							X = x,
							Y = y,
							Z = z,
							Heading = heading
						});

						Debug.WriteLine($"[PedManager] Added persistent ped ID {insertedId} at ({x}, {y}, {z})");
						callback?.Invoke(true, insertedId);
					}
					else
					{
						Debug.WriteLine("[PedManager] Failed to add persistent ped to database.");
						callback?.Invoke(false, 0);
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[PedManager] Error adding persistent ped: {ex.Message}");
					callback?.Invoke(false, 0);
				}
			}));
		}

		private void LoadPersistentPeds()
		{
			var query = "SELECT * FROM persistent_peds";
			var parameters = new Dictionary<string, object>();

			_db.Query(query, parameters, new Action<dynamic>((result) =>
			{
				persistentPeds.Clear();

				if (result == null)
				{
					Debug.WriteLine("[PedManager] No persistent peds found in database.");
					return;
				}

				try
				{
					foreach (dynamic row in result)
					{
						persistentPeds.Add(new PersistentPed
						{
							Id = Convert.ToInt32(row.id),
							Model = Convert.ToString(row.model),
							X = Convert.ToSingle(row.x),
							Y = Convert.ToSingle(row.y),
							Z = Convert.ToSingle(row.z),
							Heading = Convert.ToSingle(row.heading)
						});
					}
					Debug.WriteLine($"[PedManager] Loaded {persistentPeds.Count} persistent peds.");
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[PedManager] Error loading persistent peds: {ex.Message}");
				}
			}));
		}

		public void RemovePersistentPedByNetId(int dbId, Action<bool> callback)
		{
			var ped = persistentPeds.FirstOrDefault(p => p.Id == dbId);
			if (ped == null)
			{
				Debug.WriteLine($"[PedManager] No persistent ped found near DB ID {dbId} to remove.");
				callback?.Invoke(false);
				return;
			}

			// Remove from DB
			var query = "DELETE FROM persistent_peds WHERE id = @id";
			var parameters = new Dictionary<string, object>
			{
				{ "@id", ped.Id }
			};
			_db.Query(query, parameters, new Action<dynamic>((result) =>
			{
				persistentPeds.Remove(ped);
				Debug.WriteLine($"[PedManager] Removed persistent ped ID {ped.Id} from database and memory.");
				callback?.Invoke(true);
			}));
		}

		private void OnPlayerPostSpawned([FromSource] Player player)
		{
			if (player == null) return;

			Debug.WriteLine($"[PedManager] Server: Post-spawn for player {player.Name}");

			BaseScript.Delay(500);

			BaseScript.TriggerClientEvent("PedManager:Client:LoadPersistentPeds", persistentPeds);
			Debug.WriteLine($"[PedManager] Server: Sent {persistentPeds.Count} persistent peds to player {player.Name}");
		}
	}
}
