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

        // Version = 0.3

        string CockpitTag = "ILS Cockpit";

        string AntennaTag = "ILS Antenna";

        string ILSAntennaChannel = "channel-ILS";

        string VORAntennaChannel = "channel-VOR";

        static double LOCFullScaleDeflectionAngle = 12;

        static double GSFullScaleDeflectionAngle = 6;

        double GSAimAngle = 8;

        static double VORFullScaleDeflectionAngle = 12;

        // DO NOT EDIT ANYTHING BELOW THIS COMMENT UNLESS YOU KNOW WHAT YOU'RE DOING!
        #endregion

        // Holds the access to the chosen storage facilitator.
        MyIni config = new MyIni();

        // Set this to false to use Storage rather than customData.
        bool useCustomData = true; // Default set to true

        bool SetupComplete = false;

        bool ShipShouldListenForILS = true;

        bool ShipHasSelectedILS = false;

        bool ShipShouldListenForVOR = true;

        bool ShipHasSelectedVOR = false;

        IMyShipController CockpitBlock;
        IMyTerminalBlock ReferenceBlock;
        IMyRadioAntenna Antenna;

        Vector3D ShipVector;

        Vector3D GravVector, GravVectorNorm, ShipVectorNorm;

        ILSDataSet ILSData;


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update1;
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
            IGC.RegisterBroadcastListener(ILSAntennaChannel);
            IGC.RegisterBroadcastListener(VORAntennaChannel);


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
                    // General Commands
                    case "reset":
                        InitializeStorage();
                        break;

                    // ILS Commands
                    case "startils":
                        ShipShouldListenForILS = true;
                        break;
                    case "stopsearchils":
                        ShipShouldListenForILS = false;
                        break;
                    case "stopils":
                        ShipHasSelectedILS = false;
                        ShipShouldListenForILS = false;
                        // ResetDefaultILSInConfig();
                        break;

                    // VOR Commands
                    case "startvor":
                        ShipShouldListenForVOR = true;
                        break;
                    case "stopsearchvor":
                        ShipShouldListenForVOR = false;
                        break;
                    case "stopvor":
                        ShipHasSelectedVOR = false;
                        ShipShouldListenForVOR = false;
                        // ResetDefaultVORInConfig();
                        break;
                }
            }

            // Update the ship and gravity vectors.
            UpdateVectors();


            // Main Logic
            if (ShipShouldListenForILS)
            {
                // If ship is connected to an ILS and is listening, another ILS closer by will override
                // the active transmitter. Normally ShipShouldListen will be false once connected.
                SearchForILSMessages();
            }

            if (ShipShouldListenForVOR)
            {
                SearchForVORMessages();
            }

            ILSData = new ILSDataSet();
            if (ShipHasSelectedILS)
            {
                ILSData = HandleILS();
            }

            VORDataSet VORData = new VORDataSet();
            if (ShipHasSelectedVOR)
            {
                VORData = HandleVOR();
            }

            DrawToSurfaces(ILSData, VORData);
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
            double Distance = CalculateShipDistanceFromVector(GSWaypointVector);

            // Localizer
            double Bearing, RBearing, Deviation, Track;
            CalculateILSLocalizer(LOCWaypointVector, RWYHDG, out Bearing, out RBearing, out Deviation, out Track);

            // @TODO Implement a safety feature that disables the LOC view when ship is more than perpendicular to the RWY HDG.

            // GlideSlope
            double GSAngle;
            CalculateILSGlideSlope(GSWaypointVector, out GSAngle);


            // LOC & G/S Instruments
            /*string LOCInstrumentString, LOCInstrumentIndicator, GSInstrumentString, GSInstrumentIndicator;
            BuildLocalizerAndGlideSlopeIndications(
                Deviation,
                GSAngle,
                out LOCInstrumentString,
                out LOCInstrumentIndicator,
                out GSInstrumentString,
                out GSInstrumentIndicator
            );*/

            double RunwayDesinator = Math.Round(RWYHDG / 10);

            return new ILSDataSet
            {
                Rotation = RWYHDG - Bearing,
                LocalizerDeviation = Deviation,
                GlideSlopeDeviation = GSAngle - GSAimAngle,
                Distance = Distance,
                RunwayNumber = RunwayDesinator,
                RunwayHeading = RWYHDG,
                Bearing = Bearing,
                RelativeBearing = RBearing,
                Track = Track
            };
        }


        public VORDataSet HandleVOR()
        {
            string VORName = config.Get("VORStation", "Name").ToString();
            Vector3D VORPosition = CreateVectorFromGPSCoordinateString(config.Get("VORStation", "Position").ToString());
            Vector3D NorthVector = CreateVectorFromGPSCoordinateString(config.Get("VORStation", "NorthVector").ToString());
            Vector3D CrossVector = CreateVectorFromGPSCoordinateString(config.Get("VORStation", "CrossVector").ToString());

            VORDataSet VORData = new VORDataSet();
            VORData.Name = VORName;


            Vector3D RadialVector = Vector3D.Negate(VORPosition) + ShipVector;
            Vector3D RRadialVector = Vector3D.Reject(RadialVector, GravVectorNorm);
            Vector3D RadialVectorNorm = Vector3D.Normalize(RRadialVector);

            double NorthAngle = ToDegrees(Math.Acos(Vector3D.Dot(RadialVectorNorm, NorthVector)));
            double CrossAngle = ToDegrees(Math.Acos(Vector3D.Dot(RadialVectorNorm, CrossVector)));
            
            double Radial = NorthAngle;
            if (CrossAngle < 90)
            {
                Radial = 360 - NorthAngle;
            }

            Echo("Radial: " + Radial);
            VORData.Radial = Radial;

            double OBS = config.Get("VORNavigation", "OBS").ToDouble();

            Vector3D Direction = Vector3D.Reject(Vector3D.Normalize(CockpitBlock.WorldMatrix.Forward), GravVectorNorm);
            double RelativeOBS = ToDegrees(Math.Acos(Vector3D.Dot(Direction, RadialVectorNorm)));

            Vector3D CrossDirection;
            if (Math.Acos(Vector3D.Dot(CockpitBlock.WorldMatrix.Down, GravVectorNorm)) < (Math.PI / 2))
            {
                CrossDirection = Vector3D.Reject(CockpitBlock.WorldMatrix.Right, GravVectorNorm);
            }
            else
            {
                CrossDirection = Vector3D.Reject(CockpitBlock.WorldMatrix.Left, GravVectorNorm);
            }

            double AngleCross = ToDegrees(Math.Acos(Vector3D.Dot(CrossDirection, RadialVectorNorm)));

            double Rotation = RelativeOBS;
            if (AngleCross > 90)
            {
                Rotation = 360 - RelativeOBS;
            }

            if (Rotation > 180)
            {
                VORData.Rotation = Rotation - 180;
            } else
            {
                VORData.Rotation = Rotation + 180;
            }

            Echo("AngleCross: " + AngleCross.ToString());
            Echo("Rotation: " + VORData.Rotation.ToString());

            VORData.Distance = CalculateShipDistanceFromVector(VORPosition);
            VORData.OBS = OBS;
            VORData.RelativeOBS = RelativeOBS;

            VORData.Deviation = 6; // TODO - Calculate this

            return VORData;
        }


        public void DrawToSurfaces(ILSDataSet ILSData, VORDataSet VORData)
        {
            List<IMyTextSurfaceProvider> SurfaceProviders = GetScreens(CockpitTag);
            if (SurfaceProviders == null)
            {
                Echo("No screen found!");
                return;
            }

            CombinedDataSet CombinedData = new CombinedDataSet
            {
                ILSData = ILSData,
                VORData = VORData
            };

            foreach (IMyTextSurfaceProvider _sp in SurfaceProviders)
            {
                IMyTerminalBlock _spTerminal = _sp as IMyTerminalBlock;
                string _customDataString = _spTerminal.CustomData;
                MyIni _customData = new MyIni();
                
                MyIniParseResult iniResult;
                if (!_customData.TryParse(_customDataString, out iniResult))
                {
                    throw new Exception(iniResult.ToString());
                }

                if (_customData.Get("NavigationSurfaces", "ILS").ToInt32(-1) == -1)
                {
                    _customData.Set("NavigationSurfaces", "ILS", "1");
                    _customData.Set("NavigationSurfaces", "VOR", "2");
                    _customData.Set("NavigationSurfaces", "Data", "3");

                    _spTerminal.CustomData = _customData.ToString();
                    continue;
                }

                // ILS Screen
                try
                {
                    IMyTextSurface ILSSurface = _sp.GetSurface(_customData.Get("NavigationSurfaces", "ILS").ToInt32());
                    Draw.DrawSurface(ILSSurface, Surface.ILS, ILSData);
                }
                catch (Exception Ex)
                {
                    Echo("No ILS Surface found in \"" + _spTerminal.CustomName.ToString() + "\".");
                    Echo(Ex.ToString());
                }


                // VOR Screen
                try
                {
                    IMyTextSurface VORSurface = _sp.GetSurface(_customData.Get("NavigationSurfaces", "VOR").ToInt32());
                    Draw.DrawSurface(VORSurface, Surface.VOR, VORData);
                }
                catch (Exception Ex)
                {
                    Echo("No VOR Surface found in \"" + _spTerminal.CustomName.ToString() + "\".");
                    Echo(Ex.ToString());
                }


                // Data Screen
                try
                {
                    IMyTextSurface DataSurface = _sp.GetSurface(_customData.Get("NavigationSurfaces", "Data").ToInt32());
                    Draw.DrawSurface(DataSurface, Surface.Data, CombinedData);
                }
                catch (Exception)
                {
                    Echo("No Data Surface found in " + _spTerminal.CustomName.ToString());
                }
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


        public MyIni ParseBroadcastedMessage(MyIGCMessage message)
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
            Echo("ILS Listeners: " + listeners.Count.ToString());
            listeners.ForEach(listener => {
                if (!listener.IsActive) return;
                if (listener.Tag != ILSAntennaChannel) return;
                if (listener.HasPendingMessage)
                {
                    ActiveILSTransmitters.Add(
                        ParseBroadcastedMessage(listener.AcceptMessage())
                    );
                }
            });

            double shortestDistance = 99999;
            MyIni selectedILSTransmitter = new MyIni();

            Echo("ILS Transmitters: " + ActiveILSTransmitters.Count.ToString());
            // Select the transmitter closest to the ship.
            ActiveILSTransmitters.ForEach(transmitter =>
            {
                string _GPS = transmitter.Get("TouchdownZone", "GPSA").ToString();
                double distance = CalculateShipDistanceFromGPSString(_GPS);

                if (distance < shortestDistance)
                {
                    selectedILSTransmitter = transmitter;
                }
            });

            if (ActiveILSTransmitters.Count == 0)
            {
                Echo("Not able to connect to any ILS transmitter signals.");
                return;
            }
            

            // Select appropriate runway from the chosen transmitter.
            string GPSA = selectedILSTransmitter.Get("TouchdownZone", "GPSA").ToString();
            string GPSB = selectedILSTransmitter.Get("TouchdownZone", "GPSB").ToString();
            string HeadingA = selectedILSTransmitter.Get("Runway", "HeadingA").ToString();
            string HeadingB = selectedILSTransmitter.Get("Runway", "HeadingB").ToString();

            double TouchdownZoneADistance = CalculateShipDistanceFromGPSString(GPSA);
            double TouchdownZoneBDistance = CalculateShipDistanceFromGPSString(GPSB);

            string ActingLocalizer, ActingGlideSlope, ActiveHeading;
            if (TouchdownZoneADistance < TouchdownZoneBDistance)
            {
                ActingGlideSlope = GPSA;
                ActingLocalizer = GPSB;
                ActiveHeading = HeadingA;
            } else
            {
                ActingGlideSlope = GPSB;
                ActingLocalizer = GPSA;
                ActiveHeading = HeadingB;
            }

            config.Set("TouchdownZone", "GPSA", GPSA);
            config.Set("TouchdownZone", "GPSB", GPSB);
            config.Set("Runway", "HeadingA", HeadingA);
            config.Set("Runway", "HeadingB", HeadingB);

            config.Set("LocalizerData", "RWYHDG", ActiveHeading);
            config.Set("LocalizerData", "GPS", ActingLocalizer);
            config.Set("GlideSlopeData", "GPS", ActingGlideSlope);

            Echo("Save Storage..");
            // OverrideStorage(selectedILSTransmitter);
            SaveStorage();


            ShipShouldListenForILS = false;
            ShipHasSelectedILS = true;
        }


        public void SearchForVORMessages()
        {
            List<MyIni> ActiveVORTransmitters = new List<MyIni>();
            List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
            IGC.GetBroadcastListeners(listeners);

            // Parse any messages from active listeners on the selected channel.
            Echo("VOR Listeners: " + listeners.Count.ToString());
            listeners.ForEach(listener => {
                if (!listener.IsActive) return;
                if (listener.Tag != VORAntennaChannel) return;
                if (listener.HasPendingMessage)
                {
                    ActiveVORTransmitters.Add(
                        ParseBroadcastedMessage(listener.AcceptMessage())
                    );
                }
            });

            double shortestDistance = 99999;
            MyIni selectedVORTransmitter = new MyIni();

            Echo("VOR Transmitters: " + ActiveVORTransmitters.Count.ToString());
            // Select the transmitter closest to the ship.
            ActiveVORTransmitters.ForEach(transmitter =>
            {
                string _GPS = transmitter.Get("Station", "Position").ToString();
                double distance = CalculateShipDistanceFromGPSString(_GPS);

                if (distance < shortestDistance)
                {
                    selectedVORTransmitter = transmitter;
                }
            });

            if (ActiveVORTransmitters.Count == 0)
            {
                Echo("Not able to connect to any VOR transmitter signals.");
                return;
            }

            string Name = selectedVORTransmitter.Get("Station", "Name").ToString();
            string Position = selectedVORTransmitter.Get("Station", "Position").ToString();
            string NorthVector = selectedVORTransmitter.Get("Station", "NorthVector").ToString();
            string CrossVector = selectedVORTransmitter.Get("Station", "CrossVector").ToString();

            Echo("Connected to VOR: " + Name);

            config.Set("VORStation", "Name", Name);
            config.Set("VORStation", "Position", Position);
            config.Set("VORStation", "NorthVector", NorthVector);
            config.Set("VORStation", "CrossVector", CrossVector);

            config.Set("VORNavigation", "OBS", 360);

            SaveStorage();

            ShipHasSelectedVOR = true;
            ShipShouldListenForVOR = false;
        }


        public void SendMessage(string message)
        {
            IGC.SendBroadcastMessage(ILSAntennaChannel, message, TransmissionDistance.TransmissionDistanceMax);
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


        public double CalculateShipDistanceFromVector(Vector3D GSWaypointVector)
        {
            return Math.Round(Vector3D.Distance(GSWaypointVector, ShipVector), 2);
        }


        public double CalculateShipDistanceFromGPSString(string GPS)
        {
            string GPSName;
            Vector3D GPSVector = CreateVectorFromGPSCoordinateString(GPS, out GPSName);
            return Math.Round(Vector3D.Distance(GPSVector, ShipVector));
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


        public void SaveStorage()
        {
            if (useCustomData)
            {
                Me.CustomData = config.ToString();
            } else
            {
                Storage = config.ToString();
            }
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


        public static Vector3D CreateVectorFromGPSCoordinateString(string gps)
        {
            string dump;
            return CreateVectorFromGPSCoordinateString(gps, out dump);
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
