using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Events;
using Smod2.EventHandlers;
using Smod2.Commands;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

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
    SmodMinor = 8,
    SmodRevision = 0,
    version = "2.8")]
    public class PluginHandler : Plugin, IEventHandlerPlayerDie, IEventHandlerRoundEnd, IEventHandlerAdminQuery, IEventHandlerBan, IEventHandlerRoundStart
    {
        private BotHandler bot;
        private List<KillCount> GetKills = new List<KillCount>();
        private List<string> act_combo = new List<string>();
        public bool canLog = true, counting = true, reading = false;
        public int teamkills_count, bans_count;
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
            counting = true;
            try
            {
                if (this.config.Count == 0)
                {
                    Settings default_settings = new Settings() { webhook_url = "none",
                        autobans = true,
                        autoban_text = "%nick% has been banned automatically",
                        autoban_reason_text = "You've killed too many people from your team, your punishment duration is %time%",
                        extended_bot = false };
                    File.WriteAllText(this.PluginDirectory + $"/servers/{this.Server.Port}/config.json", JsonConvert.SerializeObject(default_settings));
                    this.Warn($"Go to '{this.PluginDirectory}/servers/{ this.Server.Port}/config.json' and change webhook url!");
                    return;
                }
                else
                {
                    settings = this.config.ToObject<Settings>();
                    if (settings.webhook_url == null || settings.webhook_url == "" || settings.webhook_url == "none")
                    {
                        this.Warn($"Can't read webhook address, make sure you write the webhook link to config!");
                        return;
                    }
                }
            }
            catch
            {
                this.Error($"Can't read webhook config, make sure your config is correct!");
                return;
            }
            string pipeid = null;
            if (settings.extended_bot)
            {
                pipeid = this.Server.Port.ToString();
                Info("Extended bot pipe name: " + pipeid);
            }
            if(settings.autobans || settings.extended_bot)
                bot = new BotHandler(settings.webhook_url, pipeid);
            this.AddEventHandlers(this);
            if (File.Exists(FileManager.GetAppFolder() + $"ServerLogs/players_log_{this.Server.Port}.json"))
            {
                string context = File.ReadAllText(FileManager.GetAppFolder() + $"ServerLogs/players_log_{this.Server.Port}.json", System.Text.Encoding.Unicode);
                if (!string.IsNullOrEmpty(context))
                    GetKills = JsonConvert.DeserializeObject<List<KillCount>>(context);
            }
            Info("LogBot has started");
        }

        public override void Register()
        {
            //Will be implemented auto update
            SendCount().GetAwaiter();
        }

        private async Task SendCount()
        {
            while (this.counting)
            {
                await Task.Delay(10000);
                if (bot != null && !reading)
                    if (bot.stream != null)
                        if (bot.stream.IsConnected)
                            ReadWait().GetAwaiter();
                await bot.SendToBot(MessageType.SERVER_COUNT, this.Server.GetPlayers().Count);
            }
        }

        private async Task ReadWait()
        {
            reading = true;
            while (bot.stream.IsConnected)
            {
                BotHandler.Message msg = await bot.WaitForMessage();
                try
                {
                    switch ((MessageType)msg.destiny)
                    {
                        case MessageType.SWITCH_LOGBOT:
                            SwitchLogbot((string)msg.data);
                            break;
                        case MessageType.SWITCH_AUTOBANS:
                            if ((string)msg.data == "on")
                                this.settings.autobans = true;
                            else if((string)msg.data == "off")
                                this.settings.autobans = false;
                            break;
                        case MessageType.BAN:
                            string send = (string)msg.data;
                            this.Server.GetPlayers().Find(x => x.UserId == send.Split(' ')[0]).Ban(int.Parse(send.Split(' ')[1]));
                            break;
                    }
                } 
                catch
                {
                    await bot.SendToBot(MessageType.ERROR, null);
                }
            }
            reading = false;
        }

        private void SaveBans() 
        {
            var result = JsonConvert.SerializeObject(GetKills);
            string path = FileManager.GetAppFolder() + $"ServerLogs/players_log_{this.Server.Port}.json";
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
                (ev.Killer.TeamRole.Team.Equals(Team.CDP) && ev.Player.TeamRole.Team.Equals(Team.CHI))) 
            {
                int index = RegisterKiller(ev.Killer);
                this.Debug($"Logging kill");
                bot.Post($"{ev.Killer.Name} killed {ev.Player.Name} using {Enum.GetName(typeof(DamageType), ev.DamageTypeVar)}",
                    $"{ev.Killer.TeamRole.Name} killed {ev.Player.TeamRole.Name}", ev.Killer.UserId + ev.Killer.IpAddress + "\tKills: " + GetKills[index].kills, 0);
            } else if (ev.Killer.TeamRole.Team == ev.Player.TeamRole.Team)
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
                this.Debug("Couting "+ GetKills[killerIND].userID + ": " + i);
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
            this.Server.Map.Broadcast(3, broadcast_text, false);
            bot.Post(broadcast_text,
                $"Time: {time_text}", ply.UserId + ply.IpAddress + "\tKills: " + GetKills.Find(x=>x.userID == ply.UserId).kills, 16732240);
            ply.Ban(duration, reason);
        }

        public void OnBan(BanEvent ev)
        {
            if (ev.Admin == null || ev.Duration == 0)
                return;
            bans_count++;
            bot.Post($"{ev.Player.Name} get banned by {ev.Admin.Name}",
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
            [JsonProperty]
            public bool extended_bot;
        }
    }
}
