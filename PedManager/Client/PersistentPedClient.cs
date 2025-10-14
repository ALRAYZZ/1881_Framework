using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

namespace PedManager.Client
{
	internal sealed class PersistentPedClient
	{
		private readonly EventHandlerDictionary _eventHandler;
		public PersistentPedClient(EventHandlerDictionary eventHandler)
		{
			_eventHandler = eventHandler;


			_eventHandler["PedManager:Client:LoadPersistentPeds"] += new Action<List<dynamic>>(SpawnPersistentPeds);
		}

		private async void SpawnPersistentPeds(List<dynamic> peds)
		{
			foreach (var pedData in peds)
			{
				int hash = (int)GetHashKey(pedData.Model.ToString());
				RequestModel((uint)hash);
				while (!HasModelLoaded((uint)hash))
				{
					await BaseScript.Delay(1);
				}

				int ped = CreatePed(4, (uint)hash, (float)pedData.X, (float)pedData.Y, (float)pedData.Z, (float)pedData.Heading, true, true);
				SetEntityAsMissionEntity(ped, true, true);
				FreezeEntityPosition(ped, true);
				SetEntityInvincible(ped, true);
			}
		}

	}
}
