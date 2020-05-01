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

            static Vector2 Size;
            static Vector2 Center;

            static float UnitX;
            static float UnitY;

            static readonly float DataScreenFontSize = 0.48f;

            static Vector2 CenterSub;


            public static void DrawSurface(IMyTextSurface surface, Surface SurfaceType, IDataSet Data)
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

                var Frame = DrawingSurface.DrawFrame();

                Size = new Vector2(DrawingSurface.SurfaceSize.Y, DrawingSurface.SurfaceSize.Y);
                Center = new Vector2(DrawingSurface.TextureSize.X / 2, DrawingSurface.TextureSize.Y / 2);
                CenterSub = new Vector2(DrawingSurface.TextureSize.X / 100 * 10, 0);

                UnitX = Size.X / 256f;
                UnitY = Size.Y / 256f;

                switch (SurfaceType)
                {
                    case Surface.ILS:
                        DrawILS(ref Frame, DrawingSurface, Data as ILSDataSet);
                        break;

                    case Surface.VOR:
                        DrawVOR(ref Frame, DrawingSurface, Data as VORDataSet);
                        break;

                    case Surface.NDB:
                        DrawNDB(ref Frame, DrawingSurface, Data as NDBDataSet);
                        break;

                    case Surface.Data:
                        DrawDataScreen(ref Frame, DrawingSurface, Data as CombinedDataSet);
                        break;
                }

                Frame.Dispose();
            }


            private static void PrepareTextSurfaceForSprites(IMyTextSurface TextSurface)
            {
                // Set the sprite display mode
                TextSurface.ContentType = ContentType.SCRIPT;
                // Make sure no built-in script has been selected
                TextSurface.Script = "";
            }


            public static void ResetSurface(IMyTextSurface TextSurface)
            {
                var Frame = TextSurface.DrawFrame();
                Size = new Vector2(DrawingSurface.SurfaceSize.Y, DrawingSurface.SurfaceSize.Y);
                Center = new Vector2(DrawingSurface.TextureSize.Y / 2, DrawingSurface.TextureSize.Y / 2);

                UnitX = Size.X / 256f;
                UnitY = Size.Y / 256f;

                DrawCross(ref Frame);
                Frame.Dispose();
            }


            private static void DrawILS(ref MySpriteDrawFrame Frame, IMyTextSurface Surface, ILSDataSet ILSData)
            {
                if (ILSData.Rotation == null)
                {
                    DrawCross(ref Frame);
                    return;
                }


                // Vars
                float CircleSize = Size.Y * 0.95f;
                float ArrowLength = Size.Y * 0.8f;

                float ArrowRotation = (float) ILSData.Rotation;
                float Deviation = (float) ILSData.LocalizerDeviation; // between -12 and 12.

                // Re-position the Center position a bit offset in order to accomodate glideslope indicator.
                Center -= CenterSub;

                // Circle
                MySprite Circle1 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize));
                Circle1.Color = CockpitFGColor.Alpha(1f);
                Frame.Add(Circle1);

                MySprite Circle2 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize) * 0.95f);
                Circle2.Color = CockpitBGColor.Alpha(1f);
                Frame.Add(Circle2);

                // Arrow
                MySprite ArrowBody = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, ArrowLength)); // new Vector2(10 * UnitX, 60 * UnitY)
                ArrowBody.Color = Color.LawnGreen.Alpha(1f);
                ArrowBody.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(ArrowBody);

                float AConstant = ArrowLength / 2.1f;
                float Ax = (float)Math.Sin(ToRadian(ArrowRotation)) * AConstant;
                float Ay = (float)Math.Cos(ToRadian(ArrowRotation)) * AConstant * -1;

                MySprite ArrowHead = MySprite.CreateSprite("Triangle", Center + new Vector2(Ax, Ay), Size * 0.2f);
                ArrowHead.Color = Color.LawnGreen.Alpha(1f);
                ArrowHead.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(ArrowHead);


                // Deviation bar
                float DConstant = Deviation / (float) LOCFullScaleDeflectionAngle * (Size.Y * 0.4f);
                float Dx = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DConstant * -1;
                float Dy = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DConstant;

                MySprite DeviationBarMask = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, ArrowLength / 2.7f));
                DeviationBarMask.Color = CockpitBGColor.Alpha(1f);
                DeviationBarMask.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(DeviationBarMask);

                MySprite DeviationBar = MySprite.CreateSprite("SquareSimple", Center + new Vector2(Dx, Dy), new Vector2(12 * UnitX, ArrowLength / 3));
                DeviationBar.Color = Color.LawnGreen.Alpha(1f);
                DeviationBar.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(DeviationBar);



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

                Frame.Add(DSM2Sprite);
                Frame.Add(DSM1Sprite);
                Frame.Add(DSCSprite);
                Frame.Add(DSP1Sprite);
                Frame.Add(DSP2Sprite);


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

                Frame.Add(GDSM2Sprite);
                Frame.Add(GDSM1Sprite);
                Frame.Add(GDSCSprite);
                Frame.Add(GDSP1Sprite);
                Frame.Add(GDSP2Sprite);

                Frame.Add(GSDiamond);

                // Re-center the center position.
                Center += CenterSub;
            }


            private static void DrawVOR(ref MySpriteDrawFrame Frame, IMyTextSurface Surface, VORDataSet VORData)
            {
                if (VORData.Rotation == null)
                {
                    DrawCross(ref Frame);
                    return;
                }

                // Vars
                float CircleSize = Size.Y * 0.95f;
                float ArrowLength = Size.Y * 0.8f;

                float ArrowRotation = (float)VORData.Rotation;
                float Deviation = (float)VORData.Deviation;


                // Circle
                MySprite Circle1 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize));
                Circle1.Color = CockpitFGColor.Alpha(1f);
                Frame.Add(Circle1);

                MySprite Circle2 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize) * 0.95f);
                Circle2.Color = CockpitBGColor.Alpha(1f);
                Frame.Add(Circle2);

                // Arrow
                MySprite ArrowBody = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, ArrowLength));
                ArrowBody.Color = Color.LawnGreen.Alpha(1f);
                ArrowBody.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(ArrowBody);

                float AConstant = ArrowLength / 2.1f;
                float Ax = (float)Math.Sin(ToRadian(ArrowRotation)) * AConstant;
                float Ay = (float)Math.Cos(ToRadian(ArrowRotation)) * AConstant * -1;

                MySprite ArrowHead = MySprite.CreateSprite("Triangle", Center + new Vector2(Ax, Ay), Size * 0.2f);
                ArrowHead.Color = Color.LawnGreen.Alpha(1f);
                ArrowHead.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(ArrowHead);


                // Deviation bar
                float DConstant = Deviation / (float)VORFullScaleDeflectionAngle * (Size.Y * 0.4f);
                float Dx = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DConstant * -1;
                float Dy = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DConstant;

                MySprite DeviationBarMask = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, ArrowLength / 2.7f));
                DeviationBarMask.Color = CockpitBGColor.Alpha(1f);
                DeviationBarMask.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(DeviationBarMask);

                MySprite DeviationBar = MySprite.CreateSprite("SquareSimple", Center + new Vector2(Dx, Dy), new Vector2(12 * UnitX, ArrowLength / 3));
                DeviationBar.Color = Color.LawnGreen.Alpha(1f);
                DeviationBar.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(DeviationBar);


                // VOR Deviation Scale
                float DSM4 = -1.0f * (Size.Y * 0.4f);
                float DSM3 = -0.75f * (Size.Y * 0.4f);
                float DSM2 = -0.5f * (Size.Y * 0.4f);
                float DSM1 = -0.25f * (Size.Y * 0.4f);
                float DSP1 = 0.25f * (Size.Y * 0.4f);
                float DSP2 = 0.5f * (Size.Y * 0.4f);
                float DSP3 = 0.75f * (Size.Y * 0.4f);
                float DSP4 = 1.0f * (Size.Y * 0.4f);


                float DSM4x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSM4 * -1;
                float DSM4y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSM4;

                float DSM3x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSM3 * -1;
                float DSM3y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSM3;

                float DSM2x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSM2 * -1;
                float DSM2y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSM2;

                float DSM1x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSM1 * -1;
                float DSM1y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSM1;

                float DSP1x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSP1 * -1;
                float DSP1y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSP1;

                float DSP2x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSP2 * -1;
                float DSP2y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSP2;

                float DSP3x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSP3 * -1;
                float DSP3y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSP3;

                float DSP4x = (float)Math.Sin(ToRadian(ArrowRotation + 90)) * DSP4 * -1;
                float DSP4y = (float)Math.Cos(ToRadian(ArrowRotation + 90)) * DSP4;

                MySprite DSM4Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSM4x, DSM4y), new Vector2(CircleSize, CircleSize) * 0.05f);
                MySprite DSM3Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSM3x, DSM3y), new Vector2(CircleSize, CircleSize) * 0.05f);
                MySprite DSM2Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSM2x, DSM2y), new Vector2(CircleSize, CircleSize) * 0.05f);
                MySprite DSM1Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSM1x, DSM1y), new Vector2(CircleSize, CircleSize) * 0.05f);
                MySprite DSCSprite = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize) * 0.1f);
                MySprite DSP1Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSP1x, DSP1y), new Vector2(CircleSize, CircleSize) * 0.05f);
                MySprite DSP2Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSP2x, DSP2y), new Vector2(CircleSize, CircleSize) * 0.05f);
                MySprite DSP3Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSP3x, DSP3y), new Vector2(CircleSize, CircleSize) * 0.05f);
                MySprite DSP4Sprite = MySprite.CreateSprite("Circle", Center + new Vector2(DSP4x, DSP4y), new Vector2(CircleSize, CircleSize) * 0.05f);

                DSM4Sprite.Color = CockpitFGColor.Alpha(1f);
                DSM3Sprite.Color = CockpitFGColor.Alpha(1f);
                DSM2Sprite.Color = CockpitFGColor.Alpha(1f);
                DSM1Sprite.Color = CockpitFGColor.Alpha(1f);
                DSCSprite.Color = CockpitFGColor.Alpha(1f);
                DSP1Sprite.Color = CockpitFGColor.Alpha(1f);
                DSP2Sprite.Color = CockpitFGColor.Alpha(1f);
                DSP3Sprite.Color = CockpitFGColor.Alpha(1f);
                DSP4Sprite.Color = CockpitFGColor.Alpha(1f);

                Frame.Add(DSM4Sprite);
                Frame.Add(DSM3Sprite);
                Frame.Add(DSM2Sprite);
                Frame.Add(DSM1Sprite);
                Frame.Add(DSCSprite);
                Frame.Add(DSP1Sprite);
                Frame.Add(DSP2Sprite);
                Frame.Add(DSP3Sprite);
                Frame.Add(DSP4Sprite);

            }


            private static void DrawNDB(ref MySpriteDrawFrame Frame, IMyTextSurface Surface, NDBDataSet NDBData)
            {
                if (NDBData.Rotation == null)
                {
                    DrawCross(ref Frame);
                    return;
                }

                // Vars
                float CircleSize = Size.Y * 0.95f;
                float ArrowLength = Size.Y * 0.8f;
                float ArrowRotation = (float)NDBData.Rotation;

                // Circle
                MySprite Circle1 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize));
                Circle1.Color = CockpitFGColor.Alpha(1f);
                Frame.Add(Circle1);

                MySprite Circle2 = MySprite.CreateSprite("Circle", Center, new Vector2(CircleSize, CircleSize) * 0.95f);
                Circle2.Color = CockpitBGColor.Alpha(1f);
                Frame.Add(Circle2);

                // Arrow
                MySprite ArrowBody = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, ArrowLength));
                ArrowBody.Color = Color.LawnGreen.Alpha(1f);
                ArrowBody.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(ArrowBody);

                float AConstant = ArrowLength / 2.1f;
                float Ax = (float)Math.Sin(ToRadian(ArrowRotation)) * AConstant;
                float Ay = (float)Math.Cos(ToRadian(ArrowRotation)) * AConstant * -1;

                MySprite ArrowHead = MySprite.CreateSprite("Triangle", Center + new Vector2(Ax, Ay), Size * 0.2f);
                ArrowHead.Color = Color.LawnGreen.Alpha(1f);
                ArrowHead.RotationOrScale = ToRadian(ArrowRotation);
                Frame.Add(ArrowHead);
            }


            private static void DrawDataScreen(ref MySpriteDrawFrame Frame, IMyTextSurface Surface, CombinedDataSet CombinedData)
            {
                // ILS Data
                DrawDataScreenILSSection(ref Frame, Surface, CombinedData);

                // VOR 
                DrawDataScreenVORSection(ref Frame, Surface, CombinedData);

                // Line between the two columns
                MySprite SeparationLine = MySprite.CreateSprite("SquareSimple", Center, new Vector2(3 * UnitX, 200 * UnitY));
                SeparationLine.Color = CockpitFGColor.Alpha(1f);
                Frame.Add(SeparationLine);
            }


            private static void DrawDataScreenILSSection(ref MySpriteDrawFrame Frame, IMyTextSurface Surface, CombinedDataSet CombinedData)
            {
                float PositionYFactor = 5f;
                float PosistionX = 12 * UnitX;

                if (CombinedData.ILSData.Rotation == null)
                {
                    Frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = "No ILS\nConnection!",
                        Position = new Vector2(PosistionX, PositionYFactor * UnitY) + Viewport.Position,
                        RotationOrScale = DataScreenFontSize + 0.2f,
                        Color = Color.Tomato,
                        Alignment = TextAlignment.LEFT,
                        FontId = "White"
                    });
                    return;
                }

                string[] _lines = {
                    "RWY: " + CombinedData.ILSData.RunwayNumber.ToString(),
                    "DME: " + Math.Round(CombinedData.ILSData.Distance / 1000, 1).ToString(),
                    "Bearing: "+ Math.Round(CombinedData.ILSData.Bearing, 0).ToString(),
                    "Rel. Bearing: "+ Math.Round(CombinedData.ILSData.RelativeBearing, 0).ToString(),
                    "Track: "+ Math.Round(CombinedData.ILSData.Track, 0).ToString(),
                };

                List<string> Lines = new List<string>(_lines);

                foreach (string line in Lines)
                {
                    Frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = line,
                        Position = new Vector2(PosistionX, PositionYFactor * UnitY) + Viewport.Position,
                        RotationOrScale = DataScreenFontSize,
                        Color = CockpitFGColor.Alpha(1f),
                        Alignment = TextAlignment.LEFT,
                        FontId = "White"
                    });

                    PositionYFactor += 35;
                }
            }


            private static void DrawDataScreenVORSection(ref MySpriteDrawFrame Frame, IMyTextSurface Surface, CombinedDataSet CombinedData)
            {
                float PositionYFactor = 5f;
                float PosistionX = Center.X + 12 * UnitX;

                if (CombinedData.VORData.Rotation == null)
                {
                    Frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = "No VOR\nConnection!",
                        Position = new Vector2(PosistionX, PositionYFactor * UnitY) + Viewport.Position,
                        RotationOrScale = DataScreenFontSize + 0.2f,
                        Color = Color.Tomato,
                        Alignment = TextAlignment.LEFT,
                        FontId = "White"
                    });
                    return;
                }

                string[] _lines = {
                    "VOR: " + CombinedData.VORData.Name,
                    "DME: " + Math.Round(CombinedData.VORData.Distance / 1000, 1).ToString(),
                    "Radial: "+ Math.Round(CombinedData.VORData.Radial, 0).ToString(),
                    "OBS: "+ Math.Round(CombinedData.VORData.OBS, 0).ToString(),
                    "HDG: "+ Math.Round(CombinedData.VORData.Heading, 0).ToString(),
                    "DEV: "+ Math.Round(CombinedData.VORData.Deviation, 0).ToString()
                };

                List<string> Lines = new List<string>(_lines);

                foreach (string line in Lines)
                {
                    Frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = line,
                        Position = new Vector2(PosistionX, PositionYFactor * UnitY) + Viewport.Position,
                        RotationOrScale = DataScreenFontSize,
                        Color = CockpitFGColor.Alpha(1f),
                        Alignment = TextAlignment.LEFT,
                        FontId = "White"
                    });

                    PositionYFactor += 35;
                }
            }
            

            private static void DrawCross(ref MySpriteDrawFrame Frame)
            {
                MySprite CrossDiagonal1 = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, 100 * UnitY));
                CrossDiagonal1.RotationOrScale = ToRadian(45);
                CrossDiagonal1.Color = Color.Tomato;
                Frame.Add(CrossDiagonal1);

                MySprite CrossDiagonal2 = MySprite.CreateSprite("SquareSimple", Center, new Vector2(12 * UnitX, 100 * UnitY));
                CrossDiagonal2.RotationOrScale = ToRadian(-45);
                CrossDiagonal2.Color = Color.Tomato;
                Frame.Add(CrossDiagonal2);
            }


            private static float ToRadian(float degree)
            {
                return degree * ((float)Math.PI / 180);
            }
        }
    }
}
