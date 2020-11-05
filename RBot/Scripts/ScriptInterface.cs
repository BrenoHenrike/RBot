using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using AxShockwaveFlashObjects;

using Newtonsoft.Json;

using RBot.Flash;
using RBot.Servers;
using RBot.Utils;
using RBot.GameProxy;
using RBot.Strategy;

namespace RBot
{
    public class ScriptInterface
    {
        private static ScriptInterface _instance;
        public static ScriptInterface Instance => _instance ?? (_instance = new ScriptInterface());

        /// <summary>
        /// An object holding methods for easy saga/quest sequence bots.
        /// </summary>
        public ScriptSaga Saga { get; set; }

        /// <summary>
        /// An object holding options for the current bot.
        /// </summary>
        public ScriptOptions Options { get; set; }
        /// <summary>
        /// /// An object holding a set of methods for waiting for certain events to occur.
        /// </summary>
        public ScriptWait Wait { get; set; }
        /// <summary>
        /// An object holding a set of methods which allow most of the interaction between the script and the game.
        /// </summary>
        public ScriptPlayer Player { get; set; }
        /// <summary>
        /// An object holding a set of methods for accessing information about currently loaded monsters.
        /// </summary>
        public ScriptMonsters Monsters { get; set; }
        /// <summary>
        /// An object holding a set of methods for inventory management.
        /// </summary>
        public ScriptInventory Inventory { get; set; }
        /// <summary>
        /// An object holding a set of methods for bank management.
        /// </summary>
        /// <remarks>It is important to ensure the bank is loaded before trying to check the presence of items or move them between the bank or inventory. This can be done manually or by using <see cref="M:RBot.ScriptPlayer.LoadBank(System.Boolean)" />.</remarks>
        public ScriptBank Bank { get; set; }
        /// <summary>
        /// An object holding a set of methods for getting information about the currently loaded map.
        /// </summary>
        public ScriptMap Map { get; set; }
        /// <summary>
        /// An object holding a set of methods for quest management.
        /// </summary>
        public ScriptQuests Quests { get; set; }
        /// <summary>
        /// An object holding a set of methods for accessing and interacting with shops.
        /// </summary>
        public ScriptShops Shops { get; set; }
        /// <summary>
        /// The skill manager is used to enable skills to be used in combat.
        /// </summary>
        public ScriptSkills Skills { get; set; }
        /// <summary>
        /// An object holding runtime variables for the currently running script. These are cleared when another script is started.
        /// </summary>
        public ScriptRuntimeVars Runtime { get; set; }
        /// <summary>
        /// An object holding stats about the current botting session.
        /// </summary>
        public ScriptBotStats Stats { get; set; }
        /// <summary>
        /// An object holding a set of events which can be listened for.
        /// </summary>
        public ScriptEvents Events { get; set; }
        /// <summary>
        /// This contains options for the currently loaded script.
        /// </summary>
        public ScriptOptionContainer Config { get; set; }
        /// <summary>
		/// The drop grabber can be used to accept/reject drops. It does this on the script timer thread. This is significantly less safe than waiting for the drop to be picked up on the main thread of the running bot.
		/// </summary>
        public ScriptDrops Drops { get; set; }

        /// <summary>
        /// An object used for acquiring items based on information on how to acquire them.
        /// </summary>
        public StrategyDatabase Strategy { get; set; } = new StrategyDatabase();

        /// <summary>
        /// The global packet intercepter instance.
        /// </summary>
        public CaptureProxy GameProxy { get; } = new CaptureProxy();

        /// <summary>
        /// A boolean determining whether the world clip has been loaded yet.
        /// </summary>
        /// <remarks>This can be used as an additional way of checking if the player is logged in and ready to perform actions.</remarks>
        public bool IsWorldLoaded => !IsNull("world");

        /// <summary>
        /// A list of handlers which contain functions to be run on the script timer thread. This list is cleared when the script stops and when a new script is started.
        /// </summary>
        public List<ScriptHandler> Handlers { get; } = new List<ScriptHandler>();

