using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PedManager.Client
{
	public class PedEvents : BaseScript
	{
		private readonly EventHandlerDictionary _eventHandlers;

		public PedEvents(EventHandlerDictionary eventHandlers)
		{
			_eventHandlers = eventHandlers;

			_eventHandlers["PedManager:Client:GetNearestPed"] += new Action<string>(OnGetNearestPedRequest);
		}

		private void OnGetNearestPedRequest(string replyEvent)
		{
			int ped = FindNearestPed();
			float dist = 0.0f;

			if (ped != 0)
			{
				var playerPed = Game.PlayerPed;
				var pos = playerPed.Position;
				var pedPos = GetEntityCoords(ped, true);
				dist = GetDistanceBetweenCoords(pos.X, pos.Y, pos.Z, pedPos.X, pedPos.Y, pedPos.Z, true);
			}

			TriggerEvent(replyEvent, ped, dist);
		}

		private int FindNearestPed()
		{
			var playerPed = Game.PlayerPed;
			var pos = playerPed.Position;
			float searchRadius = 5.0f;

			int closestPed = 0;
			float closestDistance = searchRadius;

			int currentPed = 0;
			int entityEnum = FindFirstPed(ref currentPed);

			if (entityEnum != 0)
			{
				do
				{
					if (DoesEntityExist(currentPed) && currentPed != playerPed.Handle)
					{
						var pedPos = GetEntityCoords(currentPed, true);
						float distance = GetDistanceBetweenCoords(pos.X, pos.Y, pos.Z, pedPos.X, pedPos.Y, pedPos.Z, true);

						if (distance < closestDistance)
						{
							closestDistance = distance;
							closestPed = currentPed;
						}
					}

					if (!FindNextPed(entityEnum, ref currentPed))
					{
						break;
					}
				}
				while (true);

				EndFindPed(entityEnum);
			}
			return closestPed;
		}
	}
}
