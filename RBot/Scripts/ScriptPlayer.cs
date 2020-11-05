using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Dynamic;

using RBot.Flash;
using RBot.Monsters;
using RBot.Skills;
using RBot.Factions;
using RBot.Servers;
using RBot.Utils;
using RBot.Items;

namespace RBot
{
    public class ScriptPlayer : ScriptableObject
    {
        /// <summary>
        /// Gets the player ID (useful for some packets).
        /// </summary>
        [ObjectBinding("world.myAvatar.uid")]
        public int ID { get; }
        /// <summary>
        /// Gets the player's current XP.
        /// </summary>
        [ObjectBinding("world.myAvatar.objData.intExp")]
        public int XP { get; }
        /// <summary>
        /// Gets the player's required XP to level up.
        /// </summary>
        [ObjectBinding("world.myAvatar.objData.intExpToLevel")]
        public int RequiredXP { get; }
        /// <summary>
        /// The current cell that the player is in.
        /// </summary>
        [ObjectBinding("world.strFrame")]
        public string Cell { get; }
        /// <summary>
        /// The current pad that the player spawned from.
        /// </summary>
        [ObjectBinding("world.strPad")]
        public string Pad { get; }
        /// <summary>
        /// The server to which the player is currently connected.
        /// </summary>
        [ObjectBinding("serverIP", Static = true)]
        public string ServerIP { get; }
        /// <summary>
        /// Checks whether the player is both logged in and alive.
        /// </summary>
        public bool Playing => LoggedIn && Alive;
        /// <summary>
        /// Checks whether the player is logged in.
        /// </summary>
        [CallBinding("isLoggedIn")]
        public bool LoggedIn { get; }
        /// <summary>
        /// Gets the player's username.
        /// </summary>
        [ObjectBinding("loginInfo.strUsername", Static = true)]
        public string Username { get; }
        /// <summary>
        /// Gets the player's password.
        /// </summary>
        /// <remarks>This currently does not always work.</remarks>
        [ObjectBinding("loginInfo.strPassword", Static = true)]
        public string Password { get; }
        /// <summary>
        /// Gets whether the player was kicked from the server.
        /// </summary>
        [CallBinding("isKicked")]
        public bool Kicked { get; }
        /// <summary>
        /// Gets the player's state.
        /// </summary>
        [ObjectBinding("world.myAvatar.dataLeaf.intState", RequireNotNull = "world.myAvatar", DefaultValue = 0)]
        public int State { get; }
        /// <summary>
        /// Checks if the player is in combat.
        /// </summary>
        public bool InCombat => State == 2;
        /// <summary>
        /// Checks if the player is a member (upgrade).
        /// </summary>
        public bool IsMember => Bot.GetGameObject<int>("world.myAvatar.objData.iUpgDays") >= 0;
        /// <summary>
        /// Checks whether the player is alive or not.
        /// </summary>
        public bool Alive => State > 0;
        /// <summary>
        /// Gets the player's current health.
        /// </summary>
        [ObjectBinding("world.myAvatar.dataLeaf.intHP")]
        public int Health { get; }
        /// <summary>
        /// Gets the player's maximum health.
        /// </summary>
        [ObjectBinding("world.myAvatar.dataLeaf.intHPMax")]
        public int MaxHealth { get; }
        /// <summary>
        /// Gets or sets the player's current mana.
        /// </summary>
        [ObjectBinding("world.myAvatar.objData.intMP", "world.myAvatar.dataLeaf.intMP")]
        public int Mana { get; set; }
        /// <summary>
        /// Gets the player's maximum mana.
        /// </summary>
        [ObjectBinding("world.myAvatar.dataLeaf.intMPMax")]
        public int MaxMana { get; }
        /// <summary>
        /// Gets the player's level.
        /// </summary>
        [ObjectBinding("world.myAvatar.dataLeaf.intLevel")]
        public int Level { get; }
        /// <summary>
        /// Gets the player's gold.
        /// </summary>
        [ObjectBinding("world.myAvatar.objData.intGold")]
        public int Gold { get; }
        /// <summary>
        /// Gets the player's current class rank.
        /// </summary>
        [ObjectBinding("world.myAvatar.objData.iRank")]
        public int Rank { get; }
        /// <summary>
        /// Checks if the player currently has a target selected.
        /// </summary>
        public bool HasTarget
        {
            get
            {
                Monster m = Target;
                return m?.Alive ?? false;
            }
        }
        /// <summary>
        /// Checks whether the player's avatar is loaded.
        /// </summary>
        public bool Loaded => Bot.GetGameObject<int>("world.myAvatar.items.length") > 0
                            && !Bot.GetGameObject<bool>("world.mapLoadInProgress")
                            && Bot.CallGameFunction<bool>("world.myAvatar.pMC.artLoaded");
        /// <summary>
        /// The player's access level.
        /// </summary>
        [ObjectBinding("world.myAvatar.objData.intAccessLevel")]
        public int AccessLevel { get; set; }
        /// <summary>
        /// Gets/sets whether the player is upgrade or not.
        /// </summary>
        public bool Upgrade
        {
            get
            {
                return Bot.GetGameObject<int>("world.myAvatar.objData.iUpgDays") > 0;
            }
            set
            {
                Bot.SetGameObject("world.myAvatar.objData.iUpg", value ? 1000 : 0);
                Bot.SetGameObject("world.myAvatar.objData.iUpgDays", value ? 1000 : 0);
            }
        }
        /// <summary>
        /// Gets an array containing information about the player's current skills.
        /// </summary>
        [ObjectBinding("world.actions.active")]
        public SkillInfo[] Skills { get; }
        /// <summary>
        /// Checks whether the player is marked as AFK or not.
        /// </summary>
        [ObjectBinding("world.myAvatar.dataLeaf.afk")]
        public bool AFK { get; }
        /// <summary>
        /// The current position of the player.
        /// </summary>
        public PointF Position => new PointF(X, Y);
        /// <summary>
        /// The player's current X coordinate.
        /// </summary>
        [ObjectBinding("world.myAvatar.pMC.x")]
        public float X { get; }
        /// <summary>
        /// The player's current Y coordinate.
        /// </summary>
        [ObjectBinding("world.myAvatar.pMC.y")]
        public float Y { get; }
        /// <summary>
        /// Gets or sets the walking speed of the player. The default value is 8.
        /// </summary>
        [ObjectBinding("world.WALKSPEED")]
        public int WalkSpeed { get; set; }
        /// <summary>
        /// This does nothing at the moment...
        /// </summary>
        [ObjectBinding("world.SCALE")]
        public int Scale { get; set; }
        /// <summary>
        /// The currently targeted monster. If no monster is targeted, null is returned.
        /// </summary>
        [ObjectBinding("world.myAvatar.target.objData", RequireNotNull = "world.myAvatar.target")]
        public Monster Target { get; }
        /// <summary>
        /// Gets an array containing all the names of the factions that the player has some reputation in.
        /// </summary>
        [ObjectBinding("world.myAvatar.factions")]
        public List<Faction> Factions { get; }
        [CallBinding("getDrops", Json = true)]
        private List<DropInfo> _currentDrops { get; }
        /// <summary>
        /// Gets a list of item names currently on the drop stack.
        /// </summary>
        public List<string> CurrentDrops => _currentDrops.Select(x => x.Name.Trim()).Distinct().ToList();
        /// <summary>
        /// Gets a list of drops available with their counts.
        /// </summary>
        public List<DropInfo> CurrentDropInfos => _currentDrops;