        public static bool exit;

        private volatile bool _appExit;
        private volatile bool _waitForLogin;
        private Thread _timerThread;

        public ScriptInterface()
        {
            Options = new ScriptOptions();
            Wait = new ScriptWait();
            Player = new ScriptPlayer();
            Monsters = new ScriptMonsters();
            Inventory = new ScriptInventory();
            Bank = new ScriptBank();
            Map = new ScriptMap();
            Quests = new ScriptQuests();
            Shops = new ScriptShops();
            Skills = new ScriptSkills();
            Runtime = new ScriptRuntimeVars();
            Stats = new ScriptBotStats();
            Events = new ScriptEvents();
            Config = new ScriptOptionContainer();
            Drops = new ScriptDrops();

            FlashUtil.FlashCall += HandleFlashCall;

            _timerThread = new Thread(_TimerThread) { Name = "Script Timer" };
        }

        /// <summary>
        /// Initializes the ScriptInterface instance and its timer thread.
        /// </summary>
        public void Init()
        {
            _timerThread.Start();
        }

        /// <summary>
        /// Prepares for the application to close.
        /// Do NOT call this from scripts.
        /// </summary>
        public void Exit()
        {
            exit = true;
            _appExit = true;
            _timerThread.Abort();
            ScriptManager.CurrentScriptThread?.Abort();
        }

        /// <summary>
        /// Returns a value determining whether or not the current script should exit.
        /// </summary>
        /// <returns>Whether the current script should exit.</returns>
        public bool ShouldExit()
        {
            return exit;
        }

        /// <summary>
        /// Schedules the specified action to run after the specified delay in ms. This is done using C# async tasks.
        /// </summary>
        /// <param name="delay">The time to delay the action for in milliseconds.</param>
        /// <param name="action">The action to run. This can be passed as a lambda expression.</param>
        public Task Schedule(int delay, Func<ScriptInterface, Task> action)
        {
            return Task.Delay(delay).ContinueWith(t => action(this));
        }

        /// <summary>
        /// Schedules the specified action to run after the specified delay in ms. This is done using C# async tasks.
        /// </summary>
        /// <param name="delay">The time to delay the action for in milliseconds.</param>
        /// <param name="action">The action to run. This can be passed as a lambda expression.</param>
        public Task Schedule(int delay, Action<ScriptInterface> action)
        {
            return Task.Delay(delay).ContinueWith(t => action(this));
        }

        private volatile int _iHandler;
        /// <summary>
        /// Register an action to be executed every time the specified number of ticks has passed. A tick is 20ms.
        /// </summary>
        /// <param name="ticks">The number of ticks between consecutive executions of the action.</param>
        /// <param name="func">The action to carry out. If this function returns false, the handler will not continue to run.</param>
        /// <param name="name">The name of this handler (must be unique). Passing null will assign it a unique name.</param>
        /// <returns>The handler registered.</returns>
        public ScriptHandler RegisterHandler(int ticks, Func<ScriptInterface, bool> func, string name = null)
        {
            ScriptHandler handler = new ScriptHandler()
            {
                Name = name ?? _iHandler++.ToString(),
                Ticks = ticks,
                Function = func
            };
            Handlers.Add(handler);
            return handler;
        }

        /// <summary>
        /// Register an action to be executed every time the specified number of ticks has passed. A tick is 20ms.
        /// </summary>
        /// <param name="ticks">The number of ticks between consecutive executions of the action.</param>
        /// <param name="func">The action to carry out at every interval.</param>
        /// <param name="name">The name of this handler (must be unique). Passing null will assign it a unique name.</param>
        /// <returns>The handler registered.</returns>
        public ScriptHandler RegisterHandler(int ticks, Action<ScriptInterface> func, string name = null)
        {
            return RegisterHandler(ticks, b =>
            {
                func(b);
                return true;
            }, name);
        }

