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
    partial class Program
    {
        public class ILSDataSet : IDataSet
        {
            public double? Rotation { get; set; }

            private double _localizerDeviation;
            public double LocalizerDeviation {
                get { return _localizerDeviation; }
                set {
                    if (value > 180)
                    {
                        value -= 360;
                    }

                    if (value > LOCFullScaleDeflectionAngle)
                    {
                        _localizerDeviation = LOCFullScaleDeflectionAngle;
                    }
                    else if (value < (LOCFullScaleDeflectionAngle * -1))
                    {
                        _localizerDeviation = LOCFullScaleDeflectionAngle * -1;
                    }
                    else
                    {
                        _localizerDeviation = value;
                    }

                }
            }

            private double _glideSlopeDeviation;
            public double GlideSlopeDeviation {
                get { return _glideSlopeDeviation; }
                set
                {
                    if (value > GSFullScaleDeflectionAngle)
                    {
                        _glideSlopeDeviation = GSFullScaleDeflectionAngle;
                    }
                    else if (value < (GSFullScaleDeflectionAngle * -1))
                    {
                        _glideSlopeDeviation = GSFullScaleDeflectionAngle * -1;
                    }
                    else
                    {
                        _glideSlopeDeviation = value;
                    }
                }
            }

            public double Distance { get; set; }

            public double RunwayNumber { get; set; }

            public double RunwayHeading { get; set; }

            public double Bearing { get; set; }

            public double RelativeBearing { get; set; }

            public double Track { get; set; }
        }
    }
}
