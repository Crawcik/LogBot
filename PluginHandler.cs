﻿using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Events;
using Smod2.EventHandlers;
using Smod2.Commands;

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;

using Newtonsoft.Json;
using System.Linq;

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
    version = "2.6")]
    public class PluginHandler : Plugin, IEventHandlerPlayerDie, IEventHandlerRoundEnd, IEventHandlerAdminQuery, IEventHandlerBan, IEventHandlerRoundStart
    {
        private BotHandler bot;
        private List<KillCount> GetKills = new List<KillCount>();
        private List<string> act_combo = new List<string>();
        public bool canLog = true;
        public int teamkills_count, bans_count;
        Settings settings;

        #region Startup

        public override void OnDisable()
        {
            Info("LogBot has been disabled");
        }

        public override void OnEnable()
        {
            try
            {
                if (this.config.Count == 0)
                {
                    Settings default_settings = new Settings() { webhook_url = "none",
                        autobans = true,
                        autoban_text = "%nick% has been banned automatically",
                    autoban_reason_text = "You've killed too many people from your team, your punishment duration is %time%"};
                    File.WriteAllText(this.PluginDirectory + $"\\servers\\{this.Server.Port}\\config.json", JsonConvert.SerializeObject(default_settings));
                    this.Warn($"Go to '{this.PluginDirectory}\\servers\\{ this.Server.Port}\\config.json' and change webhook url!");
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
            if(settings.autobans)

            bot = new BotHandler(this.Server.Name, settings.webhook_url);
            this.AddEventHandlers(this);
            if (File.Exists(FileManager.GetAppFolder() + $"ServerLogs/players_log_{this.Server.Port}.json"))
            {
                string context = File.ReadAllText(FileManager.GetAppFolder() + $"ServerLogs/players_log_{this.Server.Port}.json", System.Text.Encoding.UTF8);
                if (!string.IsNullOrEmpty(context))
                    GetKills = JsonConvert.DeserializeObject<List<KillCount>>(context);
            }
            Info("LogBot has started");
        }

        public override void Register()
        {
            //Will be implemented auto update
        }

        private void SaveBans() 
        {
            var result = JsonConvert.SerializeObject(GetKills);
            string path = FileManager.GetAppFolder() + $"ServerLogs/players_log_{this.Server.Port}.json";
            if (!File.Exists(path))
            {
                File.Create(path);
                File.WriteAllText(path, JsonConvert.SerializeObject(GetKills), System.Text.Encoding.UTF8);
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
            if (ev.Duration == 0)
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
                    if (args[1] == "on")
                    {
                        canLog = true;
                    }
                    else if (args[1] == "off")
                    {
                        canLog = false;
                    }
                    else
                    {
                        ev.Output = "Usage: logbot [on/off]";
                    }
                    ev.Successful = true;
                    ev.Admin.SendConsoleMessage("Zmieniono!", "yellow");
                }
            }
            if (ev.Query.Contains("bcp"))
            {
                string[] args = ev.Query.Split(' '); 
                uint time;
                if (args.Length <= 4)
                {
                    int PlayerID;
                    if (args[2].Contains("@"))
                    {
                        PlayerID = this.Server.GetPlayers().Find(x => x.UserId == args[2]).PlayerId;
                    } else
                    {
                        PlayerID = int.Parse(args[2]);
                    }
                    if(args[0] == "bcp" && uint.TryParse(args[1], out time))
                    {
                        List<string> text = args.ToList();
                        text.RemoveRange(0, 3);
                        string message = "";
                        
                        text.ForEach(x => message += x + " "); 
                        this.Server.GetPlayer(PlayerID).PersonalBroadcast(time, args[3], false);
                    }
                    else
                    {
                        ev.Output = "Usage: bcp [minutes] [id/steam] [message]";
                    }
                    ev.Successful = true;
                    ev.Admin.SendConsoleMessage("Zmieniono!", "yellow");
                }
                else
                {
                    ev.Output = "Usage: bcp [minutes] [id/steam] [message]";
                }
            }
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
