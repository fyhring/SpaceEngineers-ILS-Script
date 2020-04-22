﻿using Sandbox.Game.EntityComponents;
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

        double Version = 0.2;

        string CockpitTag = "ILS Cockpit";

        string AntennaTag = "ILS Antenna";

        string AntennaChannel = "channel-ILS";

        static double LOCFullScaleDeflectionAngle = 12;

        static double GSFullScaleDeflectionAngle = 12;

        double GSAimAngle = 10;

        // DO NOT EDIT ANYTHING BELOW THIS COMMENT UNLESS YOU KNOW WHAT YOU'RE DOING!
        #endregion

        // Holds the access to the chosen storage facilitator.
        MyIni config = new MyIni();

        // Set this to false to use Storage rather than customData.
        bool useCustomData = true; // Default set to true

        bool SetupComplete = false;

        bool ShipShouldListen = true;

        bool ShipHasSelectedILS = false;

        int SurfaceIndex = 1;

        IMyShipController CockpitBlock;
        IMyTerminalBlock ReferenceBlock;
        IMyRadioAntenna Antenna;

        Vector3D ShipVector;

        Vector3D GravVector, GravVectorNorm, ShipVectorNorm;

        List<IMyTerminalBlock> LCDScreens;

        ILSDataSet ILSData;


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update10;
        }


        public void Setup()
        {
            // Cockpit
            List<IMyTerminalBlock> cockpitListReferences = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(CockpitTag, cockpitListReferences);
            if (cockpitListReferences.Count == 0)
            {
                throw new Exception("No cockpit found! Check the naming tag.");
            }

            CockpitBlock = (IMyShipController)cockpitListReferences[0];
            ReferenceBlock = cockpitListReferences[0];

            // Antenna
            List<IMyTerminalBlock> antennaListReferences = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(AntennaTag, antennaListReferences);

            if (antennaListReferences.Count == 0)
            {
                throw new Exception("No ILS Receiver Antenna! Check naming tag.");
            }

            Antenna = (IMyRadioAntenna)antennaListReferences[0];
            IGC.RegisterBroadcastListener(AntennaChannel);


            // Mark setup as completed.
            SetupComplete = true;
            Echo("Setup complete.");
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if (!SetupComplete)
            {
                Echo("Running setup.");
                Setup();
                InitializeStorage();
            } else
            {
                Echo("Skipping setup");
            }


            /*
                User Commands:
                - Update
                - Listen (even while connected to an ILS)
                - StopListening
                - Disconnect (also runs StopListening)
            */
            if (!argument.Equals(""))
            {
                switch (argument.ToLower())
                {
                    case "update":
                        InitializeStorage();
                        break;
                    case "listen":
                        ShipShouldListen = true;
                        break;
                    case "stoplistening":
                        ShipShouldListen = false;
                        break;
                    case "disconnect":
                        ShipHasSelectedILS = false;
                        ShipShouldListen = false;
                        break;
                }
            }

            // Update the ship and gravity vectors.
            UpdateVectors();


            // Main Logic
            if (!ShipHasSelectedILS)
            {
                Echo("Not connected");
            }
            else
            {
                Echo("Is connected!");
            }

            Echo("ShipShouldListen: " + ShipShouldListen.ToString());

            if (ShipShouldListen)
            {
                // If ship is connected to an ILS and is listening, another ILS closer by will override
                // the active transmitter. Normally ShipShouldListen will be false once connected.
                SearchForILSMessages();
                return;
            }

            
            if (ShipHasSelectedILS)
            {
                ILSData = HandleILS();
            }

            /*VORDataSet VORData;
            if (ShipHasSelectedVOR)
            {
                VORData = HandleVOR();
            }*/

            DrawToSurfaces(ILSData);
        }



        public ILSDataSet HandleILS()
        {
            // Custom Data
            string LOCGPS, GSGPS;
            double RWYHDG;
            Echo("Getting data");

            LOCGPS = config.Get("LocalizerData", "GPS").ToString();
            GSGPS = config.Get("GlideSlopeData", "GPS").ToString();
            RWYHDG = config.Get("LocalizerData", "RWYHDG").ToDouble(-1);


            // Waypoint vevtors
            string locWayPointName, gsWayPointName;
            Vector3D LOCWaypointVector = CreateVectorFromGPSCoordinateString(LOCGPS, out locWayPointName);
            Vector3D GSWaypointVector = CreateVectorFromGPSCoordinateString(GSGPS, out gsWayPointName);

            // Slant range distance
            double Distance = CalculateILSDistance(GSWaypointVector);

            // Localizer
            double Bearing, RBearing, Deviation, Track;
            CalculateILSLocalizer(LOCWaypointVector, RWYHDG, out Bearing, out RBearing, out Deviation, out Track);

            // @TODO Implement a safety feature that disables the LOC view when ship is more than perpendicular to the RWY HDG.

            // GlideSlope
            double GSAngle;
            CalculateILSGlideSlope(GSWaypointVector, out GSAngle);


            // LOC & G/S Instruments
            string LOCInstrumentString, LOCInstrumentIndicator, GSInstrumentString, GSInstrumentIndicator;
            BuildLocalizerAndGlideSlopeIndications(
                Deviation,
                GSAngle,
                out LOCInstrumentString,
                out LOCInstrumentIndicator,
                out GSInstrumentString,
                out GSInstrumentIndicator
            );

            double RunwayDesinator = Math.Round(RWYHDG / 10);

            return new ILSDataSet
            {
                Rotation = RWYHDG - Bearing,
                LocalizerDeviation = Deviation,
                GlideSlopeDeviation = GSAngle,
                Distance = Distance,
                RunwayNumber = RunwayDesinator
            };
        }


        public void DrawToSurfaces(ILSDataSet ILSData)
        {
            List<IMyTextSurfaceProvider> SurfaceProviders = GetScreens(CockpitTag);
            if (SurfaceProviders == null)
            {
                Echo("No screen found!");
                return;
            }

            foreach (IMyTextSurfaceProvider _sp in SurfaceProviders)
            {
                IMyTextSurface Surface = _sp.GetSurface(SurfaceIndex);
                Draw.DrawSurface(Surface, ILSData);
            }
        }


        public List<IMyTextSurfaceProvider> GetScreens(string Tag)
        {
            List<IMyTextSurfaceProvider> TaggedSurfaceProviders = new List<IMyTextSurfaceProvider>();
            List<IMyTextSurfaceProvider> SurfaceProviders = new List<IMyTextSurfaceProvider>();
            GridTerminalSystem.GetBlocksOfType(SurfaceProviders);

            foreach (IMyTextSurfaceProvider _sp in SurfaceProviders)
            {
                if ((_sp as IMyTerminalBlock).CustomName.Contains(Tag))
                {
                    TaggedSurfaceProviders.Add(_sp);
                }
            }

            if (TaggedSurfaceProviders.Count == 0)
            {
                return null;
            }

            return TaggedSurfaceProviders;
        }


        public void WriteToScreens(List<IMyTerminalBlock> panels, string[] lines)
        {
            panels.ForEach(panel =>
            {
                // Call via WriteToScreens(list, new {"a", "b", "c"}))
            });
        }


        public void BuildLocalizerAndGlideSlopeIndications(
            double Deviation,
            double Angle,
            out string LOCInstrumentString,
            out string LOCInstrumentIndicator,
            out string GSInstrumentString,
            out string GSInstrumentIndicator
        )
        {
            LOCInstrumentString = "*-----*-----|-----*-----*";
            string stdLOCInstrumentIndicator = "------------------------^------------------------";

            try
            {
                LOCInstrumentIndicator = stdLOCInstrumentIndicator.Substring((int)Deviation + 12, 25);
            }
            catch (Exception)
            {
                if (Deviation > 12)
                {
                    LOCInstrumentIndicator = "^------------------------";
                }
                else if (Deviation < -12)
                {
                    LOCInstrumentIndicator = "------------------------^";
                }
                else
                {
                    LOCInstrumentIndicator = "-----------???-----------";
                }
            }

            GSInstrumentString = "*-----*-----|-----*-----*";
            string stdGSInstrumentIndicator = "------------------------^------------------------";

            try
            {
                GSInstrumentIndicator = stdGSInstrumentIndicator.Substring((int)Angle - (int)GSAimAngle + 12, 25);
            }
            catch (Exception)
            {
                if (Angle > 12)
                {
                    GSInstrumentIndicator = "^------------------------";
                }
                else if (Angle < -12)
                {
                    GSInstrumentIndicator = "------------------------^";
                }
                else
                {
                    GSInstrumentIndicator = "-----------???-----------";
                }
            }
        }


        public MyIni ParseBroadcastedILSMessage(MyIGCMessage message)
        {
            long sender = message.Source;

            Echo("Message received with tag" + message.Tag + "\n\r");
            Echo("from address " + sender.ToString() + ": \n\r");

            MyIni iniMessage = new MyIni();
            MyIniParseResult iniResult;
            if (!iniMessage.TryParse(message.Data.ToString(), out iniResult))
            {
                Echo("Failed to parse data. Data: " + message.Data.ToString());
                throw new Exception(iniResult.ToString());
            }

            return iniMessage;
        }


        public void TriggerUpdate()
        {
            Echo("Trigger update..");
        }


        public void SearchForILSMessages()
        {
            List<MyIni> ActiveILSTransmitters = new List<MyIni>();
            List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
            IGC.GetBroadcastListeners(listeners);

            // Parse any messages from active listeners on the selected channel.
            Echo("Listeners: " + listeners.Count.ToString());
            listeners.ForEach(listener => {
                if (!listener.IsActive) return;
                if (listener.Tag != AntennaChannel) return;
                if (listener.HasPendingMessage)
                {
                    ActiveILSTransmitters.Add(
                        ParseBroadcastedILSMessage(listener.AcceptMessage())
                    );
                }
            });

            double shortestDistance = 99999;
            MyIni selectedILSTransmitter = new MyIni();

            Echo("Transmitters: " + ActiveILSTransmitters.Count.ToString());
            // Select the transmitter closest to the ship.
            ActiveILSTransmitters.ForEach(transmitter =>
            {
                string _gsGPS = transmitter.Get("GlideSlopeData", "GPS").ToString();
                string _gsName;
                Vector3D _gsWaypointVector = CreateVectorFromGPSCoordinateString(_gsGPS, out _gsName);
                double distance = Math.Round(Vector3D.Distance(_gsWaypointVector, ShipVector), 2);
                if (distance < shortestDistance)
                {
                    selectedILSTransmitter = transmitter;
                }
            });

            if (ActiveILSTransmitters.Count != 0)
            {
                Echo("Override Storage..");
                OverrideStorage(selectedILSTransmitter);
                ShipShouldListen = false;
                ShipHasSelectedILS = true;
                TriggerUpdate();
            }
        }


        public void SendMessage(string message)
        {
            IGC.SendBroadcastMessage(AntennaChannel, message, TransmissionDistance.TransmissionDistanceMax);
        }


        public void UpdateVectors()
        {
            GravVector = CockpitBlock.GetNaturalGravity();
            GravVectorNorm = Vector3D.Normalize(GravVector);

            ShipVector = ReferenceBlock.GetPosition();
            ShipVectorNorm = Vector3D.Normalize(ShipVector);
        }


        public void CalculateILSLocalizer(
            Vector3D LOCWaypointVector,
            double RWYHDG,
            out double Bearing,
            out double RBearing,
            out double Deviation,
            out double Track
        )
        {

            Vector3D RForwardVector, VectorNorth, RVectorNorth, LOCWaypointNorm, LReject;
            LOCWaypointNorm = Vector3D.Normalize(LOCWaypointVector);

            // Localizer
            RForwardVector = Vector3D.Reject(CockpitBlock.WorldMatrix.Forward, LOCWaypointNorm);
            VectorNorth = Vector3D.Reject(new Vector3D(0, -1, 0), GravVectorNorm);
            RVectorNorth = Vector3D.Reject(LOCWaypointNorm, GravVectorNorm);
            Bearing = Math.Acos(Vector3D.Dot(Vector3D.Normalize(RForwardVector), Vector3D.Normalize(VectorNorth))) * 180 / Math.PI;
            RBearing = Math.Acos(Vector3D.Dot(Vector3D.Normalize(RForwardVector), Vector3D.Normalize(RVectorNorth))) * 180 / Math.PI;

            if (Math.Acos(Vector3D.Dot(CockpitBlock.WorldMatrix.Down, GravVectorNorm)) < (Math.PI / 2))
            {
                LReject = Vector3D.Reject(CockpitBlock.WorldMatrix.Right, GravVectorNorm);
            }
            else
            {
                LReject = Vector3D.Reject(CockpitBlock.WorldMatrix.Left, GravVectorNorm);
            }

            Vector3D projection = Vector3D.ProjectOnVector(ref LReject, ref LOCWaypointNorm);
            Vector3D projectionNorm = Vector3D.Normalize(projection);

            if (LReject.GetDim(1) <= 0)
            {
                Bearing = 360 - Bearing;
            }

            if (projectionNorm.GetDim(0) < 0)
            {
                RBearing = 0 - RBearing;
            }

            Track = Bearing + RBearing;
            if (Track > 360)
            {
                Track -= 360;
            }
            if (Track < 0)
            {
                Track += 360;
            }

            Deviation = RWYHDG - Track;
        }


        public void CalculateILSGlideSlope(Vector3D GSWaypointVector, out double Angle)
        {
            Vector3D GSNegatedShipVector = Vector3D.Negate(ShipVector);
            Vector3D GSResultantVector = GSWaypointVector + GSNegatedShipVector;
            Vector3D GSResultantVectorNorm = Vector3D.Normalize(GSResultantVector);

            Angle = 90 - Math.Acos(Vector3D.Dot(GSResultantVectorNorm, GravVectorNorm)) * 180 / Math.PI;
        }
        

        public double CalculateILSDistance(Vector3D GSWaypointVector)
        {
            return Math.Round(Vector3D.Distance(GSWaypointVector, ShipVector), 2);
        }


        public void InitializeStorage()
        {
            Echo("Setting up storage..");
            if (useCustomData == true)
            {
                MyIniParseResult iniResult;
                if (!config.TryParse(Me.CustomData, out iniResult))
                {
                    Echo("Try setting useCustomData to false.");
                    throw new Exception(iniResult.ToString());
                }
            }
            else
            {
                config.TryParse(Storage);
            }

            double __RWYHDG = config.Get("LocalizerData", "RWYHDG").ToDouble(-1);
            if (__RWYHDG.Equals(-1))
            {
                config.Set("LocalizerData", "RWYHDG", 0);
                config.Set("LocalizerData", "GPS", "N/A");
                config.Set("GlideSlopeData", "GPS", "N/A");

                if (useCustomData == true)
                {
                    Me.CustomData = config.ToString();
                }
                else
                {
                    Storage = config.ToString();
                }
            }

            Echo("Storage setup complete.");
        }


        public void OverrideStorage(MyIni config)
        {
            if (useCustomData == true)
            {
                Me.CustomData = config.ToString();
            }
            else
            {
                Storage = config.ToString();
            }

            InitializeStorage();
        }


        public static Vector3D CreateVectorFromGPSCoordinateString(string gps, out string name)
        {
            string[] splitCoord = gps.Split(':');

            if (splitCoord.Length < 5)
            {
                throw new Exception("Error: GPS coordinate " + gps + " could not be understod\nPlease input coordinates in the form\nGPS:[Name of waypoint]:[x]:[y]:[z]:");
            }

            name = splitCoord[1];

            Vector3D vector;
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

    }
}
