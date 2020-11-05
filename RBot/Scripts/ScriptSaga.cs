using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RBot
{
	public class ScriptSaga : ScriptableObject
	{
		/// <summary>
		/// A list that tracks the current quests requirements.
		/// </summary>
		public List<Items.ItemBase> CurrentRequirements { get; } = new List<Items.ItemBase>();

		/// <summary>
		/// The last map joined
		/// </summary>
		public string LastMap { get; set; }

		/// <summary>
		/// Whether to reject items when using <see cref="Hunter(string, int, int)"/> or <see cref="HuntForItems(string, string[], int[], bool)"/>
		/// </summary>
		public bool RejectElse { get; set; } = true;

		/// <summary>
		/// List holding the actions to be made when running the bot.
		/// </summary>
		public List<Action> ToDo = new List<Action>();
		private string completeMap;

		/// <summary>
		/// Add a quest into the ToDo list with a default template.
		/// </summary>
		/// <param name="id">ID of the quest to be accepted.</param>
		/// <param name="action">What will be done to complete the quest.</param>
		/// <param name="map">The map where it will be completed, if already defined before there is no need to redefine again.</param>
		/// <param name="itemID">The id of the item chosen when the quest is turned in.</param>
		/// <remarks>Be sure to create an option called "lastMap" to track progress.</remarks>
		public void AddQuest(int id = 0, Action action = null, string map = null, int itemID = -1)
		{
			ToDo.Add(() =>
			{
				if (!string.IsNullOrEmpty(map))
				{
					completeMap = LastMap = map;
					Bot.Config.Set("lastMap", completeMap);
					Bot.Config.Save();
				}
				if(id != 0)
					EnsureAccept(id);
				action?.Invoke();
				if (!string.IsNullOrEmpty(completeMap))
					MapConfirm(completeMap);
				if (!Bot.Wait.ForQuestComplete(id, 2000) && id != 0)
					EnsureComplete(id, itemID);
			});
		}

		public void RunBot(int startIndex = 0)
		{
			if(ToDo.Count <= 0)
			{
				Log("To Do list empty");
			}
			if (startIndex > ToDo.Count)
			{
				Log("Start index out of range, stoping bot");
				return;
			}
			if (!Bot.Player.Loaded)
			{
				Log("Login first");
				return;
			}

			Log("Bot initialized");
			Bot.Skills.StartTimer();
			for (int i = startIndex; i < ToDo.Count; i++)
			{
				Bot.Config.Set("startIndex", i);
				Bot.Config.Save();
				Log($"[START INDEX {i}]");
				ToDo[i]?.Invoke();
				CurrentRequirements.Clear();
				Log($"[END INDEX {i}]");
				if (Bot.Player.Mana < 40 || Bot.Player.Health < Bot.Player.MaxHealth * 0.5)
					Bot.Player.Rest(true);
			}
			Bot.Skills.StopTimer();
			Log("Bot finished");
		}

		/// <summary>
		/// Accepts the specified quest and adds it's requirements to <see cref="CurrentRequirements"/>.
		/// </summary>
		/// <param name="id">The id of the quest.</param>
		public void Accept(int id)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForActionCooldown(ScriptWait.GameActions.AcceptQuest);
            Bot.CallGameFunction("world.acceptQuest", id);
            Bot.Quests.TryGetQuest(id, out Quests.Quest quest);
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForQuestAccept(id);
            CurrentRequirements.AddRange(quest.Requirements);
        }

        /// <summary>
        /// Tries to accept a quest until it is successfully accepted and adds it's requirements to <see cref="CurrentRequirements"/>.
        /// </summary>
        /// <param name="id">The id of the quest.</param>
        /// <param name="tries">The maximum number of tries before giving up.</param>
        public bool EnsureAccept(int id, int tries = 100)
        {
            int tried = 0;
            while (!Bot.Quests.IsInProgress(id) && tried++ < tries)
                Accept(id);
            return !Bot.Quests.IsInProgress(id);
        }

        /// <summary>
        /// Attempts to turn in the specified quest.
        /// </summary>
        /// <param name="id">The id of the quest.</param>
        /// <param name="itemId">The id of the item chosen when the quest is turned in.</param>
        /// <param name="special">Determines whether the quest is marked 'special' or not.</param>
        /// <remarks>The itemId parameter can be used to acquire a particular item when there is a choice of rewards from the quest. For example, in the Voucher Item: Totem of Nulgath quest, you are given the choice of getting a Totem of Nulgath or 10 Gems of Nulgath.</remarks>
        public void Complete(int id, int itemId = -1, bool special = false)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForActionCooldown(ScriptWait.GameActions.TryQuestComplete);
            if (Bot.Options.ExitCombatBeforeQuest && Bot.Player.InCombat)
                Bot.Player.Jump(Bot.Player.Cell, Bot.Player.Pad);
            Bot.Quests.TryGetQuest(id, out Quests.Quest quest);
            if(CurrentRequirements.Any())
                CurrentRequirements.RemoveAll(req => quest.Requirements.Contains(req));
            ScriptInterface.Instance.CallGameFunction("world.tryQuestComplete", id, itemId, special);
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForQuestComplete(id);
        }

        /// <summary>
        /// Tries to turn in the specified quest until it is successfully turned in (no longer in progress).
        /// </summary>
        /// <param name="id">The id of the quest.</param>
        /// <param name="itemId">The id of the item chosen when the quest is turned in.</param>
        /// <param name="special">Determines whether the quest is marked 'special' or not.</param>
        /// <param name="tries">The maximum number of tries before giving up.</param>
        public bool EnsureComplete(int id, int itemId = -1, bool special = false, int tries = 100)
        {
            if (Bot.Options.ExitCombatBeforeQuest)
                Bot.Player.Jump(Bot.Player.Cell, Bot.Player.Pad);
            int tried = 0;
            do
            {
                Complete(id, itemId, special);
            }
            while (Bot.Quests.IsInProgress(id) && tried++ < tries);
            return Bot.Quests.IsInProgress(id);
        }

		/// <summary>
		/// Hunt a monster till it gets ANY item present in <see cref="CurrentRequirements"/>, change <paramref name="itemQuant"/> if you need more than 1 item
		/// </summary>
		/// <param name="monster">The monster to hunt.</param>
		/// <param name="itemQuant">How many items it should get (0 = any quantity, usually 1 depending on drop chance)</param>
		/// <param name="tries">How many times it should kill the monster.</param>
		public void Hunter(string monster, int itemQuant = 0, int tries = 10)
		{
			if (!Bot.Monsters.MapMonsters.Exists(m => m.Name.ToLower() == monster.ToLower()))
			{
				Log($"[!!!] {monster} not found in {Bot.Map.Name} map");
				return;
			}

			bool shouldContinue = true;
			int tried = 0;
			do
			{
				AliveCheck();
				Log($"({tries}) Hunting: {monster}");
				Bot.Player.Hunt(monster);
				if (CurrentRequirements.Any())
				{
					List<string> tempItems = new List<string>();
					List<int> tempQuants = new List<int>();
					List<string> items = new List<string>();
					List<int> quants = new List<int>();
					foreach (var reqItem in CurrentRequirements)
					{
						if (Bot.Player.DropExists(reqItem.Name)
							|| ((HasItem(reqItem.Name) && !HasItem(reqItem.Name, reqItem.Quantity) || (HasItem(reqItem.Name) && reqItem.Quantity == 1)) && itemQuant == 0)
							|| (itemQuant != 0 && HasItem(reqItem.Name)))
						{
							switch (reqItem.Temp)
							{
								case true:
									tempQuants.Add(reqItem.Quantity);
									tempItems.Add(reqItem.Name);
									break;
								case false:
									Bot.Player.Pickup(reqItem.Name);
									quants.Add(reqItem.Quantity);
									items.Add(reqItem.Name);
									break;
							}
						}
					}
					AliveCheck();

					if (((itemQuant != 0 && items.Count + tempItems.Count != itemQuant)
						|| (items.Count < 1 && tempItems.Count < 1))
						&& tried < tries)
						continue;

					if (items.Count > 0)
					{
						HuntForItems(monster, items.ToArray(), quants.ToArray(), false);
						shouldContinue = false;
					}
					if (tempItems.Count > 0)
					{
						HuntForItems(monster, tempItems.ToArray(), tempQuants.ToArray(), true);
						shouldContinue = false;
					}
				}
			}
			while (shouldContinue && tried++ < tries);
			if (shouldContinue)
				Log("[!!!] Max tries reached without success");
		}

		/// <summary>
		/// Checks if the player has the specified item in any inventory.
		/// </summary>
		/// <param name="name">Name of the item.</param>
		/// <param name="quant">Quantity to look for.</param>
		/// <returns>Whether the item exists in any inventory</returns>
		public bool HasItem(string name, int quant = 1)
		{
			if (Bot.Bank.Contains(name))
				Bot.Bank.ToInventory(name);

			if (Bot.Inventory.Contains(name, quant) || Bot.Inventory.ContainsTempItem(name, quant))
				return true;

			return false;
		}

		/// <summary>
		/// Confirms that the player is alive, if not will wait till it respawns.
		/// </summary>
		public void AliveCheck()
		{
			if (Bot.Player.Alive)
				return;

			Log("Player dead");
			while (!Bot.Player.Alive)
				Bot.Sleep(2000);
		}

		/// <summary>
		/// Remove the items from the <see cref="CurrentRequirements"/> and Hunt the monster for the specified items
		/// </summary>
		/// <param name="monster">Monster to hunt.</param>
		/// <param name="items">Array of items to hunt for.</param>
		/// <param name="quants">Quantity of the items.</param>
		/// <param name="isTemp">Whether the items are temporary.</param>
		public void HuntForItems(string monster, string[] items, int[] quants, bool isTemp = false)
		{
			if ((items.Length == 1 && HasItem(items[0], quants[0]))
				|| items.Length != quants.Length)
				return;

			CurrentRequirements?.RemoveAll(i => items.Contains(i.Name));
			Log($"Hunting {monster} for{(isTemp == true ? " [Temp]" : "")} {string.Join(", ", items)}");
			Bot.Player.HuntForItems(monster, items, quants, isTemp, RejectElse);
		}

		/// <summary>
		/// Join and wait for the map to be loaded.
		/// </summary>
		/// <param name="map">Name of the map to join.</param>
		public void MapConfirm(string map)
		{
			if (Bot.Map.Name.Equals(map, StringComparison.OrdinalIgnoreCase))
			{
				LastMap = map;
				Bot.Player.Join(map);
				Bot.Wait.ForMapLoad(map);
			}
		}

		/// <summary>
		/// Jumps to the specified cell and set the spawn point.
		/// </summary>
		/// <param name="cell">The cell to jump to.</param>
		/// <param name="pad">The pad to jump to.</param>
		public void Jump(string cell = "Enter", string pad = "Spawn")
		{
			if (Bot.Player.Cell != cell)
			{
				Bot.Player.Jump(cell, pad);
				Bot.Player.SetSpawnPoint(cell, pad);
			}
		}

		/// <summary>
		/// Sends a getMapItem packet for the specified item.
		/// </summary>
		/// <param name="itemID">The id of the item.</param>
		/// <param name="quant">The quantity to obtain.</param>
		public void GetMapItem(int itemID, int quant = 1)
		{
			if (itemID < 1)
				return;

			if (quant == 1)
				Bot.Map.GetMapItem(itemID);
			else
			{
				for (int i = 0; i < quant; i++)
					Bot.Map.GetMapItem(itemID);
			}
			Log($"Acquired: {itemID} ({quant})");
		}

		/// <summary>
		/// Logs a line of text to the script log.
		/// </summary>
		/// <param name="info">Message to log</param>
		/// <param name="caller">Method who called this.</param>
		public void Log(string info, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        => Bot.Log($"[ {System.Text.RegularExpressions.Regex.Replace(caller, "[A-Z]", " $0").Trim()} ] - {info}.");
    }
}
