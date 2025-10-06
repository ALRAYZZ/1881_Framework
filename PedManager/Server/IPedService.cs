using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace PedManager.Server
{
	public interface IPedService
	{
		// 2 methods to control if we overwrite a ped model or not on the DB so we avoid unnecessary writes, like when logging in
		void SetPedFor(Player target, string modelName);
		void SetPedFor(Player target, string modelName, bool persist);

		// Loads ped from DB and applies it to the player
		void ApplyInitialPedFor(Player player);
	}
}
