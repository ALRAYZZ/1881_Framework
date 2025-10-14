using CitizenFX.Core;
using PedManager.Server.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace PedManager.Server.Services
{
	internal sealed class PersistentPedService
	{
		private readonly EventHandlerDictionary _eventHandler;
		private readonly dynamic _db;
		private List<PersistentPed> persistentPeds = new List<PersistentPed>();

		public PersistentPedService(dynamic db, EventHandlerDictionary eventHandler)
		{
			_eventHandler = eventHandler;
			_db = db;
			_eventHandler["OnResourceStart"] += new Action<string>(OnResourceStart);
			_eventHandler["playerConnecting"] += new Action<Player, string, dynamic, dynamic>(OnPlayerConnecting);
		}

		private void OnResourceStart(string resourceName)
		{
			if (GetCurrentResourceName() != resourceName) return;
			// Load persistent peds from database
			LoadPersistentPeds();
		}

		private void LoadPersistentPeds()
		{
			var query = "SELECT * FROM persistent_peds";
			var parameters = new Dictionary<string, object>();

			_db.Query(query, parameters, new CallbackDelegate(result =>
			{
				persistentPeds.Clear();

				if (result == null)
				{
					Debug.WriteLine("[PedManager] No persistent peds found in database.");
					return null;
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
					Debug.WriteLine($"[PersistentPedService] Loaded {persistentPeds.Count} persistent peds.");
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[PedManager] Error loading persistent peds: {ex.Message}");
				}

				return null;
			}));
		}

		private void OnPlayerConnecting([FromSource] Player player, string playerName, dynamic setKickReason, dynamic deferrals)
		{
			player.TriggerEvent("PedManager:Client:LoadPersistentPeds", persistentPeds);
		}
	}
}
