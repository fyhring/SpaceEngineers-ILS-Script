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
        public class VORDataSet : IDataSet
        {
            public double? Rotation { get; set; }
            public double Radial { get; set; }

            public string Name { get; set; }
            public double Distance { get; set; }

            public double OBS { get; set; }
            public double Heading { get; set; }

            private double _deviation;
            public double Deviation
            {
                get { return _deviation; }
                set
                {

                    if (value > VORFullScaleDeflectionAngle)
                    {
                        _deviation = VORFullScaleDeflectionAngle;
                    }
                    else if (value < (VORFullScaleDeflectionAngle * -1))
                    {
                        _deviation = VORFullScaleDeflectionAngle * -1;
                    }
                    else
                    {
                        _deviation = value;
                    }

                }
            }
        }
    }
}
