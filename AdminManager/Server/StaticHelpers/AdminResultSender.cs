using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace AdminManager.Server.StaticHelpers
{
	internal static class AdminResultSender
	{
		public static void SendResult(Player target, bool success, string message)
		{
			target.TriggerEvent("AdminManager:Teleport:Result", success, message);
		}
	}
}