        /// <summary>
        /// Checks if the given skill has cooled down.
        /// </summary>
        /// <param name="index">The index of the skill to check.</param>
        /// <returns>Whether the given skill has cooled down.</returns>
        [MethodCallBinding("canUseSkill")]
        public bool CanUseSkill(int index) => false;

        /// <summary>
        /// Picks up the specified list of items.
        /// </summary>
        /// <param name="items">The names of the items to pick up.</param>
        public void Pickup(params string[] items)
        {
            string whitelist = CreateWhitelistString(items);
            List<string> existing = CurrentDrops.FindAll(x => items.Any(i => i.Equals(x, StringComparison.OrdinalIgnoreCase)));
            FlashUtil.Call("pickupDrops", whitelist);
            if (Bot.Options.SafeTimings)
                existing.ForEach(i => Bot.Wait.ForPickup(i));
        }

        /// <summary>
        /// Picks up the specified list of items, without waiting for the items to be picked up. This method disregards the SafeTimings option.
        /// </summary>
        /// <param name="items"></param>
        public void PickupFast(params string[] items)
        {
            FlashUtil.Call("pickupDrops", CreateWhitelistString(items));
        }

        /// <summary>
        /// Rejects all drops except those in the specified list.
        /// </summary>
        /// <param name="items">The list of items to not reject.</param>
        public void RejectExcept(params string[] items)
        {
            string whitelist = CreateWhitelistString(items);
            IEnumerable<string> toRemove = CurrentDrops.Where(x => !items.Any(i => i.Equals(x, StringComparison.OrdinalIgnoreCase)));
            FlashUtil.Call("rejectExcept", whitelist);
            if (Bot.Options.SafeTimings)
                toRemove.ForEach(i => Bot.Wait.ForPickup(i));
        }