        /// <summary>
        /// Register an action to be executed every time the specified number of ticks has passed. A tick is 20ms.
        /// </summary>
        /// <param name="ticks">The number of ticks between consecutive executions of the action.</param>
        /// <param name="func">The action to carry out once.</param>
        /// <param name="name">The name of this handler (must be unique). Passing null will assign it a unique name.</param>
        /// <returns>The handler registered.</returns>
        public ScriptHandler RegisterOnce(int ticks, Action<ScriptInterface> func, string name = null)
        {
            return RegisterHandler(ticks, b =>
            {
                func(b);
                return false;
            }, name);
        }

        /// <summary>
        /// Logs a line of text to the script log.
        /// </summary>
        /// <param name="text"></param>
        public void Log(string text)
        {
            Forms.Log.AppendScript(text + "\r\n");
        }

        /// <summary>
        /// Sleeps the bot for the specified time period. This method sleeps the script execution thread.
        /// </summary>
        /// <param name="ms">The time in milliseconds for the bot to sleep.</param>
        public void Sleep(int ms)
        {
            Thread.Sleep(ms);
        }

        /// <summary>
        /// Sends the specified packet to the server.
        /// </summary>
        /// <param name="packet">The packet to be sent.</param>
        /// <param name="type">The type of the packet being sent (String, Json). The default is string.</param>
        /// <remarks>Be careful when using this method. Incorrect use of this method may cause you to be kicked (or banned, although very unlikely).</remarks>
        public void SendPacket(string packet, string type = "String")
        {
            CallGameFunction("sfc.send" + type, packet);
        }

        /// <summary>
        /// Sends the specified packet to the client (simulates a response as if the server sent the packet).
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="type">The type of the packet. This can be xml, json or str.</param>
        public void SendClientPacket(string packet, string type = "str")
        {
            FlashUtil.Call("sendClientPacket", packet, type);
        }

        /// <summary>
        /// Checks if the actionscript object at the given path is null.
        /// </summary>
        /// <param name="path">The path of the object to check.</param>
        /// <returns>True if the object at the given path is null (unset).</returns>
        public bool IsNull(string path)
        {
            return FlashUtil.Call<bool>("isNull", path);
        }

        /// <summary>
        /// Gets an actionscript object at the given location as a JSON string.
        /// </summary>
        /// <param name="path">The path of the object to get.</param>
        /// <returns>The value of the object at the given path as a serialzied JSON string.</returns>
        public string GetGameObject(string path)
        {
            return FlashUtil.Call("getGameObject", path);
        }

        /// <summary>
        /// Gets an actionscript object at the given location and deserializes it as JSON to the given type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the object to.</typeparam>
        /// <param name="path">The path of the object to get (i.e. world.myAvatar.sta.$tha will get your haste stat).</param>
        /// <param name="def">The default value to return if the call/deserialization fails.</param>
        /// <returns>The deserialized value of the object at the given path.</returns>
        public T GetGameObject<T>(string path, T def = default)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(GetGameObject(path));
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Gets a static actionscript object at the given location as a JSON string.
        /// </summary>
        /// <param name="path">The path of the object to get.</param>
        /// <returns>The value of the object at the given path as a serialzied JSON string.</returns>
        public string GetGameObjectStatic(string path)
        {
            return FlashUtil.Call("getGameObjectS", path);
        }

        /// <summary>
        /// Gets a static actionscript object at the given location and deserializes it as JSON to the given type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the object to.</typeparam>
        /// <param name="path">The path of the object to get.</param>
        /// <param name="def">The default value to return if the call/deserialization fails.</param>
        /// <returns>The deserialized value of the object at the given path.</returns>
        public T GetGameObjectStatic<T>(string path, T def = default)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(GetGameObjectStatic(path));
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Sets the value of the actionscript object at the given path.
        /// </summary>
        /// <param name="path">The path of the object to set.</param>
        /// <param name="value">The value to set the object to. This can be a string, any number type or a bool.</param>
        public void SetGameObject(string path, object value)
        {
            FlashUtil.Call("setGameObject", path, value);
        }

