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

        public SeedKicker()
        {

        }

        public string GetPluginName()
        {
            return "SeedKicker";
        }

        public string GetPluginVersion()
        {
            return "0.4.0";
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
            return @"<p></p>";
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
            this.RegisterEvents(this.GetType().Name, "OnPluginLoaded", "OnServerInfo");
            this.kickTimer = new Timer();
            this.kickTimer.Elapsed += new ElapsedEventHandler(this.kickPlayers);
            this.kickTimer.Interval = 1500;
            this.kickTimer.Start();
            this.kickTimer.Stop();
        }

        public void OnPluginEnable()
        {
            this.pluginEnabled = true;
            this.toConsole(1, "SeedKicker Enabled!");
            this.kickTimer = new Timer();
            this.kickTimer.Elapsed += new ElapsedEventHandler(this.kickPlayers);
            this.kickTimer.Interval = 1500;
            this.kickTimer.Start();
            this.kickTimer.Stop();
        }

        public void OnPluginDisable()
        {
            this.toConsole(1, "SeedKicker Disabled!");
            this.kickTimer.Stop();
            this.pluginEnabled = false;
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
                    if (DateTime.Now.Subtract(this.then).Seconds >= this.minSeconds)
                    {
                        this.toConsole(1, "Kicking seeders...");
                        foreach (string seederName in seederList)
                        {
                            this.kickQueue.Enqueue(seederName);
                        }

                        this.then = DateTime.Now;

                        this.kickTimer = new Timer();
                        this.kickTimer.Elapsed += new ElapsedEventHandler(this.kickPlayers);
                        this.kickTimer.Interval = 1500;
                        this.kickTimer.Start();
                    }
                }
                else
                {
                    this.toConsole(2, "Threshold not yet met.");
                    this.then = DateTime.Now;
                    this.kickTimer.Stop();
                }
            }
        }

        public void kickPlayers(object source, ElapsedEventArgs e)
        {
            if (this.kickQueue.Count > 0)
            {
                this.toConsole(2, "Kicking...");
                this.ExecuteCommand("procon.protected.send", "admin.kickPlayer", kickQueue.Dequeue(), kickMessage);
            }
            else
            {
                this.toConsole(2, "All seeders kicked from server.");
                this.kickTimer.Stop();
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
            lstReturn.Add(new CPluginVariable("Settings|Kick Message", typeof(string), kickMessage));
            lstReturn.Add(new CPluginVariable("Settings|Debug Level", typeof(string), debugLevelString));
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
            else if (strVariable.Contains("Min. Time Threshold Met"))
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
        }
    }
}