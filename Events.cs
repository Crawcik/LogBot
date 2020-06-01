using Smod2.API;
using Smod2.EventHandlers;
using Smod2.Events;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LogBot
{
    public partial class LogbotManager : IEventHandlerPlayerDie, IEventHandlerRoundEnd, IEventHandlerAdminQuery, IEventHandlerBan, IEventHandlerRoundStart
    {
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

        public void OnBan(BanEvent ev)
        {
            bans_count++;
            if (this.isAutoBan || ev.Duration == 0)
            {
                this.isAutoBan = false;
                return;
            }
            bot.Post($"{ev.Player.Name} get banned",
                $"Time: {ev.Duration / 60} hours", ev.Player.UserId + ev.Player.IpAddress + "\tKills: " + GetKills.Find(x => x.userID == ev.Player.UserId).kills, 16732240);
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
            bot.Post("Round Start", null, null, 3289800);
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
            else if (ev.Query.Contains("unban"))
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
                    if (list.Exists(x => x.Contains(args[2])))
                    {
                        list.Remove(list.Find(x => x.Contains(args[2])));
                        File.WriteAllLines(path + file, list.ToArray());
                    }
                }
            }
        }
    }
}