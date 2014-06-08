//Import various C# things.
using System;
using System.IO;
using System.Text;
using System.Timers;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

//Import Procon things.
using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Players;

namespace PRoConEvents
{
    public class SeedKicker : PRoConPluginAPI, IPRoConPluginInterface
    {

        private bool pluginEnabled = false;
        private int playerCount = 0;
        //The List of seeders as configured.
        private List<String> seederList = new List<String>();
        private DateTime then = new DateTime();
        private int minSeconds = 60;
        private int threshold = 24;
        private String debugLevelString = "1";
        private int debugLevel = 1;
        private string kickMessage = "Seeding has completed for this server. Thank you!";
        private Timer kickTimer = new Timer();
        private Queue<String> kickQueue = new Queue<String>();

        //SK 1.0
        private int autoKickSeconds = 1800;
        private DateTime playerJoinedTime = new DateTime();
        //Have we gone under the threshold?
        private bool hasDipped = true;
        private int lastDipped = 0;
        private int maxSeedersAllowed = 4;

        public SeedKicker()
        {

        }

        public string GetPluginName()
        {
            return "SeedKicker";
        }

        public string GetPluginVersion()
        {
            return "1.1.6";
        }

        public string GetPluginAuthor()
        {
            return "Analytalica";
        }

        public string GetPluginWebsite()
        {
            return "purebattlefield.org";
        }

        public string GetPluginDescription()
        {
            return @"<p>SeedKicker is a plugin that kicks specific players when the server has reached or exceeded a player set count.</p>
<p>
<ul>
<li><b>To add or remove players:</b> Type a name in 'Add a soldier name' and that player will be considered a seeder. Clear a soldier name field and it will be removed from the list.</li>
<li><b>Player Count Threshold:</b> Specifies the player count needed (larger than or equal to) before starting the kicking process.</li>
<li><b>Min. Time Threshold is Met:</b> Specifices the minimum amount of time, in seconds, the player count must be larger than or equal to the threshold before kicking seeders. NOTE: The actual kick delay may have up to a ~20 second variance.</li>
<li><b>Kick Message:</b> The message seeders will see when kicked.</li>
<li><b>Auto Kick Timer Delay (sec):</b> How much time since the last player joined until the seeders get auto-kicked to re-seed servers.</li></ul>
<li><b>Maximum Seeders Allowed On Server:</b> Specifices how many seeders can be on the server at once. Excess seeders will be kicked to bring the in-game seeder count down to this level.</li>
</p>";
        }

        public void toConsole(int msgLevel, String message)
        {
            if (debugLevel >= msgLevel)
            {
                this.ExecuteCommand("procon.protected.pluginconsole.write", "SeedKicker: " + message);
            }
        }

        //--------------------------------------
        //These methods run when Procon does what's on the label.
        //--------------------------------------