        /// <summary>
        /// Rejects all drops except those in the specified list, without waiting for the items to be picked up. This method disregards the SafeTimings option.
        /// </summary>
        /// <param name="items"></param>
        public void RejectExceptFast(params string[] items)
        {
            FlashUtil.Call("rejectExcept", CreateWhitelistString(items));
        }

        /// <summary>
        /// Checks if a drop of the specified item exists.
        /// </summary>
        /// <param name="name">The name of the item.</param>
        /// <returns>Whether a drop of the specified item exists.</returns>
        public bool DropExists(string name) => CurrentDrops.Contains(x => name == "*" || x.Equals(name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Picks up all available drops.
        /// </summary>
        /// <param name="skipWait">Whether the SafeTimings option is ignored.</param>
        public void PickupAll(bool skipWait = false)
        {
            FlashUtil.Call("pickupDrops", "*");
            if (Bot.Options.SafeTimings && !skipWait)
                Bot.Wait.ForPickup("*");
        }

        /// <summary>
        /// Rejects all availble drops.
        /// </summary>
        /// <param name="skipWait">Whether the SafeTimings option is ignored.</param>
        public void RejectAll(bool skipWait = false)
        {
            FlashUtil.Call("rejectExcept", "");
            if (Bot.Options.SafeTimings && !skipWait)
                Bot.Wait.ForPickup("*");
        }

        /// <summary>
        /// Walks the player to the specified x and y coordinates.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="speed">The speed at which to move the player's avatar.</param>
        [MethodCallBinding("world.myAvatar.pMC.walkTo", RunMethodPost = true, GameFunction = true)]
        public void WalkTo(float x, float y, int speed = 8)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForPlayerPosition(x, y);
        }

        /// <summary>
        /// Sets the player's respawn point to the current cell and pad.
        /// </summary>
        public void SetSpawnPoint(string cell, string pad)
        {
            Bot.CallGameFunction("world.setSpawnPoint", cell, pad);
        }

        /// <summary>
        /// Loads the player's bank so it can be used in the script.
        /// </summary>
        public void LoadBank(bool waitForLoad = true)
        {
            Bot.SendPacket($"%xt%zm%loadBank%{Bot.Map.RoomID}%All%");
            if (waitForLoad)
                Bot.Wait.ForBankLoad(20);
        }

        /// <summary>
        /// Logs into the game with the specified username and password.
        /// </summary>
        /// <param name="username">The username to login with.</param>
        /// <param name="password">The password to login with.</param>
        [MethodCallBinding("login", GameFunction = true)]
        public void Login(string username, string password) { }

        /// <summary>
        /// Connects to the game server with the specified name.
        /// </summary>
        /// <param name="serverName">The name of the server to connect to (e.g. Artix)</param>
        public void Connect(string serverName)
        {
            Server s = ServerList.Servers.Find(x => x.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
            if (s != null)
                Connect(s);
        }

        /// <summary>
        /// Connects to the specified game server.
        /// </summary>
        /// <param name="server">The server to connect to.</param>
        public void Connect(Server server)
        {
            ConnectIP(server.IP);
        }

        /// <summary>
        /// Connects to the game server with the specified IP address.
        /// </summary>
        /// <param name="ip">The IP address of the server to connec to.</param>
        [MethodCallBinding("connectTo", GameFunction = true)]
        public void ConnectIP(string ip) { }

        /// <summary>
        /// Logs in and connects to the specified server.
        /// </summary>
        public void Reconnect(string serverName, int loginDelay = 2000)
        {
            Login(Username, Password);
            Thread.Sleep(loginDelay);
            Connect(serverName);
        }

        /// <summary>
        /// Logs out of the game.
        /// </summary>
        [MethodCallBinding("logout", RunMethodPost = true, GameFunction = true)]
        public void Logout()
        {
            Bot.CallGameFunction("gotoAndPlay", "Login");
        }

        /// <summary>
        /// Untargets the player if they are currently targeted.
        /// </summary>
        [MethodCallBinding("untargetSelf")]
        public void UntargetSelf() { }

        /// <summary>
        /// Deselects the currently selected target.
        /// </summary>
        [MethodCallBinding("world.cancelTarget", RunMethodPost = true, GameFunction = true)]
        public void CancelTarget()
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForMonsterDeath();
        }

        /// <summary>
        /// Walks towards (approaches) the currently selected target.
        /// </summary>
        [MethodCallBinding("world.approachTarget", GameFunction = true)]
        public void ApproachTarget() { }

        /// <summary>
        /// Attacks the specified monster.
        /// </summary>
        /// <param name="name">The name of the monster to attack.</param>
        /// <remarks>This will not wait until the monster is killed, but simply select it and start attacking it.</remarks>
        public void Attack(string name)
        {
            Monster mon = Bot.Monsters.CurrentMonsters.Find(m => (name == "*" || m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) && m.Alive);
            if (mon != null)
                Attack(mon);
        }

        /// <summary>
        /// Attacks the specified instance of a monster.
        /// </summary>
        /// <param name="monster">The monster to attack.</param>
        /// <remarks>This will not wait until the monster is killed, but simply select it and start attacking it.</remarks>
        public void Attack(Monster monster)
        {
            Attack(monster.MapID);
        }

        /// <summary>
        /// Attacks the monster with the specified map id.
        /// </summary>
        /// <param name="id">The map id of the monster to attack.</param>
        /// <remarks>This will not wait until the monster is killed, but simply select it and start attacking it.</remarks>
        [MethodCallBinding("attackMonster")]
        public void Attack(int id) { }

        private int _lastHuntTick;
        private int _huntAccum;
        /// <summary>
        /// Looks for the enemy in the current map and kills it. This method disregards ScriptOptions#HuntPriority.
        /// </summary>
        /// <param name="name">The name of the enemy to hunt.</param>
        public void Hunt(string name)
        {
            string[] names = name.Split('|');
            while (true)
            {
                List<string> cells = names.SelectMany(n => Bot.Monsters.GetLivingMonsterCells(n)).Distinct().ToList();
                foreach (string cell in cells)
                {
                    if (!cells.Contains(Bot.Player.Cell))
                    {
                        if (Environment.TickCount - _lastHuntTick < Bot.Options.HuntDelay)
                            Bot.Sleep(Bot.Options.HuntDelay - Environment.TickCount + _lastHuntTick);
                        Jump(cell, "Left");
                        _lastHuntTick = Environment.TickCount;
                    }
                    foreach(string mon in names)
                    {
                        if (Bot.Monsters.Exists(mon))
                        {
                            Kill(mon);
                            return;
                        }
                    }
                    Bot.Sleep(200);
                }
            }
        }

        /// <summary>
        /// Hunts monsters with a priority. If there is no priority, this has the same behaviour as just Hunt.
        /// If a priority is specified, monsters in the map are sorted by the given priority. Once sorted, the
        /// monster in the current cell which best matches the priority is killed. Otherwise, a cell jump is
        /// awaited and done based on ScriptOptions#HuntDelay.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="priority"></param>
        public void HuntWithPriority(string name, HuntPriorities priority)
        {
            if (priority == HuntPriorities.None)
                Hunt(name);
            else
            {
                while (true)
                {
                    string playerCell = Cell;
                    string[] names = name.Split('|').Select(x => x.ToLower()).ToArray();
                    IOrderedEnumerable<Monster> ordered = Bot.Monsters.MapMonsters.OrderBy(x => 0);
                    if (priority.HasFlag(HuntPriorities.HighHP))
                        ordered = ordered.OrderByDescending(x => x.HP);
                    else if (priority.HasFlag(HuntPriorities.LowHP))
                        ordered = ordered.OrderBy(x => x.HP);
                    if (priority.HasFlag(HuntPriorities.Close))
                        ordered = ordered.OrderBy(x => x.Cell == Cell ? 0 : 1);
                    List<Monster> test = ordered.ToList();
                    List<Monster> targets = ordered.Where(m => names.Any(n => n.Equals(m.Name, StringComparison.OrdinalIgnoreCase)) && m.Alive).ToList();
                    foreach(Monster target in targets)
                    {
                        bool sameCell = target.Cell == Cell;
                        if(sameCell || CanJumpForHunt())
                        {
                            if (!sameCell)
                            {
                                Jump(target.Cell, "Left");
                                _lastHuntTick = Environment.TickCount;
                            }
                            Kill(target);
                            return;
                        }
                    }
                    Bot.Sleep(200);
                }
            }
        }

        /// <summary>
        /// Hunts the specified monster for a specific item.
        /// </summary>
        /// <param name="name">The name of the monster to kill.</param>
        /// <param name="item">The item to collect.</param>
        /// <param name="quantity">The quantity of the item that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItem">Whether or not the item being collected is a temporary (quest) item.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not the 'item' paramater.</param>
        public void HuntForItem(string name, string item, int quantity, bool tempItem = false, bool rejectElse = true)
        {
            while (!ScriptInterface.Instance.ShouldExit()
                && (tempItem || !ScriptInterface.Instance.Inventory.Contains(item, quantity))
                && (!tempItem || !ScriptInterface.Instance.Inventory.ContainsTempItem(item, quantity)))
            {
                HuntWithPriority(name, Bot.Options.HuntPriority);
                _huntAccum++;
                if (_huntAccum >= Bot.Options.HuntBuffer && !tempItem)
                {
                    Pickup(item);
                    if (rejectElse)
                        RejectExcept(item);
                }
            }
        }

        /// <summary>
        /// Hunts the specified monsters for a specific item.
        /// </summary>
        /// <param name="names">The array of names of monsters to kill.</param>
        /// <param name="item">The item to collect.</param>
        /// <param name="quantity">The quantity of the item that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItem">Whether or not the item being collected is a temporary (quest) item.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not the 'item' paramater.</param>
        public void HuntForItem(string[] names, string item, int quantity, bool tempItem = false, bool rejectElse = true)
        {
            HuntForItem(ConvertToNamesString(names), item, quantity, tempItem, rejectElse);
        }

        /// <summary>
        /// Hunts the specified monsters for a specific item.
        /// </summary>
        /// <param name="names">The list of names of monsters to kill.</param>
        /// <param name="item">The item to collect.</param>
        /// <param name="quantity">The quantity of the item that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItem">Whether or not the item being collected is a temporary (quest) item.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not the 'item' paramater.</param>
        public void HuntForItem(List<string> names, string item, int quantity, bool tempItem = false, bool rejectElse = true)
        {
            HuntForItem(ConvertToNamesString(names), item, quantity, tempItem, rejectElse);
        }

        /// <summary>
        /// Hunts the specified monster until the desired items are collected in the desired quantities.
        /// </summary>
        /// <param name="name">The name of the monster to kill.</param>
        /// <param name="items">The items to collect.</param>
        /// <param name="quantities">The quantities of the items that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItems">Whether or not each item being collected is a temporary (quest) item.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not contained in the 'items' array.</param>
        public void HuntForItems(string name, string[] items, int[] quantities, bool[] tempItems, bool rejectElse)
        {
            if (items.Length == quantities.Length)
            {
                bool[] temp = tempItems ?? new bool[tempItems.Length];
                while (!Bot.ShouldExit()
                    && !Enumerable.Range(0, items.Length).All(i => (!temp[i] && Bot.Inventory.Contains(items[i], quantities[i]))
                                                                || (temp[i] && Bot.Inventory.ContainsTempItem(items[i], quantities[i]))))
                {
                    HuntWithPriority(name, Bot.Options.HuntPriority);
                    _huntAccum++;
                    if (_huntAccum >= Bot.Options.HuntBuffer && !temp.All(x => x))
                    {
                        Pickup(items);
                        if (rejectElse)
                            RejectExcept(items);
                        _huntAccum = 0;
                    }
                }
            }
            else
                Bot.Log("Item count does not match quantity count.");
        }

        /// <summary>
        /// Hunts the specified monster until the desired items are collected in the desired quantities.
        /// </summary>
        /// <param name="name">The name of the monster to kill.</param>
        /// <param name="items">The items to collect.</param>
        /// <param name="quantities">The quantities of the items that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItems">Whether or not the items being collected are temporary (quest) items.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not contained in the 'items' array.</param>
        public void HuntForItems(string name, string[] items, int[] quantities, bool tempItems = false, bool rejectElse = true)
        {
            HuntForItems(name, items, quantities, Enumerable.Range(0, items.Length).Select(i => tempItems).ToArray(), rejectElse);
        }

        /// <summary>
        /// Hunts the specified monster until the desired items are collected in the desired quantities.
        /// </summary>
        /// <param name="names">The names of the monsters to kill.</param>
        /// <param name="items">The item to collect.</param>
        /// <param name="quantities">The quantities of the items that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItems">Whether or not each item being collected is a temporary (quest) item.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not contained in the 'items' array.</param>
        public void HuntForItems(string[] names, string[] items, int[] quantities, bool[] tempItems, bool rejectElse)
        {
            HuntForItems(ConvertToNamesString(names), items, quantities, tempItems, rejectElse);
        }

        /// <summary>
        /// Hunts the specified monster until the desired items are collected in the desired quantities.
        /// </summary>
        /// <param name="names">The names of the monsters to kill.</param>
        /// <param name="items">The items to collect.</param>
        /// <param name="quantities">The quantities of the items that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItems">Whether or not the items being collected are temporary (quest) items.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not contained in the 'items' array.</param>
        public void HuntForItems(string[] names, string[] items, int[] quantities, bool tempItems = false, bool rejectElse = true)
        {
            HuntForItems(names, items, quantities, Enumerable.Range(0, items.Length).Select(i => tempItems).ToArray(), rejectElse);
        }

        /// <summary>
        /// Hunts the specified monster until the desired items are collected in the desired quantities.
        /// </summary>
        /// <param name="names">The names of the monsters to kill.</param>
        /// <param name="items">The items to collect.</param>
        /// <param name="quantities">The quantities of the items that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItems">Whether or not each item being collected is a temporary (quest) item.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not contained in the 'items' array.</param>
        public void HuntForItems(List<string> names, string[] items, int[] quantities, bool[] tempItems, bool rejectElse)
        {
            HuntForItems(ConvertToNamesString(names), items, quantities, tempItems, rejectElse);
        }

        /// <summary>
        /// Hunts the specified monster until the desired items are collected in the desired quantities.
        /// </summary>
        /// <param name="names">The names of the monsters to kill.</param>
        /// <param name="items">The items to collect.</param>
        /// <param name="quantities">The quantities of the items that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItems">Whether or not the items being collected are temporary (quest) items.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not contained in the 'items' array.</param>
        public void HuntForItems(List<string> names, string[] items, int[] quantities, bool tempItems = false, bool rejectElse = true)
        {
            HuntForItems(names, items, quantities, Enumerable.Range(0, items.Length).Select(i => tempItems).ToArray(), rejectElse);
        }

        private bool CanJumpForHunt()
        {
            return Environment.TickCount - _lastHuntTick >= Bot.Options.HuntDelay;
        }

        /// <summary>
        /// Attacks the specified monster and waits until it is killed (if SafeTimings are enabled).
        /// </summary>
        /// <param name="name">The name of the monster to kill.</param>
        public void Kill(string name)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForMonsterSpawn(name, 30);
            Attack(name);
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForMonsterDeath();
        }

