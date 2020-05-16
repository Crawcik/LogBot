using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Events;
using Smod2.EventHandlers;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
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
    version = "2.8")]
    public class LogbotManager : Plugin, IEventHandlerPlayerDie, IEventHandlerRoundEnd, IEventHandlerAdminQuery, IEventHandlerBan, IEventHandlerRoundStart
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
        
        }

        public override void Register()
        {
            Settings default_settings = new Settings()
            {
                webhook_url = "none",
                autobans = true,
                autoban_text = "%nick% has been banned automatically",
                autoban_reason_text = "You've killed too many people from your team, your punishment duration is %time%",
            };
            try
            {
                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(this.PluginDirectory + $"/config.json", Encoding.Unicode));
                counting = true;
            }
            catch
            {
                if (!Directory.Exists(this.PluginDirectory))
                {
                    Directory.CreateDirectory(this.PluginDirectory);
                }
                if (!File.Exists(this.PluginDirectory + $"/config.json"))
                {
                    File.Create(this.PluginDirectory + $"/config.json");
                    File.WriteAllText(this.PluginDirectory + $"/config.json", JsonConvert.SerializeObject(default_settings), Encoding.Unicode);
                    this.Warn($"Go to '{this.PluginDirectory}/config.json' and change webhook url!");
                }
                this.Error($"Can't read plugin config, make sure your config is correct!");
                return;
            }
            if (!string.IsNullOrWhiteSpace(settings.webhook_url))
            {
                bot = new BotHandler(settings.webhook_url);
            }
            this.AddEventHandlers(this);
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"SCP Secret Laboratory/ServerLogs/players_log_{this.Server.Port}.json"))
            {
                string context = File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"SCP Secret Laboratory/ServerLogs/players_log_{this.Server.Port}.json", System.Text.Encoding.Unicode);
                if (!string.IsNullOrEmpty(context))
                    GetKills = JsonConvert.DeserializeObject<List<KillCount>>(context);
            }
            Info("LogBot has started");
        }

        private void SaveBans() 
        {
            var result = JsonConvert.SerializeObject(GetKills);
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +$"SCP Secret Laboratory/ServerLogs/players_log_{this.Server.Port}.json";
            if (!File.Exists(path))
            {
                File.Create(path);
                File.WriteAllText(path, JsonConvert.SerializeObject(GetKills), System.Text.Encoding.Unicode);
            }
        }
        #endregion
        public void OnPlayerDie(PlayerDeathEvent ev)
        {
            this.Debug($"Someone die");
            if (ev.Killer.PlayerId == ev.Player.PlayerId || !canLog)
                return;
            if ((ev.Killer.TeamRole.Team.Equals(Team.MTF) && ev.Player.TeamRole.Team.Equals(Team.RSC)) ||
                (ev.Killer.TeamRole.Team.Equals(Team.RSC) && ev.Player.TeamRole.Team.Equals(Team.MTF)) ||
                (ev.Killer.TeamRole.Team.Equals(Team.CHI) && ev.Player.TeamRole.Team.Equals(Team.CDP)) ||
                (ev.Killer.TeamRole.Team.Equals(Team.CHI) && ev.Player.TeamRole.Team.Equals(Team.CHI))) 
            {
                int index = RegisterKiller(ev.Killer);
                this.Debug($"Logging kill");
                bot.Post($"{ev.Killer.Name} killed {ev.Player.Name} using {Enum.GetName(typeof(DamageType), ev.DamageTypeVar)}",
                    $"{ev.Killer.TeamRole.Name} killed {ev.Player.TeamRole.Name}", ev.Killer.UserId + ev.Killer.IpAddress + "\tKills: " + GetKills[index].kills, 0);
            } 
            else if (ev.Killer.TeamRole.Team == ev.Player.TeamRole.Team)
            {
                int index = RegisterKiller(ev.Killer);
                this.Debug($"Logging kill");
                bot.Post($"{ev.Killer.Name} killed {ev.Player.Name} using {Enum.GetName(typeof(DamageType), ev.DamageTypeVar)}",
                    $"{ev.Killer.TeamRole.Name} killed {ev.Player.TeamRole.Name}", ev.Killer.UserId + ev.Killer.IpAddress + "\tKills: " + GetKills[index].kills, 0);
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

        public void OnBan(BanEvent ev)
        {
            bans_count++;
            if (this.isAutoBan || ev.Duration == 0)
            {
                this.isAutoBan = false;
                return;
            }    
            bot.Post($"{ev.Player.Name} get banned",
                $"Time: {ev.Duration/60} hours", ev.Player.UserId + ev.Player.IpAddress + "\tKills: " + GetKills.Find(x=>x.userID == ev.Player.UserId).kills, 16732240);
        }

        public void OnRoundEnd(RoundEndEvent ev)
        {
            if (ev.Round.Duration < 60)
                return;
            bot.Post("Round End", $"Teamkills count: {teamkills_count} | Bans count: {bans_count}", $"Round time: {ev.Round.Duration / 60} minutes", 3289800);
            canLog = false;
            SaveBans();
        }
        public void OnRoundStart(RoundStartEvent ev)
        {
            canLog = true;
            bot.Post("Round Start",null, null, 3289800);
            teamkills_count = 0;
            bans_count = 0;
        }

        public void OnAdminQuery(AdminQueryEvent ev)
        {
            if (ev.Query.Contains("logbot"))
            {
                string[] args = ev.Query.Split(' ');
                if (args[0] == "logbot")
                {
                    if (SwitchLogbot(args[1]))
                    {
                        ev.Successful = true;
                        ev.Admin.SendConsoleMessage("Changed!", "yellow");
                    }
                    else
                    {
                        ev.Successful = false;
                        ev.Admin.SendConsoleMessage("Usage: logbot [on/off]", "red");
                    }
                }
            }
            else if (ev.Query.Contains("bcp"))
            {
                string[] args = ev.Query.Split(' ');
                uint time;
                if (args.Length <= 4)
                {
                    int PlayerID;
                    if (args[2].Contains("@"))
                    {
                        PlayerID = this.Server.GetPlayers().Find(x => x.UserId == args[2]).PlayerId;
                    }
                    else
                    {
                        PlayerID = int.Parse(args[2]);
                    }
                    if (args[0] == "bcp" && uint.TryParse(args[1], out time))
                    {
                        List<string> text = args.ToList();
                        text.RemoveRange(0, 3);
                        string message = string.Join(" ", text.ToArray());
                        this.Server.GetPlayer(PlayerID).PersonalBroadcast(time, args[3], false);
                    }
                    else
                    {
                        ev.Output = "Usage: bcp [minutes] [id/steam] [message]";
                    }
                    ev.Successful = true;
                    ev.Admin.SendConsoleMessage("Changed!", "yellow");
                }
                else
                {
                    ev.Output = "Usage: bcp [minutes] [id/steam] [message]";
                }
            }
            else if(ev.Query.Contains("unban")) 
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Roaming\SCP Secret Labolatory\config\true\";
                string[] args = ev.Query.Split(' ');
                if (args.Length == 3)
                {
                    string file = "";
                    if (args[1] == "ip")
                        file = "IpBans.txt";
                    else if (args[1] == "id")
                        file = "UserIdBans.txt";
                    List<string> list = File.ReadAllLines(path + file).ToList();
                    if (list.Exists(x => x.Contains(args[2]))){
                        list.Remove(list.Find(x => x.Contains(args[2])));
                        File.WriteAllLines(path + file, list.ToArray());
                    }
                }
            }
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
