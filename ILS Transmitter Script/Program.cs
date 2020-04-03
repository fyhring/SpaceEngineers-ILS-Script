using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        #region mdk preserve
        // YOU CAN EDIT THESE VALUES IF YOU WANT TO.
        string AntennaName = "ILS Station Antenna";

        string antennaChannel = "channel-ILS";

        // DO NOT EDIT ANYTHING BELOW THIS COMMENT UNLESS YOU KNOW WHAT YOU'RE DOING!
        #endregion

        bool SetupComplete = false;

        IMyRadioAntenna Antenna;

        MyIni config = new MyIni();


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;
        }


        public void Setup()
        {
            List<IMyTerminalBlock> listReferences = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(AntennaName, listReferences);
            Antenna = (IMyRadioAntenna)listReferences[0];

            if (Antenna == null)
            {
                throw new Exception("Setup failed. No antenna found.");
            }
            
            // Is it necessary to listen? This should probably only transmit, not receive..
            // IGC.RegisterBroadcastListener(antennaChannel);

            Echo("Setup complete.");
            SetupComplete = true;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!SetupComplete)
            {
                Setup();
            }

            // Init storage
            InitializeStorage();


            // Set default data if 
            if (!IsDataFormatValid())
            {
                SetDefaultData();
                return;
            }


            // Validate GPS strings.
            bool IsLocalizerGPSValid  = ValidateGPSFormat(config.Get("LocalizerData", "GPS").ToString());
            bool IsGlideslopeGPSValid = ValidateGPSFormat(config.Get("GlideSlopeData", "GPS").ToString());

            if (!IsLocalizerGPSValid || !IsGlideslopeGPSValid)
            {
                throw new Exception("Please enter the runway data or see the guide for help.");
            }


            // Construct & send message.
            string message = config.ToString();
            IGC.SendBroadcastMessage(antennaChannel, message);
        }


        public bool ValidateGPSFormat(string gps)
        {
            string[] splitCoord = gps.Split(':');
            return splitCoord.Length >= 5;
        }


        public void InitializeStorage()
        {
            MyIniParseResult iniResult;
            if (!config.TryParse(Me.CustomData, out iniResult))
            {
                throw new Exception(iniResult.ToString());
            }
        }


        public bool IsDataFormatValid()
        {
            string gps = config.Get("LocalizerData", "GPS").ToString("none");
            if (gps == "none")
            {
                Echo("No data set, creating template in Custom Data.");
                return false;
            }

            return true;
        }


        public void SetDefaultData()
        {
            config.Set("LocalizerData", "RWYHDG", 0);
            config.Set("LocalizerData", "GPS", "N/A");
            config.Set("GlideSlopeData", "GPS", "N/A");
            Me.CustomData = config.ToString();
        }
    }
}
