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
        string AntennaTag = "VOR Station Antenna";

        string CockpitTag = "Cockpit";

        string AntennaChannel = "channel-VOR";

        // DO NOT EDIT ANYTHING BELOW THIS COMMENT UNLESS YOU KNOW WHAT YOU'RE DOING!
        #endregion


        MyIni config = new MyIni();

        IMyShipController CockpitBlock;

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

            Echo("Broadcasting on \""+ AntennaChannel +"\"..");
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
            string gps = config.Get("Station", "NorthVector").ToString("none");
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
            Vector3D NorthVector = FindNorthVector();
            Vector3D CrossVector = FindCrossVector(NorthVector);

            string PositionGPS = ConvertVector3DToGPS(Position);
            string NorthVectorGPS = ConvertVector3DToGPS(NorthVector);
            string CrossVectorGPS = ConvertVector3DToGPS(CrossVector);

            config.Set("Station", "Name", "Sector XX");
            config.Set("Station", "Position", PositionGPS);
            config.Set("Station", "NorthVector", NorthVectorGPS);
            config.Set("Station", "CrossVector", CrossVectorGPS);

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

            return "GPS:VOR" + _position + ":";
        }


        public Vector3D FindPosition()
        {
            IMyRadioAntenna Antenna = FindAntennaBlock();
            return Antenna.GetPosition();
        }


        public Vector3D FindNorthVector()
        {
            IMyShipController CockpitBlock = FindCockpitBlock();
            Vector3D GravVector = CockpitBlock.GetNaturalGravity();
            Vector3D GravVectorNorm = Vector3D.Normalize(GravVector);

            Vector3D NorthVector = Vector3D.Reject(new Vector3D(0, -1, 0), GravVectorNorm);
            Vector3D NorthVectorNorm = Vector3D.Normalize(NorthVector);
            // Echo("DotN: " + Vector3D.Dot(NorthVector, GravVectorNorm));
            // Echo("DotNN: " + Vector3D.Dot(NorthVectorNorm, GravVectorNorm));

            return NorthVectorNorm;
        }


        public Vector3D FindCrossVector(Vector3D NorthVectorNorm)
        {
            IMyShipController CockpitBlock = FindCockpitBlock();
            Vector3D GravVector = CockpitBlock.GetNaturalGravity();
            Vector3D GravVectorNorm = Vector3D.Normalize(GravVector);

            return Vector3D.Cross(NorthVectorNorm, GravVectorNorm);
        }


        public IMyShipController FindCockpitBlock()
        {
            if (CockpitBlock == null)
            {
                List<IMyTerminalBlock> cockpitListReferences = new List<IMyTerminalBlock>();
                GridTerminalSystem.SearchBlocksOfName(CockpitTag, cockpitListReferences);
                if (cockpitListReferences.Count == 0)
                {
                    throw new Exception("No cockpit found! Check the naming tag.");
                }

                CockpitBlock = (IMyShipController)cockpitListReferences[0];
            }

            return CockpitBlock;
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
