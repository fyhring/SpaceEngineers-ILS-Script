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

        string CockpitTag = "Cockpit";

        string antennaChannel = "channel-ILS";

        // DO NOT EDIT ANYTHING BELOW THIS COMMENT UNLESS YOU KNOW WHAT YOU'RE DOING!
        #endregion

        bool SetupComplete = false;

        MyIni config = new MyIni();

        IMyRadioAntenna Antenna;

        IMyShipController CockpitBlock;


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
            bool IsTouchdownZoneAValid = ValidateGPSFormat(config.Get("TouchdownZone", "GPSA").ToString());
            bool IsTouchdownZoneBValid = ValidateGPSFormat(config.Get("TouchdownZone", "GPSB").ToString());

            if (!IsTouchdownZoneAValid || !IsTouchdownZoneBValid)
            {
                throw new Exception("Please enter the runway data or see the guide for help.");
            }

            int RunwayHDGA = config.Get("Runway", "HeadingA").ToInt16();
            int RunwayHDGB = config.Get("Runway", "HeadingB").ToInt16();
            if (RunwayHDGA == -1 || RunwayHDGB == -1)
            {
                FindCockpitBlock();
                DetectRunwayHeadings();
                return;
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
            string gps = config.Get("TouchdownZone", "GPSA").ToString("none");
            if (gps == "none")
            {
                Echo("No data set, creating template in Custom Data.");
                return false;
            }

            return true;
        }


        public void SetDefaultData()
        {
            config.Set("Runway", "HeadingA", -1);
            config.Set("Runway", "HeadingB", -1);
            config.Set("TouchdownZone", "GPSA", "N/A");
            config.Set("TouchdownZone", "GPSB", "N/A");
            Me.CustomData = config.ToString();
        }


        public void DetectRunwayHeadings()
        {
            string ZoneAString = config.Get("TouchdownZone", "GPSA").ToString();
            string ZoneBString = config.Get("TouchdownZone", "GPSB").ToString();

            string ZoneAName;
            Vector3D ZoneA = CreateVectorFromGPSCoordinateString(ZoneAString, out ZoneAName);

            string ZoneBName;
            Vector3D ZoneB = CreateVectorFromGPSCoordinateString(ZoneBString, out ZoneBName);

            Vector3D ResultA = (ZoneA * -1) + ZoneB;
            Vector3D ResultB = ResultA * -1;
            
            Vector3D ResultANorm = Vector3D.Normalize(ResultA);
            Vector3D ResultBNorm = Vector3D.Normalize(ResultB);

            //

            Vector3D GravVector = CockpitBlock.GetNaturalGravity();
            Vector3D GravVectorNorm = Vector3D.Normalize(GravVector);
            
            Vector3D VectorNorth = Vector3D.Reject(new Vector3D(0, -1, 0), GravVectorNorm);
            // Vector3D RVectorNorth = Vector3D.Reject(LOCWaypointNorm, GravVectorNorm);
            Vector3D VectorNorthNorm = Vector3D.Normalize(VectorNorth);

            Vector3D VectorEast = Vector3D.Reject(new Vector3D(1, 0, 0), GravVectorNorm);
            // Vector3D RVectorEast = Vector3D.Reject(..., GravVectorNorm);
            Vector3D VectorEastNorm = Vector3D.Normalize(VectorEast);
            
            double DotNorthA = Vector3D.Dot(ResultANorm, VectorNorthNorm);
            double DotNorthB = Vector3D.Dot(ResultBNorm, VectorNorthNorm);

            double AngleNorthA = ToDegrees(Math.Acos(DotNorthA));
            double AngleNorthB = ToDegrees(Math.Acos(DotNorthB));

            double DotEastA = Vector3D.Dot(ResultANorm, VectorEastNorm);
            double DotEastB = Vector3D.Dot(ResultBNorm, VectorEastNorm);

            double AngleEastA = ToDegrees(Math.Acos(DotEastA));
            double AngleEastB = ToDegrees(Math.Acos(DotEastB));

            Echo("AngleNorthA: " + (AngleNorthA).ToString());
            Echo("AngleNorthB: " + (AngleNorthB).ToString());
            Echo("AngleEastA: " + (AngleEastA).ToString());
            Echo("AngleEastB: " + (AngleEastB).ToString());

            double HDGA = AngleNorthA;
            if (AngleEastA > 90)
            {
                HDGA = 360 - AngleNorthA;
            }

            double HDGB = AngleNorthB;
            if (AngleEastB > 90)
            {
                HDGB = 360 - AngleNorthB;
            }

            HDGA = Math.Round(HDGA);
            HDGB = Math.Round(HDGB);

            config.Set("Runway", "HeadingA", HDGA.ToString());
            config.Set("Runway", "HeadingB", HDGB.ToString());
            Me.CustomData = config.ToString();
        }


        public void FindCockpitBlock()
        {
            List<IMyTerminalBlock> cockpitListReferences = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(CockpitTag, cockpitListReferences);
            if (cockpitListReferences.Count == 0)
            {
                throw new Exception("No cockpit found! Check the naming tag.");
            }

            CockpitBlock = (IMyShipController)cockpitListReferences[0];
        }


        public static Vector3D CreateVectorFromGPSCoordinateString(string gps, out string name)
        {
            string[] splitCoord = gps.Split(':');

            if (splitCoord.Length < 5)
            {
                throw new Exception("Error: GPS coordinate " + gps + " could not be understod\nPlease input coordinates in the form\nGPS:[Name of waypoint]:[x]:[y]:[z]:");
            }

            name = splitCoord[1];

            Vector3D vector = new Vector3D();
            vector.X = StringToDouble(splitCoord[2]);
            vector.Y = StringToDouble(splitCoord[3]);
            vector.Z = StringToDouble(splitCoord[4]);

            return vector;
        }


        public static double StringToDouble(string value)
        {
            double n;
            bool isDouble = double.TryParse(value, out n);
            if (isDouble)
            {
                return n;
            }
            else
            {
                return 0;
            }
        }


        public double ToDegrees(double angle)
        {
            return angle / Math.PI * 180;
        }
    }
}
