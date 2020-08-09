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

        // Version = 1.2

        string CockpitTag = "Cockpit";

        string AntennaTag = "ILS Antenna";

        string ILSAntennaChannel = "channel-ILS";

        string VORAntennaChannel = "channel-VOR";

        string NDBAntennaChannel = "channel-NDB";

        static double LOCFullScaleDeflectionAngle = 12;

        static double GSFullScaleDeflectionAngle = 4;

        static double GSAimAngle = 8;

        static double VORFullScaleDeflectionAngle = 24;

        static bool HighUpdateRate = true;

        // static bool DisableBackcourseBlockage = false;

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

        bool ShipShouldListenForNDB = true;

        bool ShipHasSelectedNDB = false;

        IMyShipController CockpitBlock;
        IMyTerminalBlock ReferenceBlock;
        IMyRadioAntenna Antenna;

        Vector3D ShipVector;

        Vector3D GravVector, GravVectorNorm, ShipVectorNorm;

        ILSDataSet ILSData;


        public Program()
        {
            if (HighUpdateRate)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update1;
            } else
            {
                Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update10;
            }
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
                throw new Exception("No ILS Receiver Antenna!\nCheck naming tag.");
            }

            Antenna = (IMyRadioAntenna)antennaListReferences[0];
            IGC.RegisterBroadcastListener(ILSAntennaChannel);
            IGC.RegisterBroadcastListener(VORAntennaChannel);
            IGC.RegisterBroadcastListener(NDBAntennaChannel);


            // Mark setup as completed.
            SetupComplete = true;
            Echo("Setup complete.");
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if (!SetupComplete)
            {
                try
                {
                    Echo("Running setup.");
                    Setup();
                    InitializeStorage();
                }
                catch (Exception e)
                {
                    Echo(e.Message);
                    return;
                }
            } else
            {
                // Echo("Skipping setup");
            }

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
                    case "switchrunway":
                        SwitchILSRunway();
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
                // OBS 90
                if (argument.ToLower().StartsWith("obs "))
                {
                    string[] argPart = argument.Split(' ');
                    double OBS;
                    if (double.TryParse(argPart[1], out OBS))
                    {
                        config.Set("VORNavigation", "OBS", OBS.ToString());
                        SaveStorage();
                    }
                }

                // SurfaceToggle 0 1
                if (argument.ToLower().StartsWith("surfacetoggle ") || argument.ToLower().StartsWith("togglesurface "))
                {
                    string[] argPart = argument.Split(' ');
                    int SurfaceProviderIndex, SurfaceIndex;

                    int.TryParse(argPart[1], out SurfaceProviderIndex);
                    int.TryParse(argPart[2], out SurfaceIndex);

                    ToggleSurface(SurfaceProviderIndex, SurfaceIndex);
                }
            }

            // Update the ship and gravity vectors.
            UpdateVectors();


            // Main Logic
            if (ShipShouldListenForILS)
            {
                // If ship is connected to an ILS and is listening, another ILS closer by will override
                // the active transmitter. Normally ShipShouldListen will be false once connected.
                Echo("Is listing for ILS signals");
                SearchForILSMessages();
            }

            if (ShipShouldListenForVOR)
            {
                Echo("Is listing for VOR signals");
                SearchForVORMessages();
            }

            if (ShipShouldListenForNDB)
            {
                Echo("Is listing for NDB signals");
                SearchForNDBMessages();
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

            NDBDataSet NDBData = new NDBDataSet();
            if (ShipHasSelectedNDB)
            {
                NDBData = HandleNDB();
            }

            DrawToSurfaces(ILSData, VORData, NDBData);
        }



        public ILSDataSet HandleILS()
        {
            // Custom Data
            string LOCGPS, GSGPS;
            double RWYHDG;

            LOCGPS = config.Get("LocalizerData", "GPS").ToString();
            GSGPS = config.Get("GlideSlopeData", "GPS").ToString();
            RWYHDG = config.Get("LocalizerData", "RWYHDG").ToDouble(-1);

            Vector3D NorthVector = CreateVectorFromGPSCoordinateString(config.Get("LocalizerData", "NorthVector").ToString());
            Vector3D CrossVector = CreateVectorFromGPSCoordinateString(config.Get("LocalizerData", "CrossVector").ToString());


            // Waypoint vevtors
            string locWayPointName, gsWayPointName;
            Vector3D LOCWaypointVector = CreateVectorFromGPSCoordinateString(LOCGPS, out locWayPointName);
            Vector3D GSWaypointVector = CreateVectorFromGPSCoordinateString(GSGPS, out gsWayPointName);

            // Slant range distance
            double Distance = CalculateShipDistanceFromVector(GSWaypointVector);

            // Localizer
            Vector3D RadialVector = Vector3D.Negate(LOCWaypointVector) + ShipVector;
            Vector3D RRadialVector = Vector3D.Reject(RadialVector, GravVectorNorm);
            Vector3D RadialVectorNorm = Vector3D.Normalize(RRadialVector);

            double NorthAngle = ToDegrees(Math.Acos(Vector3D.Dot(RadialVectorNorm, NorthVector)));
            double CrossAngle = ToDegrees(Math.Acos(Vector3D.Dot(RadialVectorNorm, CrossVector)));

            double Radial = NorthAngle;
            if (CrossAngle < 90)
            {
                Radial = 360 - NorthAngle;
            }

            Vector3D ACNorthVector = Vector3D.Reject(new Vector3D(0, -1, 0), GravVectorNorm);
            Vector3D ACNorthVectorNorm = Vector3D.Normalize(ACNorthVector);

            Vector3D Direction = Vector3D.Normalize(Vector3D.Reject(Vector3D.Normalize(CockpitBlock.WorldMatrix.Forward), GravVectorNorm));
            double Heading = ToDegrees(Math.Acos(Vector3D.Dot(Direction, ACNorthVectorNorm)));

            Vector3D CrossDirection;
            if (Math.Acos(Vector3D.Dot(CockpitBlock.WorldMatrix.Down, GravVectorNorm)) < (Math.PI / 2))
            {
                CrossDirection = Vector3D.Reject(CockpitBlock.WorldMatrix.Right, GravVectorNorm);
            }
            else
            {
                CrossDirection = Vector3D.Reject(CockpitBlock.WorldMatrix.Left, GravVectorNorm);
            }

            double AngleCross = ToDegrees(Math.Acos(Vector3D.Dot(CrossDirection, ACNorthVectorNorm)));
            if (AngleCross < 90)
            {
                Heading = 360 - Heading;
            }

            double Rotation = RWYHDG - Heading - 180;

            // Deviation
            double Deviation = Radial - RWYHDG;
            Deviation *= -1;
            if (Deviation > 90)
            {
                Deviation -= 180;
                Rotation -= 180;
            }
            else if (Deviation < -90)
            {
                Deviation += 180;
                Rotation -= 180;
            }

            // GlideSlope
            double GSAngle;
            CalculateILSGlideSlope(GSWaypointVector, out GSAngle);
            double GlideSlopeDeviation = GSAngle - GSAimAngle;


            // @TODO Implement a safety feature that disables the LOC view when ship is more than perpendicular to the RWY HDG. Or maybe not?
            bool FailLocalizer = false;


            bool FailGlideSlope = false;
            if (GlideSlopeDeviation > 1.5 * GSFullScaleDeflectionAngle)
            {
                FailGlideSlope = true;
            }



            double RunwayDesinator = Math.Round(RWYHDG / 10);

            return new ILSDataSet
            {
                Rotation = Rotation,
                LocalizerDeviation = Deviation,
                GlideSlopeDeviation = GlideSlopeDeviation,
                Distance = Distance,
                RunwayNumber = RunwayDesinator,
                RunwayHeading = RWYHDG,
                FailLocalizer = FailLocalizer,
                FailGlideSlope = FailGlideSlope
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
            
            VORData.Radial = Radial;

            double OBS = config.Get("VORNavigation", "OBS").ToDouble();

            // Aircraft North Vector
            Vector3D ACNorthVector = Vector3D.Reject(new Vector3D(0, -1, 0), GravVectorNorm);
            Vector3D ACNorthVectorNorm = Vector3D.Normalize(ACNorthVector);

            Vector3D ConvVORNorthVector = Vector3D.Normalize(Vector3D.Reject(NorthVector, GravVectorNorm));
            Vector3D ConvVORCrossVector = Vector3D.Normalize(Vector3D.Reject(CrossVector, GravVectorNorm));

            double Convergence = ToDegrees(Math.Acos(Vector3D.Dot(ACNorthVectorNorm, ConvVORNorthVector)));
            double ConvergenceCross = ToDegrees(Math.Acos(Vector3D.Dot(ACNorthVectorNorm, ConvVORCrossVector)));
            
            if (ConvergenceCross < 90)
            {
                Convergence = Convergence * -1;
            }

            Vector3D Direction = Vector3D.Normalize(Vector3D.Reject(Vector3D.Normalize(CockpitBlock.WorldMatrix.Forward), GravVectorNorm));
            double Heading = ToDegrees(Math.Acos(Vector3D.Dot(Direction, ACNorthVectorNorm)));

            Vector3D CrossDirection;
            if (Math.Acos(Vector3D.Dot(CockpitBlock.WorldMatrix.Down, GravVectorNorm)) < (Math.PI / 2))
            {
                CrossDirection = Vector3D.Reject(CockpitBlock.WorldMatrix.Right, GravVectorNorm);
            }
            else
            {
                CrossDirection = Vector3D.Reject(CockpitBlock.WorldMatrix.Left, GravVectorNorm);
            }

            double AngleCross = ToDegrees(Math.Acos(Vector3D.Dot(CrossDirection, ACNorthVectorNorm)));
            if (AngleCross < 90)
            {
                Heading = 360 - Heading;
            }

            double Rotation = OBS - Heading - Convergence - 180;
            

            // Deviation
            double Deviation = Radial - OBS;
            Deviation *= -1;
            if (Deviation > 90)
            {
                Deviation -= 180;
                Rotation -= 180;
            }
            else if (Deviation < -90)
            {
                Deviation += 180;
                Rotation -= 180;
            }


            VORData.Rotation = Rotation;
            VORData.Distance = CalculateShipDistanceFromVector(VORPosition);
            VORData.OBS = OBS;
            VORData.Heading = Heading;
            VORData.Deviation = Deviation;

            return VORData;
        }


        public NDBDataSet HandleNDB()
        {
            string NDBName = config.Get("NDBStation", "Name").ToString();
            Vector3D NDBPosition = CreateVectorFromGPSCoordinateString(config.Get("NDBStation", "Position").ToString());

            NDBDataSet NDBData = new NDBDataSet();
            NDBData.Name = NDBName;

            Vector3D HeadingVector = Vector3D.Normalize(Vector3D.Reject(Vector3D.Normalize(CockpitBlock.WorldMatrix.Forward), GravVectorNorm));
            Vector3D CrossHeadingVector;
            if (Math.Acos(Vector3D.Dot(CockpitBlock.WorldMatrix.Down, GravVectorNorm)) < (Math.PI / 2))
            {
                CrossHeadingVector = Vector3D.Reject(CockpitBlock.WorldMatrix.Right, GravVectorNorm);
            }
            else
            {
                CrossHeadingVector = Vector3D.Reject(CockpitBlock.WorldMatrix.Left, GravVectorNorm);
            }

            Vector3D Direction = Vector3D.Normalize(Vector3D.Reject(NDBPosition - ShipVector, GravVectorNorm));
            double Angle = ToDegrees(Math.Acos(Vector3D.Dot(Direction, HeadingVector)));
            double CrossAngle = ToDegrees(Math.Acos(Vector3D.Dot(Direction, CrossHeadingVector)));

            if(CrossAngle > 90)
            {
                Angle = 360 - Angle;
            }

            NDBData.Rotation = Angle;
            
            return NDBData;
        }


        public void DrawToSurfaces(ILSDataSet ILSData, VORDataSet VORData, NDBDataSet NDBData)
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


                if (_customData.Get("NavigationSurfaces", "0").ToString("none") == "none")
                {
                    for (int i = 0; i < _sp.SurfaceCount; i++)
                    {
                        switch(i) {
                            case 1:
                                _customData.Set("NavigationSurfaces", i.ToString(), "ILS");
                                break;
                            case 2:
                                _customData.Set("NavigationSurfaces", i.ToString(), "VOR");
                                break;
                            case 3:
                                _customData.Set("NavigationSurfaces", i.ToString(), "Data");
                                break;
                            default:
                                _customData.Set("NavigationSurfaces", i.ToString(), "N/A");
                                break;
                        }
                    }

                    _spTerminal.CustomData = _customData.ToString();
                    continue;
                }

                string[] DrawSurfaces = new string[_sp.SurfaceCount];
                try
                {
                    for (var i = 0; i < _sp.SurfaceCount; i++)
                    {
                        string value = _customData.Get("NavigationSurfaces", i.ToString()).ToString();
                        DrawSurfaces[i] = value;
                    }
                }
                catch (Exception)
                {
                    Echo("Error in building DrawSurfaces Loop");
                }

                // ILS Screen
                try
                {
                    IMyTextSurface ILSSurface = _sp.GetSurface(Array.IndexOf(DrawSurfaces, "ILS"));
                    Draw.DrawSurface(ILSSurface, Surface.ILS, ILSData);
                }
                catch (Exception)
                {
                    Echo("No ILS Surface found in \"" + _spTerminal.CustomName.ToString() + "\".");
                }


                // VOR Screen
                try
                {
                    IMyTextSurface VORSurface = _sp.GetSurface(Array.IndexOf(DrawSurfaces, "VOR"));
                    Draw.DrawSurface(VORSurface, Surface.VOR, VORData);
                }
                catch (Exception)
                {
                    Echo("No VOR Surface found in \"" + _spTerminal.CustomName.ToString() + "\".");
                }


                // NDB Screen
                try
                {
                    IMyTextSurface VORSurface = _sp.GetSurface(Array.IndexOf(DrawSurfaces, "NDB"));
                    Draw.DrawSurface(VORSurface, Surface.NDB, NDBData);
                }
                catch (Exception)
                {
                    Echo("No NDB Surface found in \"" + _spTerminal.CustomName.ToString() + "\".");
                }


                // Data Screen
                try
                {
                    IMyTextSurface DataSurface = _sp.GetSurface(Array.IndexOf(DrawSurfaces, "Data"));
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
            List<IMyTerminalBlock> TaggedSurfaceProviders = new List<IMyTerminalBlock>();
            List<IMyTextSurfaceProvider> SurfaceProviders = new List<IMyTextSurfaceProvider>();
            GridTerminalSystem.GetBlocksOfType(SurfaceProviders);

            foreach (IMyTerminalBlock _sp in SurfaceProviders)
            {
                if (_sp.CustomName.Contains(Tag))
                {
                    TaggedSurfaceProviders.Add(_sp);
                }
            }

            if (TaggedSurfaceProviders.Count == 0)
            {
                return null;
            }

            TaggedSurfaceProviders.Sort((x, y) => string.Compare(x.CustomName, y.CustomName));
            List<IMyTextSurfaceProvider> SortedSurfaceProviders = TaggedSurfaceProviders.ConvertAll(x => (IMyTextSurfaceProvider)x);

            return SortedSurfaceProviders;
        }


        public void ToggleSurface(int SurfaceProviderIndex, int SurfaceIndex)
        {
            List<IMyTextSurfaceProvider> SurfaceProviders = GetScreens(CockpitTag);
            if (SurfaceProviders == null)
            {
                return;
            }

            IMyTextSurfaceProvider SurfaceProvider;
            try
            {
                 SurfaceProvider = SurfaceProviders[SurfaceProviderIndex];
            }
            catch (Exception)
            {
                Echo("Surface provider \"" + SurfaceProviderIndex.ToString() + "\" was not found!");
                return;
            }

            IMyTerminalBlock SurfaceProviderTerminal = SurfaceProvider as IMyTerminalBlock;
            string _customDataString = SurfaceProviderTerminal.CustomData;
            MyIni _customData = new MyIni();

            MyIniParseResult iniResult;
            if (!_customData.TryParse(_customDataString, out iniResult))
            {
                throw new Exception(iniResult.ToString());
            }

            string[] DrawSurfaces = new string[SurfaceProvider.SurfaceCount];
            try
            {
                for (var i = 0; i < SurfaceProvider.SurfaceCount; i++)
                {
                    string value = _customData.Get("NavigationSurfaces", i.ToString()).ToString();
                    DrawSurfaces[i] = value;
                }
            }
            catch (Exception)
            {
                Echo("Error in building DrawSurfaces Loop");
            }

            string[] DisplayServices = { "ILS", "VOR", "NDB", "Data", "N/A" };

            string CurrentValue;
            try
            {
                CurrentValue = DrawSurfaces[SurfaceIndex];
            }
            catch (Exception)
            {
                Echo("Surface does not exists in the surface provider."); // TODO Add index values to output!
                return;
            }

            int CurrentValueServiceIndex;
            try
            {
                CurrentValueServiceIndex = Array.IndexOf(DisplayServices, CurrentValue);
            }
            catch (Exception)
            {
                Echo("\"" + CurrentValue + "\" is not a valid value for a surface!");
                return;
            }

            int NextServiceIndex = CurrentValueServiceIndex + 1;
            if (NextServiceIndex >= DisplayServices.Count())
            {
                NextServiceIndex = 0;
            }

            string NextService = DisplayServices[NextServiceIndex];

            // Reset the two values after the recursive check.
            NextServiceIndex = RecursiveServiceDetermination(DisplayServices, DrawSurfaces, NextServiceIndex, NextService);
            NextService = DisplayServices[NextServiceIndex];

            Echo("Next Service: " + NextService);

            _customData.Set("NavigationSurfaces", SurfaceIndex.ToString(), NextService);
            SurfaceProviderTerminal.CustomData = _customData.ToString();

            if (NextService == "N/A")
            {
                IMyTextSurface TextSurface = SurfaceProvider.GetSurface(SurfaceIndex);
                Draw.ResetSurface(TextSurface);
            }
        }


        private int RecursiveServiceDetermination(string[] DisplayServices, string[] DrawSurfaces, int NextServiceIndex, string NextService)
        {
            if (Array.IndexOf(DrawSurfaces, NextService) >= 0 && NextService != "N/A")
            {
                NextServiceIndex++;
                if (NextServiceIndex >= DisplayServices.Count())
                {
                    NextServiceIndex = 0;
                }
                NextService = DisplayServices[NextServiceIndex];

                return RecursiveServiceDetermination(DisplayServices, DrawSurfaces, NextServiceIndex, NextService);
            } else
            {
                return NextServiceIndex;
            }
        }


        public void SwitchILSRunway()
        {
            string ActiveRunway = config.Get("LocalizerData", "RWYHDG").ToString();
            string RunwayHDGA = config.Get("Runway", "HeadingA").ToString();
            string RunwayHDGB = config.Get("Runway", "HeadingB").ToString();

            string GPSA = config.Get("TouchdownZone", "GPSA").ToString();
            string GPSB = config.Get("TouchdownZone", "GPSB").ToString();

            string NewRunwayHDG;
            string NewGPS;

            if (ActiveRunway == RunwayHDGA)
            {
                NewRunwayHDG = RunwayHDGB;
                NewGPS = GPSB;
            } else
            {
                NewRunwayHDG = RunwayHDGA;
                NewGPS = GPSA;
            }

            config.Set("LocalizerData", "RWYHDG", NewRunwayHDG);
            config.Set("LocalizerData", "GPS", NewGPS);
        }


        public void WriteToScreens(List<IMyTerminalBlock> panels, string[] lines)
        {
            panels.ForEach(panel =>
            {
                // Call via WriteToScreens(list, new {"a", "b", "c"}))
            });
        }
        

        public MyIni ParseBroadcastedMessage(MyIGCMessage message)
        {
            long sender = message.Source;

            Echo("Message received with tag" + message.Tag + "\n\r");

            MyIni iniMessage = new MyIni();
            MyIniParseResult iniResult;
            if (!iniMessage.TryParse(message.Data.ToString(), out iniResult))
            {
                Echo("Failed to parse data. Data: " + message.Data.ToString());
                throw new Exception(iniResult.ToString());
            }

            return iniMessage;
        }


        public void SearchForILSMessages()
        {
            List<MyIni> ActiveILSTransmitters = new List<MyIni>();
            List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
            IGC.GetBroadcastListeners(listeners);

            // Parse any messages from active listeners on the selected channel.
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
            string NorthVector = selectedILSTransmitter.Get("Station", "NorthVector").ToString();
            string CrossVector = selectedILSTransmitter.Get("Station", "CrossVector").ToString();

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

            config.Set("LocalizerData", "NorthVector", NorthVector);
            config.Set("LocalizerData", "CrossVector", CrossVector);

            config.Set("LocalizerData", "RWYHDG", ActiveHeading);
            config.Set("LocalizerData", "GPS", ActingLocalizer);
            config.Set("GlideSlopeData", "GPS", ActingGlideSlope);
            
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

            config.Set("VORNavigation", "OBS", config.Get("VORNavigation", "OBS").ToDouble(360));

            SaveStorage();

            ShipHasSelectedVOR = true;
            ShipShouldListenForVOR = false;
        }


        public void SearchForNDBMessages()
        {
            List<MyIni> ActiveNDBTransmitters = new List<MyIni>();
            List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
            IGC.GetBroadcastListeners(listeners);

            // Parse any messages from active listeners on the selected channel.
            listeners.ForEach(listener => {
                if (!listener.IsActive) return;
                if (listener.Tag != NDBAntennaChannel) return;
                if (listener.HasPendingMessage)
                {
                    ActiveNDBTransmitters.Add(
                        ParseBroadcastedMessage(listener.AcceptMessage())
                    );
                }
            });

            double shortestDistance = 99999;
            MyIni selectedNDBTransmitter = new MyIni();

            // Select the transmitter closest to the ship.
            ActiveNDBTransmitters.ForEach(transmitter =>
            {
                string _GPS = transmitter.Get("Station", "Position").ToString();
                double distance = CalculateShipDistanceFromGPSString(_GPS);

                if (distance < shortestDistance)
                {
                    selectedNDBTransmitter = transmitter;
                }
            });

            if (ActiveNDBTransmitters.Count == 0)
            {
                Echo("Not able to connect to any NDB transmitter signals.");
                return;
            }

            string Name = selectedNDBTransmitter.Get("Station", "Name").ToString();
            string Position = selectedNDBTransmitter.Get("Station", "Position").ToString();

            Echo("Connected to NDB: " + Name);

            config.Set("NDBStation", "Name", Name);
            config.Set("NDBStation", "Position", Position);

            SaveStorage();

            ShipHasSelectedNDB = true;
            ShipShouldListenForNDB = false;
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