        /// <summary>
        /// Attacks the specified instance of a monster and waits until it is killed (if SafeTimings are enabled).
        /// </summary>
        /// <param name="monster">The monster to kill.</param>
        public void Kill(Monster monster)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForTrue(() => Bot.Monsters.CurrentMonsters.Contains(m => m.MapID == monster.MapID && m.Alive), 30);
            Attack(monster);
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForMonsterDeath();
        }

        /// <summary>
        /// Kills the specified monster until the desired item is collected in the desired quantity.
        /// </summary>
        /// <param name="name">The name of the monster to kill.</param>
        /// <param name="item">The item to collect.</param>
        /// <param name="quantity">The quantity of the item that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItem">Whether or not the item being collected is a temporary (quest) item.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not the 'item' paramater.</param>
        public void KillForItem(string name, string item, int quantity, bool tempItem = false, bool rejectElse = true)
        {
            while (!Bot.ShouldExit()
                && (tempItem || !Bot.Inventory.Contains(item, quantity))
                && (!tempItem || !Bot.Inventory.ContainsTempItem(item, quantity)))
            {
                Kill(name);
                ScriptInterface.Instance.Player.Pickup(item);
                if (rejectElse)
                    ScriptInterface.Instance.Player.RejectExcept(item);
            }
        }

