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
        string AntennaTag = "NDB Antenna";

        string AntennaChannel = "channel-NDB";

        // DO NOT EDIT ANYTHING BELOW THIS COMMENT UNLESS YOU KNOW WHAT YOU'RE DOING!
        #endregion


        MyIni config = new MyIni();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            // Init storage
            InitializeStorage();


            // Set default data if 
            if (!IsDataFormatValid() || argument == "reset")
            {
                SetDefaultData();
                return;
            }


            // Construct & send message.
            string message = config.ToString();
            IGC.SendBroadcastMessage(AntennaChannel, message);

            Echo("Broadcasting on \"" + AntennaChannel + "\"..");
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
            string gps = config.Get("Station", "Position").ToString("none");
            if (gps == "none")
            {
                Echo("No data set, creating positions in Custom Data.");
                return false;
            }

            return true;
        }


        public void SetDefaultData()
        {

            Vector3D Position = FindPosition();
            string PositionGPS = ConvertVector3DToGPS(Position);

            config.Set("Station", "Name", "NDB XX");
            config.Set("Station", "Position", PositionGPS);

            Me.CustomData = config.ToString();
        }


        public string ConvertVector3DToGPS(Vector3D Position)
        {
            // Turning this:
            // X:-0.0706388473865866 Y:-0.996187825944044 Z:-0.0511856296315009
            // Into this:
            // GPS:VOR:48282.5704107185:-5272.59389249216:35177.8783341206:
            string _position = Position.ToString();
            _position = _position
                .Replace(" ", "")
                .Replace("X", "")
                .Replace("Y", "")
                .Replace("Z", "");

            return "GPS:NDB" + _position + ":";
        }


        public Vector3D FindPosition()
        {
            IMyRadioAntenna Antenna = FindAntennaBlock();
            return Antenna.GetPosition();
        }

        
        public IMyRadioAntenna FindAntennaBlock()
        {
            List<IMyTerminalBlock> AntennaListReferences = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(AntennaTag, AntennaListReferences);
            if (AntennaListReferences.Count == 0)
            {
                throw new Exception("No antenna found! Check the naming tag.");
            }

            return (IMyRadioAntenna)AntennaListReferences[0];
        }
    }
}
