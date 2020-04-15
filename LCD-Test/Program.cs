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

            // WriteTextToScreens(LCDScreens, new string[]{"Hello", "world!", "", "This is a new line!"});

            
            Draw(LCDScreens[0]);
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

        public void Draw(IMyTerminalBlock panel)
        {
            _drawingSurface = panel as IMyTextSurface;
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
            Vector2 Size = new Vector2(Surface.SurfaceSize.X, Surface.SurfaceSize.X);
            Vector2 Center = new Vector2(Surface.TextureSize.X / 2, Surface.TextureSize.Y / 2);

            // Create background sprite
            var sprite = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "Grid",
                Position = _viewport.Center,
                Size = _viewport.Size,
                Color = Color.White.Alpha(0.66f),
                Alignment = TextAlignment.CENTER,
                RotationOrScale = (45 * (float)Math.PI / 180)
            };
            // Add the sprite to the frame
            frame.Add(sprite);

            // Circle
            MySprite Circle1 = MySprite.CreateSprite("Circle", Center, Size * 0.96f);
            Circle1.Color = Color.Aqua;
            frame.Add(Circle1);

            // Set up the initial position - and remember to add our viewport offset
            var position = new Vector2(256, 20) + _viewport.Position;

            // Create our first line
            sprite = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = "Line 1",
                Position = position,
                RotationOrScale = 1.0f /* 80 % of the font's default size */,
                Color = Color.Red,
                Alignment = TextAlignment.CENTER /* Center the text on the position */,
                FontId = "White"
            };
            // Add the sprite to the frame
            frame.Add(sprite);

            // Move our position 20 pixels down in the viewport for the next line
            position += new Vector2(0, 20);

            // Create our second line, we'll just reuse our previous sprite variable - this is not necessary, just
            // a simplification in this case.
            sprite = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = "Line 1",
                Position = position,
                RotationOrScale = 0.8f,
                Color = Color.Blue,
                Alignment = TextAlignment.CENTER,
                FontId = "White"
            };
            // Add the sprite to the frame
            frame.Add(sprite);
        }
    }
}
