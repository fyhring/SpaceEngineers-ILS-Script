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
        public class ILSDataSet
        {
            public double Rotation { get; set; }

            private double _localizerDeviation;
            public double LocalizerDeviation {
                get =>_localizerDeviation;
                set {
                    if (value > 12)
                    {
                        _localizerDeviation = 12;
                    }
                    else if (value < -12)
                    {
                        _localizerDeviation = -12;
                    } else
                    {
                        _localizerDeviation = value;
                    }

                }
            }

            public double GlideSlopeDeviation { get; set; }

            public double Distance { get; set; }

            public double RunwayNumber { get; set; }
        }
    }
}