        //Runs when the plugin is compiled.

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name, "OnPluginLoaded", "OnServerInfo", "OnPlayerJoin", "OnListPlayers");
            this.kickTimer = new Timer();
            this.kickTimer.Elapsed += new ElapsedEventHandler(this.kickPlayers);
            this.kickTimer.Interval = 1500;
            //this.kickTimer.Start();
            this.kickTimer.Stop();
            this.then = DateTime.Now;
            this.playerJoinedTime = DateTime.Now;
            this.kickQueue.Clear();
        }

        public void OnPluginEnable()
        {
            this.pluginEnabled = true;
            this.toConsole(1, "SeedKicker Enabled!");
            this.kickTimer = new Timer();
            this.kickTimer.Elapsed += new ElapsedEventHandler(this.kickPlayers);
            this.kickTimer.Interval = 2000;
            //this.kickTimer.Start();
            this.kickTimer.Stop();
            this.then = DateTime.Now;
            this.playerJoinedTime = DateTime.Now;
            this.hasDipped = true;
            this.kickQueue.Clear();
        }

        public void OnPluginDisable()
        {
            this.toConsole(1, "SeedKicker Disabled!");
            this.kickTimer.Stop();
            this.then = DateTime.Now;
            this.playerJoinedTime = DateTime.Now;
            this.hasDipped = true;
            this.pluginEnabled = false;
            this.kickQueue.Clear();
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            if (this.pluginEnabled)
            {
                List<String> newOnlineList = new List<String>();
                this.toConsole(2, "Player list obtained.");
                foreach (CPlayerInfo player in players)
                {
                    if (seederList.Contains(player.SoldierName.Trim().ToLower()))
                    {
                        newOnlineList.Add(player.SoldierName.Trim());
                    }
                }
                if (debugLevel > 1)
                {
                    this.toConsole(2, "" + newOnlineList.Count + " seeders found online: ");
                    foreach (string name in newOnlineList)
                    {
                        this.toConsole(2, name);
                    }
                }
                if (newOnlineList.Count > maxSeedersAllowed)
                {
                    int kickThisMany = newOnlineList.Count - maxSeedersAllowed;
                    this.toConsole(2, "There are " + kickThisMany + " too many seeders on this server. Kicking some...");
                    foreach (string seederName in newOnlineList)
                    {
                        if (this.kickQueue.Count < kickThisMany)
                        {
                            this.toConsole(2, "Enqueuing " + seederName);
                            this.kickQueue.Enqueue(seederName);
                        }
                        else
                        {
                            break;
                        }
                    }
                    this.kickTimer = new Timer();
                    this.toConsole(3, "Kick timer initiated...");
                    this.kickTimer.Elapsed += new ElapsedEventHandler(this.kickPlayers);
                    this.kickTimer.Interval = 2000;
                    this.toConsole(3, "Starting kick timer...");
                    this.kickTimer.Start();
                }
            }
        }

        public override void OnServerInfo(CServerInfo csiServerInfo)
        {
            if (pluginEnabled)
            {
                this.playerCount = csiServerInfo.PlayerCount;

                this.toConsole(2, "Refreshing player count: " + this.playerCount + " players found online.");
                if (this.playerCount >= this.threshold)
                {
                    this.toConsole(2, "Threshold met at " + this.threshold + " with " + this.playerCount + " players online.");
                    this.toConsole(2, "It's been " + DateTime.Now.Subtract(this.then).TotalSeconds + " seconds since we met the threshold.");
                    if ((int)DateTime.Now.Subtract(this.then).TotalSeconds >= this.minSeconds)
                    {
                        this.toConsole(2, "Time threshold exceeded.");
                        if (this.hasDipped || this.lastDipped > 10)
                        {
                            this.KickAllSeeders();
                            this.hasDipped = false;
                            this.lastDipped = 0;
                        }
                        else
                        {
                            this.toConsole(2, "All seeders were already kicked earlier, " + this.lastDipped + " refreshes ago. I'll kick again at 10.");
                            this.lastDipped++;
                        }
                        this.then = DateTime.Now;
                    }
                }
                else
                {
                    this.toConsole(2, "Threshold not yet met.");
                    this.hasDipped = true;
                    this.then = DateTime.Now;
                    this.kickTimer.Stop();
                }
                this.toConsole(2, "It's been " + (int)DateTime.Now.Subtract(this.playerJoinedTime).TotalSeconds + " seconds since a player joined.");
                if (this.autoKickSeconds != 0 && (int)DateTime.Now.Subtract(this.playerJoinedTime).TotalSeconds >= this.autoKickSeconds)
                {
                    this.toConsole(2, "It's been too long since a player has joined.");
                    this.KickAllSeeders();
                    this.playerJoinedTime = DateTime.Now;
                }
            }
        }

        public override void OnPlayerJoin(string soldierName)
        {
            if (pluginEnabled)
            {
                this.toConsole(2, "A new player has joined. Resetting last join time...");
                this.playerJoinedTime = DateTime.Now;
            }
        }

        public void KickAllSeeders()
        {
            if (pluginEnabled)
            {
                this.toConsole(1, "Kicking seeders...");
                foreach (string seederName in seederList)
                {
                    this.toConsole(2, "Enqueuing " + seederName);
                    this.kickQueue.Enqueue(seederName);
                }
                this.kickTimer = new Timer();
                this.toConsole(3, "Kick timer initiated...");
                this.kickTimer.Elapsed += new ElapsedEventHandler(this.kickPlayers);
                this.kickTimer.Interval = 2000;
                this.toConsole(3, "Starting kick timer...");
                this.kickTimer.Start();
            }
        }

        public void kickPlayers(object source, ElapsedEventArgs e)
        {
            if (pluginEnabled)
            {
                if (this.kickQueue.Count > 0)
                {
                    string nextPlayer = kickQueue.Dequeue();
                    this.toConsole(3, "Kicking " + nextPlayer);
                    this.ExecuteCommand("procon.protected.send", "admin.kickPlayer", nextPlayer, kickMessage);
                }
                else
                {
                    this.toConsole(2, "Seeders kicked from server.");
                    this.kickTimer.Stop();
                    this.toConsole(3, "Kick timer stopped.");
                }
            }
        }

        //List plugin variables.
        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("Seeder List|Add a soldier name... (ci)", typeof(string), ""));
            seederList.Sort();
            for (int i = 0; i < seederList.Count; i++)
            {
                String thisPlayer = seederList[i];
                if (String.IsNullOrEmpty(thisPlayer))
                {
                    seederList.Remove(thisPlayer);
                    i--;
                }
                else
                {
                    lstReturn.Add(new CPluginVariable("Seeder List|" + i.ToString() + ". Soldier name:", typeof(string), thisPlayer));
                }
            }
            lstReturn.Add(new CPluginVariable("Settings|Player Count Threshold", typeof(string), threshold.ToString()));
            lstReturn.Add(new CPluginVariable("Settings|Min. Time Threshold is Met (sec)", typeof(string), minSeconds.ToString()));
            lstReturn.Add(new CPluginVariable("Settings|Auto Kick Timer Delay (sec) (0 to disable)", typeof(string), autoKickSeconds.ToString()));
            lstReturn.Add(new CPluginVariable("Settings|Kick Message", typeof(string), kickMessage));
            lstReturn.Add(new CPluginVariable("Settings|Maximum Seeders Allowed On Server", typeof(string), maxSeedersAllowed.ToString()));
            lstReturn.Add(new CPluginVariable("Settings|Debug Level", typeof(string), debugLevelString));
            lstReturn.Add(new CPluginVariable("Settings|Test Kick All Seeders", typeof(string), "Enter any text..."));
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        public int getConfigIndex(string configString)
        {
            int lineLocation = configString.IndexOf('|');
            return Int32.Parse(configString.Substring(lineLocation + 1, configString.IndexOf('.') - lineLocation - 1));
        }

        //Set variables.
        public void SetPluginVariable(String strVariable, String strValue)
        {
            if (strVariable.Contains("Soldier name:"))
            {
                int n = getConfigIndex(strVariable);
                try
                {
                    seederList[n] = strValue.Trim().ToLower();
                }
                catch (ArgumentOutOfRangeException e)
                {
                    seederList.Add(strValue.Trim().ToLower());
                }
            }
            else if (strVariable.Contains("Add a soldier name"))
            {
                seederList.Add(strValue.Trim().ToLower());
            }
            else if (strVariable.Contains("Kick Message"))
            {
                kickMessage = strValue.Trim();
            }
            else if (strVariable.Contains("Debug Level"))
            {
                debugLevelString = strValue;
                try
                {
                    debugLevel = Int32.Parse(debugLevelString);
                }
                catch (Exception z)
                {
                    toConsole(1, "Invalid debug level! Choose 0, 1, or 2 only.");
                    debugLevel = 1;
                    debugLevelString = "1";
                }
            }
            else if (strVariable.Contains("Player Count Threshold")){
                try
                {
                    threshold = Int32.Parse(strValue);
                }
                catch (Exception z)
                {
                    toConsole(1, "Invalid threshold! Use integer values only.");
                    threshold = 24;
                }
            }
            else if (strVariable.Contains("Min. Time Threshold is Met (sec)"))
            {
                try
                {
                    minSeconds = Int32.Parse(strValue);
                }
                catch (Exception z)
                {
                    toConsole(1, "Invalid min seconds! Use integer values only.");
                    minSeconds = 60;
                }
            }
            else if (strVariable.Contains("Auto Kick Timer Delay (sec)"))
            {
                try
                {
                    autoKickSeconds = Int32.Parse(strValue);
                }
                catch (Exception z)
                {
                    toConsole(1, "Invalid auto kick seconds! Use integer values only.");
                    autoKickSeconds = 1800;
                }
            }
            else if (strVariable.Contains("Maximum Seeders Allowed On Server"))
            {
                try
                {
                    maxSeedersAllowed = Int32.Parse(strValue);
                }
                catch (Exception z)
                {
                    toConsole(1, "Invalid max seeders! Use integer values only.");
                    maxSeedersAllowed = 4;
                }
            }
            else if (strVariable.Contains("Test Kick All Seeders"))
            {
                KickAllSeeders();
            }
        }
    }
}