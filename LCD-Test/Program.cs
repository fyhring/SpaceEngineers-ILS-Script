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

        string ScreenTag = "Draw";
        List<IMyTerminalBlock> LCDScreens;

        bool SetupIsCompleted = false;
        int SurfaceIndex = 1;

        int rotation = 360;

        Color CockpitBGColor;
        Color CockpitFGColor;

        IMyTextSurface _drawingSurface;

        RectangleF _viewport;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update10;
        }

        public void Setup()
        {
            LCDScreens = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(ScreenTag, LCDScreens);
            if (LCDScreens.Count == 0)
            {
                throw new Exception("No Main LCD found!");
            }

            SetupIsCompleted = true;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!SetupIsCompleted)
            {
                Setup();
            }

            if (argument != "")
            {
                switch (argument)
                {
                    case "next":
                        SurfaceIndex++;
                        break;
                    case "prev":
                        SurfaceIndex--;
                        break;
                    default:
                        break;
                }
            }

            rotation++;

            if (rotation > 360)
            {
                rotation -= 360;
            }

            Echo("SurfaceIndex: " + SurfaceIndex.ToString());

            // WriteTextToScreens(LCDScreens, new string[]{"Hello", "world!", "", "This is a new line!"});
            // Draw(LCDScreens[0]);

            List<IMyTextSurfaceProvider> SurfaceProviders = GetScreens(ScreenTag);
            if (SurfaceProviders == null)
            {
                Echo("No screen found!");
                return;
            }

            foreach (IMyTextSurfaceProvider _sp in SurfaceProviders)
            {
                IMyTextSurface surface = _sp.GetSurface(SurfaceIndex);

                // Get user colors
                // IMyTextSurface _defaultSurface = _sp.GetSurface(0);
                CockpitBGColor = surface.ScriptBackgroundColor;
                CockpitFGColor = surface.ScriptForegroundColor;
                /*CockpitFGColor = new Color(1f, 1f, 1f);
                CockpitBGColor = new Color(1f, 0f, 0f);*/

                Draw(surface);
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

        public void WriteTextToScreens(List<IMyTerminalBlock> panels, string[] lines)
        {
            // Call via WriteToScreens(list, new {"a", "b", "c"}))
            string content = string.Join("\n", lines);

            panels.ForEach(panel =>
            {
                (panel as IMyTextPanel).WriteText(content);
            });
        }

        public void PrepareTextSurfaceForSprites(IMyTextSurface textSurface)
        {
            // Set the sprite display mode
            textSurface.ContentType = ContentType.SCRIPT;
            // Make sure no built-in script has been selected
            textSurface.Script = "";
        }

        public void Draw(IMyTextSurface surface)
        {
            _drawingSurface = surface;
            PrepareTextSurfaceForSprites(_drawingSurface);

            // Calculate viewport offset
            _viewport = new RectangleF(
                (_drawingSurface.TextureSize - _drawingSurface.SurfaceSize) / 2f,
                _drawingSurface.SurfaceSize
            );

            var frame = _drawingSurface.DrawFrame();
            DrawSprites(ref frame, _drawingSurface);
            frame.Dispose();
        }

        public void DrawSprites(ref MySpriteDrawFrame frame, IMyTextSurface Surface)
        {
            Vector2 Size = new Vector2(Surface.SurfaceSize.Y, Surface.SurfaceSize.Y);
            Vector2 Center = new Vector2(Surface.TextureSize.X / 2, Surface.TextureSize.Y / 2);
            float UnitX = Size.X / 256f;
            float UnitY = Size.Y / 256f;


            // Vars
            float CircleSize = Size.Y * 0.95f;
            float ArrowLength = Size.Y * 0.8f;

            float ArrowRotation = rotation;
            float Deviation = -5f; // between -12 and 12.


            Echo("BG: " + CockpitBGColor.ToString());
            Echo("FG: " + CockpitFGColor.ToString());


            // Set up the initial position - and remember to add our viewport offset
            // var position = new Vector2(128 * UnitX, 64 * UnitY) + _viewport.Position;

            // Circle
            MySprite Circle1 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize));
            Circle1.Color = CockpitFGColor.Alpha(1f);
            frame.Add(Circle1);

            MySprite Circle2 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize) * 0.95f);
            Circle2.Color = CockpitBGColor.Alpha(1f);
            frame.Add(Circle2);

            // Arrow
            MySprite ArrowBody = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, ArrowLength)); // new Vector2(10 * UnitX, 60 * UnitY)
            ArrowBody.Color = CockpitFGColor.Alpha(1f);
            ArrowBody.RotationOrScale = ToRadian(ArrowRotation);
            frame.Add(ArrowBody);

            float AConstant = ArrowLength / 2.1f;
            float Ax = (float)Math.Sin(ToRadian(ArrowRotation)) * AConstant;
            float Ay = (float)Math.Cos(ToRadian(ArrowRotation)) * AConstant * -1;

            MySprite ArrowHead = MySprite.CreateSprite("Triangle", Center + new Vector2(Ax, Ay), Size * 0.2f);
            ArrowHead.Color = CockpitFGColor.Alpha(1f);
            ArrowHead.RotationOrScale = ToRadian(ArrowRotation);
            frame.Add(ArrowHead);


            // Deviation bar
            float DConstant = Deviation / 12 * (Size.Y * 0.4f);
            float Dx = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DConstant * -1;
            float Dy = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DConstant;

            MySprite DeviationBar = MySprite.CreateSprite("SquareSimple", Center + new Vector2(Dx, Dy), new Vector2(12 * UnitX, ArrowLength / 3));
            DeviationBar.Color = CockpitFGColor.Alpha(1f);
            DeviationBar.RotationOrScale = ToRadian(ArrowRotation);
            frame.Add(DeviationBar);

            

            // Deviation Scale
            float DSM2 = -1.0f * (Size.Y * 0.4f);
            float DSM1 = -0.5f * (Size.Y * 0.4f);
            float DSP1 = 0.5f * (Size.Y * 0.4f);
            float DSP2 = 1.0f * (Size.Y * 0.4f);

            Echo("DSM1: " + DSM1.ToString());
            Echo("DSM2: " + DSM2.ToString());
            Echo("DSP1: " + DSP1.ToString());
            Echo("DSP2: " + DSP2.ToString());

            float DSM2x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSM2 * -1;
            float DSM2y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSM2;

            float DSM1x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSM1 * -1;
            float DSM1y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSM1;

            float DSP1x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSP1 * -1;
            float DSP1y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSP1;

            float DSP2x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSP2 * -1;
            float DSP2y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSP2;

            MySprite DSM2Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSM2x, DSM2y), new Vector2(CircleSize, CircleSize) * 0.1f);
            MySprite DSM1Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSM1x, DSM1y), new Vector2(CircleSize, CircleSize) * 0.1f);
            MySprite DSCSprite = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize) * 0.1f);
            MySprite DSP1Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSP1x, DSP1y), new Vector2(CircleSize, CircleSize) * 0.1f);
            MySprite DSP2Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSP2x, DSP2y), new Vector2(CircleSize, CircleSize) * 0.1f);

            DSM2Sprite.Color = CockpitFGColor.Alpha(1f);
            DSM1Sprite.Color = CockpitFGColor.Alpha(1f);
            DSCSprite.Color = CockpitFGColor.Alpha(1f);
            DSP1Sprite.Color = CockpitFGColor.Alpha(1f);
            DSP2Sprite.Color = CockpitFGColor.Alpha(1f);

            frame.Add(DSM2Sprite);
            frame.Add(DSM1Sprite);
            frame.Add(DSCSprite);
            frame.Add(DSP1Sprite);
            frame.Add(DSP2Sprite);
        }

        private float ToRadian(float degree)
        {
            return degree * ((float)Math.PI / 180);
        }
    }
}