        /// <summary>
        /// Kills the specified monster until the desired items are collected in the desired quantities.
        /// </summary>
        /// <param name="name">The name of the monster to kill.</param>
        /// <param name="items">The item to collect.</param>
        /// <param name="quantities">The quantities of the items that must be collected before stopping the killing of the monster.</param>
        /// <param name="tempItems">Whether or not the items being collected are temporary (quest) items.</param>
        /// <param name="rejectElse">Whether or not to reject items which are not contained in the 'items' array.</param>
        public void KillForItems(string name, string[] items, int[] quantities, bool tempItems = false, bool rejectElse = true)
        {
            if(items.Length == quantities.Length)
            {
                while(!Bot.ShouldExit()
                    && Enumerable.Range(0, items.Length).All(i => (!tempItems && Bot.Inventory.Contains(items[i], quantities[i])) 
                                                                || (tempItems && Bot.Inventory.ContainsTempItem(items[i], quantities[i]))))
                {
                    Kill(name);
                    ScriptInterface.Instance.Player.Pickup(items);
                    if (rejectElse)
                        ScriptInterface.Instance.Player.RejectExcept(items);
                }
            }
            else
                Bot.Log("Item count does not match quantity count.");
        }

        /// <summary>
        /// Attacks the specified player. If not in PVP mode, this will only target the player, and not attack them.
        /// </summary>
        /// <param name="name">The name of the player to attack.</param>
        [MethodCallBinding("attackPlayer")]
        public void AttackPlayer(string name) { }