        /// <summary>
        /// Calls the actionscript object with the given name at the given location.
        /// </summary>
        /// <param name="path">The path to the object and its function name.</param>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns>The value of the object returned by calling the function as a serialzied JSON string.</returns>
        public string CallGameFunction(string path, params object[] args)
        {
            return args.Length > 0 ? FlashUtil.Call("callGameFunction", new object[] { path }.Concat(args).ToArray()) : FlashUtil.Call("callGameFunction0", path);
        }

        /// <summary>
        /// Calls the actionscript object with the given name at the given location.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the return of the function as.</typeparam>
        /// <param name="path">The path to the object and its function name.</param>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns>The deserialized value of the object returned by the function.</returns>
        public T CallGameFunction<T>(string path, params object[] args)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(CallGameFunction(path, args));
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Gets the actionscript object of the array at the given path at the given index in that array.
        /// </summary>
        /// <param name="path">The path to the array.</param>
        /// <param name="index">The index in the array to get the object from.</param>
        /// <returns>The value of the object at the given index in the array as a serialzied JSON string.</returns>
        public string GetArrayObject(string path, int index)
        {
            return FlashUtil.Call("getArrayObject", path, index);
        }

        /// <summary>
        /// Gets the actionscript object of the array at the given path at the given index in that array.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the object in the array as.</typeparam>
        /// <param name="path">The path to the array.</param>
        /// <param name="index">The index in the array to get the object from.</param>
        /// <param name="def">The default value to return if the call/deserialization fails.</param>
        /// <returns>The deserialized value of the object at the given index in the array.</returns>
        public T GetArrayObject<T>(string path, int index, T def = default)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(GetArrayObject(path, index));
            }
            catch
            {
                return def;
            }
        }

        /// <summary>
        /// Selects the members of each object in the array at the given path and puts them into a new array and returns them.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize the contents of the array as.</typeparam>
        /// <param name="path">The path to the array.</param>
        /// <param name="selector">The name of the field to use to populate the new array.</param>
        /// <returns>A list of deserialized objects from the selected array.</returns>
        public List<T> SelectArrayObjects<T>(string path, string selector)
        {
            try
            {
                return JsonConvert.DeserializeObject<List<T>>(FlashUtil.Call("selectArrayObjects", path, selector));
            }
            catch
            {
                return new List<T>();
            }
        }

        public void HandleFlashCall(AxShockwaveFlash flash, string name, object[] args)
        {
            switch (name)
            {
                case "debug":
                    Debug.WriteLine(args[0]);
                    break;
                case "loaded":
                    Schedule(500, b => FlashUtil.Call("setTitle", $"RBot {Application.ProductVersion}"));
                    Forms.Main.Text = $"RBot {Application.ProductVersion}";
                    break;
                case "pext":
                    dynamic packet = JsonConvert.DeserializeObject<dynamic>((string)args[0]);
                    string type = packet["params"].type;
                    dynamic data = packet["params"].dataObj;
                    if (type == "json")
                    {
                        string cmd = data.cmd;
                        switch (cmd)
                        {
                            case "moveToArea":
                                if (Options.CustomName != null)
                                    Options.CustomName = Options.CustomName;
                                if (Options.CustomGuild != null)
                                    Options.CustomGuild = Options.CustomGuild;
                                Events.OnMapChanged((string)data.strMapName);
                                break;
                            case "ct":
                                dynamic p = data.p == null ? null : data.p[Player.Username.ToLower()];
                                if (p != null && p.intHP == 0)
                                {
                                    Stats.Deaths++;
                                    Events.OnPlayerDeath();
                                }
                                break;
                            case "sellItem":
                                Wait.ItemSellEvent.Set();
                                break;
                            case "buyItem":
                                if (data.bitSuccess == 1)
                                    Wait.ItemBuyEvent.Set();
                                break;
                            case "getDrop":
                                if (data.bSuccess == 1)
                                    Stats.Drops += (int)data.iQty;
                                break;
                            case "addGoldExp":
                                if (data.typ == "m")
                                {
                                    Stats.Kills++;
                                    Events.OnMonsterKilled();
                                }
                                break;
                            case "ccqr":
                                if (data.bSuccess == 1)
                                {
                                    Stats.QuestsCompleted++;
                                    Events.OnQuestTurnIn((int)data.QuestID);
                                }
                                break;
                            case "loadBank":
                                Wait.BankLoadEvent.Set();
                                break;
                            case "sAct":
                                if (AppRuntime.Options.Get<bool>("secret.zmana"))
                                {
                                    using (FlashArray<object> skills = FlashObject<object>.Create("world.actions.active").ToArray())
                                    {
                                        foreach (FlashObject<object> skill in skills)
                                        {
                                            using (FlashObject<int> propMp = skill.GetChild<int>("mp"))
                                                propMp.Value = 0;
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    else if (type == "str")
                    {
                        string cmd = data[0];
                        switch (cmd)
                        {
                            case "uotls":
                                if (Player.Username.Equals((string)data[2], StringComparison.OrdinalIgnoreCase) && data[3] == "afk:true")
                                    Events.OnPlayerAFK();
                                break;
                        }
                    }

                    Events.OnExtensionPacket(packet);
                    break;
                case "packet":
                    string[] parts = ((string)args[0]).Split(new char[] { '%' }, StringSplitOptions.RemoveEmptyEntries);
                    switch (parts[2])
                    {
                        case "moveToCell":
                            Events.OnCellChanged(Map.Name, parts[4], parts[5]);
                            break;
                        case "buyItem":
                            Events.OnTryBuyItem(int.Parse(parts[5]), int.Parse(parts[4]), int.Parse(parts[6]));
                            break;
                        case "acceptQuest":
                            Stats.QuestsAccepted++;
                            Events.OnQuestAccepted(int.Parse(parts[4]));
                            break;
                    }
                    break;
            }
        }

        private TimeLimiter _limit = new TimeLimiter();
        private const int _timerDelay = 20;
        private void _TimerThread()
        {
            bool hasLoggedIn = false;
            bool catching = false;

            int lastConnChange = 0;
            string lastConnDetail = "";

            Stopwatch sw = new Stopwatch();

            while (!_appExit)
            {
                try
                {
                    sw.Restart();

                    if (IsWorldLoaded && Player.Playing)
                    {
                        hasLoggedIn = true;
                        ServerList.LastServerIP = Player.ServerIP ?? ServerList.LastServerIP;

                        if (Options.RestPackets && !Player.InCombat && (Player.Health < Player.MaxHealth || Player.Mana < Player.MaxMana))
                            _limit.LimitedRun("rest", 1000, () => SendPacket("%xt%zm%restRequest%1%%"));

                        if (!catching)
                        {
                            FlashUtil.Call("catchPackets");
                            catching = true;
                        }

                        _limit.LimitedRun("opts", 300, () =>
                        {
                            if (Options.Magnetise)
                                FlashUtil.Call("magnetise");
                            if (Options.InfiniteRange)
                                FlashUtil.Call("infiniteRange");
                            if (Options.AggroMonsters)
                                CallGameFunction("world.aggroAllMon");
                            if (Options.SkipCutscenes)
                                FlashUtil.Call("skipCutscenes");
                            if (Options.LagKiller)
                                FlashUtil.Call("killLag", true);
                            Player.WalkSpeed = Options.WalkSpeed;

                            if (Drops.Enabled)
                            {
                                _limit.LimitedRun("drops", Drops.Interval, () =>
                                {
                                    Drops.Poll();
                                    Drops.Update();
                                });
                            }
                        });
                    }
                    else if (Options.AutoRelogin && !Player.LoggedIn && hasLoggedIn && !_waitForLogin)
                    {
                        Log("Autorelogin triggered.");
                        if (_reloginTask != null)
                        {
                            Log("Relogin task already running.");
                            _waitForLogin = true;
                            continue;
                        }

                        bool wasRunning = ScriptManager.ScriptRunning;
                        ScriptManager.StopScript();

                        bool kicked = Player.Kicked;

                        _waitForLogin = true;
                        Player.Logout();
                        Events.OnReloginTriggered(kicked);

                        _Relogin(Options.AutoReloginAny || (!Options.SafeRelogin && !kicked) ? 5000 : 70000, wasRunning || (_reloginCts?.IsCancellationRequested ?? false));
                    }
                    else if (!Player.LoggedIn && hasLoggedIn)
                        Runtime.BankLoaded = false;

                    _limit.LimitedRun("connDetail", 100, () =>
                    {
                        string connDetail = IsNull("mcConnDetail.stage") ? "null" : GetGameObject("mcConnDetail.txtDetail.text", "null");
                        if (connDetail != "null" && connDetail == lastConnDetail)
                        {
                            if (Environment.TickCount - lastConnChange >= Options.LoadTimeout)
                            {
                                if (connDetail.ToLower().StartsWith("loading map") && !_waitForLogin && Player.LastJoin != null)
                                {
                                    Player.Join(Player.LastJoin);
                                    Map.Reload();
                                    RegisterOnce(500, b =>
                                    {
                                        _waitForLogin = false;
                                        _reloginCts?.Cancel();
                                        Player.Logout();
                                    });
                                }
                                else
                                {
                                    _waitForLogin = false;
                                    _reloginCts?.Cancel();
                                    Player.Logout();
                                }
                                lastConnChange = Environment.TickCount;
                            }
                        }
                        else
                            lastConnChange = Environment.TickCount;
                        lastConnDetail = connDetail;
                    });

                    if (ScriptManager.ScriptRunning)
                    {
                        List<ScriptHandler> rem = new List<ScriptHandler>();
                        foreach (ScriptHandler handler in Handlers)
                        {
                            _limit.LimitedRun("handler_" + handler.Name, handler.Ticks * _timerDelay, () =>
                            {
                                if (!handler.Function(this))
                                    rem.Add(handler);
                            });
                        }
                        rem.ForEach(r => Handlers.Remove(r));
                    }

                    sw.Stop();
                    Thread.Sleep(Math.Max(10, _timerDelay - (int)sw.Elapsed.TotalMilliseconds));
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error in timer thread: " + e.Message);
                }
            }
        }

        private Task _reloginTask;
        private CancellationTokenSource _reloginCts;
        private void _Relogin(int delay, bool startScript)
        {
            Log("Waiting " + delay + "ms for relogin.");
            _reloginCts = new CancellationTokenSource();
            _reloginTask = Schedule(delay, async _ =>
            {
                Stats.Relogins++;
                Server server = Options.AutoReloginAny ? ServerList.Servers.Find(x => x.IP != ServerList.LastServerIP) : Options.LoginServer ?? ServerList.Servers[0];
                Player.Login(Player.Username, Player.Password);
                Player.Connect(server);

                while (_waitForLogin && (!Player.Playing || !IsWorldLoaded) && !_reloginCts.IsCancellationRequested)
                    await Task.Delay(500);
                _waitForLogin = false;

                if (!_reloginCts.IsCancellationRequested && Player.Playing)
                {
                    await Task.Delay(5000);
                    if (startScript)
                        await ScriptManager.StartScriptAsync();
                    Log("Relogin complete.");
                    _reloginCts.Dispose();
                    _reloginCts = null;
                }
                else
                    Log("Relogin was cancelled or unsuccessful.");
                _reloginTask = null;
            });
        }
    }
}
