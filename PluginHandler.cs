using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.EventHandlers;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

namespace LogBot
{

    [PluginDetails(
    author = "Crawcik",
    configPrefix = "logbot",
    description = "Specified Logs to discord",
    id = "logbot",
    langFile = "logbot",
    name = "LogBot",
    SmodMajor = 3,
    SmodMinor = 7,
    SmodRevision = 0,
    version = "2.9")]
    public partial class LogbotManager : Plugin
    {
        public string PluginDirectory {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                return Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path)) + $"/{this.Details.name}";
            }
        }

        private BotHandler bot;
        private List<KillCount> GetKills = new List<KillCount>();
        private List<string> act_combo = new List<string>();
        public bool canLog = true, counting = true, reading = false;
        public int teamkills_count, bans_count;
        public bool isAutoBan = false;
        Settings settings;

        #region Startup

        public override void OnDisable()
        {
            bot = null;
            canLog = false;
            counting = false;
            Info("LogBot has been disabled");
        }

        public override void OnEnable()
        {
            //That's because port number is loaded with some delay.
            this.Debug("Config will be loaded in 8 seconds");
            LoadConfig().GetAwaiter();
        }

        public async Task LoadConfig()
        {

            await Task.Delay(TimeSpan.FromSeconds(8));
            string path = $"{this.PluginDirectory}/{this.Server.Port}/config.json";
            try
            {
                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path, Encoding.UTF8));
                counting = true;
            }
            catch (Exception e)
            {
                if (!Directory.Exists(this.PluginDirectory))
                {
                    Directory.CreateDirectory(this.PluginDirectory);
                    this.Warn($"Go to '{path}' and change webhook url!");
                }
                if (!File.Exists(path))
                {
                    File.Create(path);
                    if (File.Exists($"{this.PluginDirectory}/config.json"))
                    {
                        settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText($"{this.PluginDirectory}/config.json", Encoding.UTF8));
                        this.Warn($"Created a file with port {this.Server.Port} from config");
                    }
                    File.WriteAllText(path, JsonConvert.SerializeObject(settings), Encoding.UTF8);
                }
                else
                {
                    this.Error("Can't read plugin config, make sure your shared config is correct!");
                    this.Error("Reason: " + e.Message);
                    return;
                }
            }
            if (!string.IsNullOrWhiteSpace(settings.webhook_url))
            {
                bot = new BotHandler(settings.webhook_url);
            }
            this.AddEventHandlers(this);
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"/SCP Secret Laboratory/ServerLogs/players_log_{this.Server.Port}.json"))
            {
                string context = File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"SCP Secret Laboratory/ServerLogs/players_log_{this.Server.Port}.json", Encoding.UTF8);
                if (!string.IsNullOrEmpty(context))
                    GetKills = JsonConvert.DeserializeObject<List<KillCount>>(context);
            }
            Info("LogBot has started");
        }

        public override void Register()
        {
            settings = new Settings()
            {
                webhook_url = "none",
                autobans = true,
                autoban_text = "%nick% has been banned automatically",
                autoban_reason_text = "You've killed too many people from your team, your punishment duration is %time%",
            };
        }
        #endregion

        private void SaveBans() 
        {
            var result = JsonConvert.SerializeObject(GetKills);
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +$"/SCP Secret Laboratory/ServerLogs/players_log_{this.Server.Port}.json";
            if (!File.Exists(path))
            {
                File.Create(path);
                File.WriteAllText(path, JsonConvert.SerializeObject(GetKills), Encoding.UTF8);
            }
        }
        

        private int RegisterKiller(Player Killer)
        {
            teamkills_count++;
            if (!GetKills.Exists(x => x.userID == Killer.UserId))
            {
                GetKills.Add(new KillCount { IPv4 = Killer.IpAddress, userID = Killer.UserId, kills = 0 });
                this.Debug($"Killer added");
            }
            int index = GetKills.FindIndex(x => x.userID == Killer.UserId || x.IPv4 == Killer.IpAddress);
            this.Debug($"Killer index: {index}");
            if (!act_combo.Contains(GetKills[index].userID))
            {
                act_combo.Add(GetKills[index].userID);
                if(settings.autobans)
                    ComboCounter(index).GetAwaiter();
                this.Debug($"Killer counter starts");
            }
            GetKills[index] = new KillCount { userID = Killer.UserId, IPv4 = Killer.IpAddress, kills = GetKills[index].kills + 1 };
            return index;
        }

        private async Task ComboCounter(int killerIND)
        {
            int first = GetKills[killerIND].kills;
            for (int i = 0; i < 21; i++)
            {
                if (first + 5 <= GetKills[killerIND].kills && i <= 12)
                {
                    AutoBan("Month", 43200, this.Server.GetPlayers().Find(x => x.UserId == GetKills[killerIND].userID));
                    break;
                }
                if (first + 3 <= GetKills[killerIND].kills && i == 12)
                {
                    AutoBan("Day", 1440, this.Server.GetPlayers().Find(x => x.UserId == GetKills[killerIND].userID));
                    break;
                }
                if (first + 3 <= GetKills[killerIND].kills && i >12)
                {
                    AutoBan("Hour", 60, this.Server.GetPlayers().Find(x => x.UserId == GetKills[killerIND].userID));
                    break;
                }
                await Task.Delay(1000);
            }
            act_combo.Remove(GetKills[killerIND].userID);
        }

        private void AutoBan(string time_text, int duration, Player ply)
        {
            string reason = settings.autoban_reason_text.Replace("%time%", time_text);
            string broadcast_text = settings.autoban_text.Replace("%nick%", ply.Name);
            this.Server.Map.Broadcast(3, broadcast_text, true);
            this.isAutoBan = true;
            bot.Post(broadcast_text,
                $"Time: {time_text}", ply.UserId + ply.IpAddress + "\tKills: " + GetKills.Find(x=>x.userID == ply.UserId).kills, 16732240);
            ply.Ban(duration, reason);
        }

        

        private bool SwitchLogbot(string arg)
        {

            if (arg == "on")
            {
                canLog = true;
                this.EventManager.RemoveEventHandlers(this);
                this.AddEventHandlers(this);
            }
            else if (arg == "off")
            {
                this.EventManager.RemoveEventHandlers(this);
                this.AddEventHandler(typeof(IEventHandlerAdminQuery), this);
            }
            else
            {
                return false;
            }
            return true;
        }

        public struct KillCount 
        {
            public string userID;
            public string IPv4;
            public int kills;
        }

        private struct Settings
        {
            [JsonProperty]
            public string webhook_url;
            [JsonProperty]
            public bool autobans;
            [JsonProperty]
            public string autoban_text;
            [JsonProperty]
            public string autoban_reason_text;
        }
    }
}