        /// <summary>
        /// Attacks the specified player and waits until they are killed (if SafeTiings are enabled). This should only be used in PVP.
        /// </summary>
        /// <param name="name">The name of the player to kill.</param>
        public void KillPlayer(string name)
        {
            AttackPlayer(name);
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForMonsterDeath();
        }

        /// <summary>
        /// Uses the skill with the specified index (1-4).
        /// </summary>
        /// <param name="index">The index of the skill.</param>
        [MethodCallBinding("useSkill")]
        public void UseSkill(int index) { }

        /// <summary>
        /// Jumps the player to the specified cell and pad.
        /// </summary>
        /// <param name="cell">The cell to jump to.</param>
        /// <param name="pad">The pad to jump to.</param>
        /// <param name="clientOnly">If true, the client will not send a moveToCell packet to the server.</param>
        [MethodCallBinding("world.moveToCell", RunMethodPost = true, GameFunction = true)]
        public void Jump(string cell, string pad, bool clientOnly = false)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForCellChange(cell);
        }

        internal string LastJoin { get; set; } = null;
        /// <summary>
        /// Joins the specified map, ignoring whether or not you are in that map.
        /// </summary>
        /// <param name="map">The name of the map.</param>
        public void JoinIgnore(string map)
        {
            Join(map, "Enter", "Spawn", true);
        }

        /// <summary>
        /// Joins the specified map, and jumps to the specified cell and pad.
        /// </summary>
        /// <param name="map">The name of the map.</param>
        /// <param name="cell">The cell to jump to.</param>
        /// <param name="pad">The pad to jump to.</param>
        /// <param name="ignoreCheck">If set to true, the bot will not check if the player is already in the given room.</param>
        public void Join(string map, string cell = "Enter", string pad = "Spawn", bool ignoreCheck = false)
        {
            _Join(map, cell, pad, ignoreCheck);
        }

        private void _Join(string map, string cell = "Enter", string pad = "Spawn", bool ignoreCheck = false)
        {
            LastJoin = map;
            if (ignoreCheck || !Bot.Map.Name.Equals(map, StringComparison.OrdinalIgnoreCase))
            {
                if (Bot.Options.PrivateRooms)
                    map = map.Split('-')[0] + "-1000000";
                if (Bot.Options.SafeTimings)
                    Bot.Wait.ForActionCooldown(ScriptWait.GameActions.Transfer);
                JoinPacket(map, cell, pad);
                if (Bot.Options.SafeTimings)
                {
                    if (!Bot.Wait.ForMapLoad(map, 20))
                    {
                        Jump(Cell, Pad);
                        Thread.Sleep(ScriptWait.WAIT_SLEEP * 10);
                        _Join(map, cell, pad, false);
                    }
                    else
                        Jump(cell, pad);
                }
            }
        }

        internal void JoinPacket(string map, string cell, string pad)
        {
            ScriptInterface.Instance.SendPacket($"%xt%zm%cmd%{Bot.Map.RoomID}%tfer%{Username}%{map}%{cell}%{pad}%");
        }

        /// <summary>
        /// Goes to the specified player (equivilent to using the /goto command).
        /// </summary>
        /// <param name="name">The name of the player.</param>
        [MethodCallBinding("world.goto", GameFunction = true)]
        public void Goto(string name) { }

        /// <summary>
        /// Equips the specified item.
        /// </summary>
        /// <param name="id">The id of the item to equip.</param>
        public void EquipItem(int id)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForActionCooldown(ScriptWait.GameActions.EquipItem);
            dynamic item = new ExpandoObject();
            item.ItemID = id;
            Bot.CallGameFunction("world.sendEquipItemRequest", item);
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForItemEquip(id);
        }

        /// <summary>
        /// Equips the specified item. This will do nothing if the item is not in the player's inventory.
        /// </summary>
        /// <param name="name">The name of the item to equip.</param>
        public void EquipItem(string name)
        {
            if (Bot.Inventory.TryGetItem(name, out InventoryItem item))
                EquipItem(item.ID);
        }

        /// <summary>
        /// Displays the bank to the user.
        /// </summary>
        [MethodCallBinding("world.toggleBank", GameFunction = true)]
        public void OpenBank() { }

        /// <summary>
        /// Rests the player (equivilent to clicking the rest button on the UI).
        /// </summary>
        /// <param name="full">If true, the bot will wait until the player's HP and MP are full.</param>
        public void Rest(bool full = false, int timeout = -1)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForActionCooldown(ScriptWait.GameActions.Rest);
            Bot.CallGameFunction("world.rest");
            if (full)
                Bot.Wait.ForTrue(() => Health >= MaxHealth && Mana >= MaxMana, timeout);
        }

        /// <summary>
        /// Checks if the specified boost is active.
        /// </summary>
        /// <param name="boost">The type of boost to check.</param>
        /// <returns>Whether the specified boost is active or not.</returns>
        public bool IsBoostActive(BoostType boost) => Bot.GetGameObject("world.myAvatar.objData." + _boostMap[boost], 0) > 0;

        /// <summary>
        /// Uses the specified boost.
        /// </summary>
        /// <param name="id">The id of the boost.</param>
        public void UseBoost(int id)
        {
            Bot.SendPacket($"%xt%zm%serverUseItem%{Bot.Map.RoomID}%+%{id}%");
        }

        private string ConvertToNamesString(IEnumerable<string> names)
        {
            return string.Join("|", names);
        }

        private string CreateWhitelistString(string[] items)
        {
            return string.Join(",", items).ToLower();
        }

        private static Dictionary<BoostType, string> _boostMap = new Dictionary<BoostType, string>
        {
            { BoostType.Gold, "iBoostG" },
            { BoostType.Class, "iBoostCP" },
            { BoostType.Reputation, "iBoostRep" },
            { BoostType.Experience, "iBoostXP" }
        };
    }
}
