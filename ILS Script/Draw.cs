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
        public class Draw
        {
            static Color CockpitBGColor;
            static Color CockpitFGColor;

            static IMyTextSurface DrawingSurface;
            static RectangleF Viewport;


            public static void DrawSurface(IMyTextSurface surface, Surface SurfaceType, ILSDataSet Data /*, VORDataSet VORData*/)
            {
                DrawingSurface = surface;

                CockpitBGColor = DrawingSurface.ScriptBackgroundColor;
                CockpitFGColor = DrawingSurface.ScriptForegroundColor;

                PrepareTextSurfaceForSprites(DrawingSurface);

                // Calculate viewport offset
                Viewport = new RectangleF(
                    (DrawingSurface.TextureSize - DrawingSurface.SurfaceSize) / 2f,
                    DrawingSurface.SurfaceSize
                );

                var frame = DrawingSurface.DrawFrame();

                switch (SurfaceType)
                {
                    case Surface.ILS:
                        DrawILS(ref frame, DrawingSurface, Data);
                        break;

                    case Surface.Data:
                        DrawDataScreen(ref frame, DrawingSurface, Data);
                        break;
                }

                frame.Dispose();
            }

            private static void PrepareTextSurfaceForSprites(IMyTextSurface textSurface)
            {
                // Set the sprite display mode
                textSurface.ContentType = ContentType.SCRIPT;
                // Make sure no built-in script has been selected
                textSurface.Script = "";
            }

            private static void DrawILS(ref MySpriteDrawFrame frame, IMyTextSurface Surface, ILSDataSet ILSData)
            {
                Vector2 Size = new Vector2(Surface.SurfaceSize.Y, Surface.SurfaceSize.Y);
                Vector2 Center = new Vector2(Surface.TextureSize.X / 2, Surface.TextureSize.Y / 2);
                Center -= new Vector2(Surface.TextureSize.X / 100 * 10, 0);

                float UnitX = Size.X / 256f;
                float UnitY = Size.Y / 256f;


                if (ILSData.Rotation == null)
                {
                    MySprite NoDataSprite = new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = "No ILS Data.",
                        Position = Center,
                        RotationOrScale = 0.7f,
                        Color = Color.White,
                        Alignment = TextAlignment.CENTER,
                        FontId = "White"
                    };
                    frame.Add(NoDataSprite);

                    return;
                }


                // Vars
                float CircleSize = Size.Y * 0.95f;
                float ArrowLength = Size.Y * 0.8f;

                float ArrowRotation = (float) ILSData.Rotation;
                float Deviation = (float) ILSData.LocalizerDeviation; // between -12 and 12.


                // Circle
                MySprite Circle1 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize));
                Circle1.Color = CockpitFGColor.Alpha(1f);
                frame.Add(Circle1);

                MySprite Circle2 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize) * 0.95f);
                Circle2.Color = CockpitBGColor.Alpha(1f);
                frame.Add(Circle2);

                // Arrow
                MySprite ArrowBody = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, ArrowLength)); // new Vector2(10 * UnitX, 60 * UnitY)
                ArrowBody.Color = Color.LawnGreen.Alpha(1f);
                ArrowBody.RotationOrScale = ToRadian(ArrowRotation);
                frame.Add(ArrowBody);

                float AConstant = ArrowLength / 2.1f;
                float Ax = (float)Math.Sin(ToRadian(ArrowRotation)) * AConstant;
                float Ay = (float)Math.Cos(ToRadian(ArrowRotation)) * AConstant * -1;

                MySprite ArrowHead = MySprite.CreateSprite("Triangle", Center + new Vector2(Ax, Ay), Size * 0.2f);
                ArrowHead.Color = Color.LawnGreen.Alpha(1f);
                ArrowHead.RotationOrScale = ToRadian(ArrowRotation);
                frame.Add(ArrowHead);


                // Deviation bar
                float DConstant = Deviation / (float) LOCFullScaleDeflectionAngle * (Size.Y * 0.4f);
                float Dx = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DConstant * -1;
                float Dy = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DConstant;

                MySprite DeviationBarMask = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, ArrowLength / 2.7f));
                DeviationBarMask.Color = CockpitBGColor.Alpha(1f);
                DeviationBarMask.RotationOrScale = ToRadian(ArrowRotation);
                frame.Add(DeviationBarMask);

                MySprite DeviationBar = MySprite.CreateSprite("SquareSimple", Center + new Vector2(Dx, Dy), new Vector2(12 * UnitX, ArrowLength / 3));
                DeviationBar.Color = Color.LawnGreen.Alpha(1f);
                DeviationBar.RotationOrScale = ToRadian(ArrowRotation);
                frame.Add(DeviationBar);



                // Localizer Deviation Scale
                float DSM2 = -1.0f * (Size.Y * 0.4f);
                float DSM1 = -0.5f * (Size.Y * 0.4f);
                float DSP1 = 0.5f * (Size.Y * 0.4f);
                float DSP2 = 1.0f * (Size.Y * 0.4f);
                

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


                // GlideSlope
                Vector2 GSCenter = new Vector2(Size.X * 1.3f, Center.Y);
                Vector2 GSM2 = new Vector2(GSCenter.X, GSCenter.Y - Size.Y * 0.4f);
                Vector2 GSM1 = new Vector2(GSCenter.X, GSCenter.Y - Size.Y * 0.2f);
                Vector2 GSP1 = new Vector2(GSCenter.X, GSCenter.Y + Size.Y * 0.2f);
                Vector2 GSP2 = new Vector2(GSCenter.X, GSCenter.Y + Size.Y * 0.4f);

                float DeviationUnits = (float) ILSData.GlideSlopeDeviation / (float)GSFullScaleDeflectionAngle * Size.Y * 0.4f;
                Vector2 GSDiamondPos = new Vector2(GSCenter.X, GSCenter.Y + DeviationUnits);


                MySprite GDSM2Sprite = MySprite.CreateSprite("Circle", GSM2, new Vector2(CircleSize, CircleSize) * 0.1f);
                MySprite GDSM1Sprite = MySprite.CreateSprite("Circle", GSM1, new Vector2(CircleSize, CircleSize) * 0.1f);
                MySprite GDSP1Sprite = MySprite.CreateSprite("Circle", GSP1, new Vector2(CircleSize, CircleSize) * 0.1f);
                MySprite GDSP2Sprite = MySprite.CreateSprite("Circle", GSP2, new Vector2(CircleSize, CircleSize) * 0.1f);

                MySprite GDSCSprite = MySprite.CreateSprite("SquareSimple", GSCenter, new Vector2(CircleSize * 0.2f, UnitY * 6));
                MySprite GSDiamond = MySprite.CreateSprite("SquareSimple", GSDiamondPos, new Vector2(UnitX * 25, UnitY * 25));

                GDSCSprite.Color = CockpitFGColor.Alpha(1f);
                GSDiamond.Color = Color.LawnGreen.Alpha(1f);
                GSDiamond.RotationOrScale = ToRadian(45f);

                GDSM2Sprite.Color = CockpitFGColor.Alpha(1f);
                GDSM1Sprite.Color = CockpitFGColor.Alpha(1f);
                GDSP1Sprite.Color = CockpitFGColor.Alpha(1f);
                GDSP2Sprite.Color = CockpitFGColor.Alpha(1f);

                frame.Add(GDSM2Sprite);
                frame.Add(GDSM1Sprite);
                frame.Add(GDSCSprite);
                frame.Add(GDSP1Sprite);
                frame.Add(GDSP2Sprite);

                frame.Add(GSDiamond);
            }

            private static void DrawDataScreen(ref MySpriteDrawFrame frame, IMyTextSurface Surface, ILSDataSet ILSData /*, VORDataSet */)
            {

                // Labels
                /*MySprite LabelRWYHDG = new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = "RWY:\n" + ILSData.RunwayNumber.ToString(),
                    Position = new Vector2(12 * UnitX, 6 * UnitY) + Viewport.Position,
                    RotationOrScale = 0.6f,
                    Color = Color.White,
                    Alignment = TextAlignment.LEFT,
                    FontId = "White"
                };
                frame.Add(LabelRWYHDG);


                MySprite LabelILSDME = new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = "DME:\n" + Math.Round(ILSData.Distance, 2).ToString(),
                    Position = new Vector2(0, 200 * UnitY) + Viewport.Position,
                    RotationOrScale = 0.6f,
                    Color = Color.White,
                    Alignment = TextAlignment.LEFT,
                    FontId = "White"
                };
                frame.Add(LabelILSDME);*/
            }

            private static float ToRadian(float degree)
            {
                return degree * ((float)Math.PI / 180);
            }
        }
    }
}
