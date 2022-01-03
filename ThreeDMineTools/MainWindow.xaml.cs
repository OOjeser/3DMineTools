﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using ObjLoader.Loader.Common;
using ObjLoader.Loader.Data.Elements;
using ObjLoader.Loader.Data.VertexData;
using ObjLoader.Loader.Loaders;
using Substrate;
using Substrate.Core;
using Substrate.Nbt;
using ThreeDMineTools.Models;
using ThreeDMineTools.Tools;
using Color = System.Windows.Media.Color;
using Image = System.Drawing.Image;
using Polygon = System.Windows.Shapes.Polygon;


namespace ThreeDMineTools
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }


        private float max(float f1, float f2, float f3)
        {
            if (f1 > f2 && f1 > f3)
                return f1;
            if (f2 > f3 && f2 > f1)
                return f2;
            return f3;
        }
        private float min(float f1, float f2, float f3)
        {
            if (f1 < f2 && f1 < f3)
                return f1;
            if (f2 < f3 && f2 < f1)
                return f2;
            return f3;
        }

        private float XMax = float.MinValue, YMax = float.MinValue, ZMax = float.MinValue, XMin = float.MaxValue, YMin = float.MaxValue, ZMin = float.MaxValue;

        private void OpenModel(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();
            openFileDialog.Filter = "obj|*.obj";
            openFileDialog.ShowDialog();

            if(openFileDialog.FileName.IsNullOrEmpty())
                return;

            File.Copy(openFileDialog.FileName.Replace(".obj", ".mtl"), openFileDialog.FileName.Split("\\").Last().Replace(".obj", ".mtl"));

            ObjLoaderFactory objLoaderFactory = new ObjLoaderFactory();
            IObjLoader? objLoader = objLoaderFactory.Create();
            FileStream fileStream = new FileStream(openFileDialog.FileName, FileMode.Open);
            LoadResult? result = objLoader.Load(fileStream);
            fileStream.Close();


            Model3D.Positions.Clear();
            Model3D.TriangleIndices.Clear();
            for (int i = 0; i < result.Vertices.Count; i++)
            {
                Vertex p = result.Vertices[i];
                p = new Vertex(p.X, p.Y, p.Z);
                XMax = MathF.Max(XMax, p.X);
                YMax = MathF.Max(YMax, p.Y);
                ZMax = MathF.Max(ZMax, p.Z);
                XMin = MathF.Min(XMin, p.X);
                YMin = MathF.Min(YMin, p.Y);
                ZMin = MathF.Min(ZMin, p.Z);
                Model3D.Positions.Add(new Point3D(p.X, p.Y, p.Z));
            }

            scale.Minimum = 1 / (YMax - YMin);
            scale.Maximum = 250 / (YMax - YMin);
            scale.Value = 1;
            scale.IsEnabled = true;

            List<MPolygon> polygons = new List<MPolygon>();
            foreach (Group? resultGroup in result.Groups)
            {
                if (resultGroup == null)
                    continue;
                Bitmap bmp = null;
                if (resultGroup.Material.DiffuseTextureMap != null)
                {
                    bmp = (Bitmap)Image.FromFile(resultGroup.Material.DiffuseTextureMap);
                    cnv.Height = bmp.Height;
                    cnv.Width = bmp.Width;
                    cnv.Background = new ImageBrush(Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    ));
                }

                foreach (Face? face in resultGroup.Faces)
                {
                    if (face == null)
                        continue;
                    Model3D.TriangleIndices.Add(face[0].VertexIndex - 1);
                    Model3D.TriangleIndices.Add(face[1].VertexIndex - 1);
                    Model3D.TriangleIndices.Add(face[2].VertexIndex - 1);

                    int avgR = 1, avgG = 1, avgB = 1, avgCount = 0;
                    if (bmp != null)
                    {
                        var uvp1 = result.Textures[face[0].VertexIndex - 1];
                        var uvp2 = result.Textures[face[1].VertexIndex - 1];
                        var uvp3 = result.Textures[face[2].VertexIndex - 1];
                        uvp1 = new Texture(uvp1.X * bmp.Width, bmp.Height - uvp1.Y * bmp.Height);
                        uvp2 = new Texture(uvp2.X * bmp.Width, bmp.Height - uvp2.Y * bmp.Height);
                        uvp3 = new Texture(uvp3.X * bmp.Width, bmp.Height - uvp3.Y * bmp.Height);

                        System.Drawing.Color TmpCol;
                        for (int x = (int)min(uvp1.Y, uvp2.Y, uvp3.Y); x < max(uvp1.X, uvp2.X, uvp3.X); x++)
                        {
                            for (int y = (int)min(uvp1.Y, uvp2.Y, uvp3.Y); y < max(uvp1.Y, uvp2.Y, uvp3.Y); y++)
                            {
                                //(x1 - x0) * (y2 - y1) - (x2 - x1) * (y1 - y0)
                                //(x2 - x0) * (y3 - y2) - (x3 - x2) * (y2 - y0)
                                //(x3 - x0) * (y1 - y3) - (x1 - x3) * (y3 - y0)
                                var a = (uvp1.X - x) * (uvp2.Y - uvp1.Y) - (uvp2.X - uvp1.X) * (uvp1.Y - y);
                                var b = (uvp2.X - x) * (uvp3.Y - uvp2.Y) - (uvp3.X - uvp2.X) * (uvp2.Y - y);
                                var c = (uvp3.X - x) * (uvp1.Y - uvp3.Y) - (uvp1.X - uvp3.X) * (uvp3.Y - y);
                                if ((a >= 0 && b >= 0 && c >= 0) || (a <= 0 && b <= 0 && c <= 0) || true)
                                {
                                    TmpCol = bmp.GetPixel(x, y);
                                    avgR += TmpCol.R;
                                    avgG += TmpCol.G;
                                    avgB += TmpCol.B;
                                    avgCount++;
                                }
                            }
                        }
                        TmpCol = bmp.GetPixel((int)uvp1.X % bmp.Width, (int)uvp1.Y % bmp.Height);
                        avgR += TmpCol.R;
                        avgG += TmpCol.G;
                        avgB += TmpCol.B;
                        TmpCol = bmp.GetPixel((int)uvp2.X % bmp.Width, (int)uvp2.Y % bmp.Height);
                        avgR += TmpCol.R;
                        avgG += TmpCol.G;
                        avgB += TmpCol.B;
                        TmpCol = bmp.GetPixel((int)uvp3.X % bmp.Width, (int)uvp3.Y % bmp.Height);
                        avgR += TmpCol.R;
                        avgG += TmpCol.G;
                        avgB += TmpCol.B;
                        avgCount += 3;

                        if (avgCount != 0)
                        {
                            avgR /= avgCount;
                            avgG /= avgCount;
                            avgB /= avgCount;
                        }
                        cnv.Children.Add(new Polygon()
                        {
                            Stroke = new SolidColorBrush(Color.FromRgb(206, 148, 0)),
                            Points = new PointCollection()
                            {
                                new System.Windows.Point(uvp1.X, uvp1.Y),
                                new System.Windows.Point(uvp2.X, uvp3.Y),
                                new System.Windows.Point(uvp3.X, uvp2.Y),
                            },
                            StrokeThickness = 1,
                            Fill = new SolidColorBrush(Color.FromArgb(76, 206, 148, 0))
                        });
                    }
                    else
                    {
                        avgR = (byte)(resultGroup.Material.DiffuseColor.X * 255);
                        avgG = (byte)(resultGroup.Material.DiffuseColor.Y * 255);
                        avgB = (byte)(resultGroup.Material.DiffuseColor.Z * 255);
                    }

                    var ppp = new MPolygon()
                    {
                        Point1 = new MPoint(Model3D.Positions[face[0].VertexIndex - 1]),
                        Point2 = new MPoint(Model3D.Positions[face[1].VertexIndex - 1]),
                        Point3 = new MPoint(Model3D.Positions[face[2].VertexIndex - 1]),
                        AverageColor = Color.FromRgb((byte)(avgR), (byte)(avgG), (byte)(avgB))
                    };


                    polygons.Add(ppp);
                }
            }

            model = new MModel()
            {
                Polygons = polygons.ToArray(),
                YMin = YMin,
                ZMin = ZMin,
                XMin = XMin,
                XMax = XMax,
                YMax = YMax,
                ZMax = ZMax
            };
            convertButton.IsEnabled = true;
            File.Delete(openFileDialog.FileName.Split("\\").Last().Replace(".obj", ".mtl"));
        }

        private MModel model;
        private List<List<List<Color?>>> vertex;
        private void WriteSchematics(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Schematic|*.schematic";
            saveFileDialog.ShowDialog();
            if (saveFileDialog.FileName.IsNullOrEmpty())
                return;


            List<byte> bytes = new List<byte>(10000);
            List<byte> data = new List<byte>(10000);

            for (int y = 0; y < vertex[0].Count; y++)
            {
                for (int z = 0; z < vertex[0][0].Count; z++)
                {
                    for (int x = 0; x < vertex.Count; x++)
                    {
                        if (vertex[x][y][z] == null)
                        {
                            bytes.Add(0);
                            data.Add(0);
                        }
                        else
                        {
                            (byte, byte) r = BlockPicker.GetBlockFroColor((Color)vertex[x][y][z]);
                            bytes.Add(r.Item1);
                            data.Add(r.Item2);
                        }
                    }
                }
            }


            FileStream file = new(saveFileDialog.FileName, FileMode.OpenOrCreate);
            NbtTree tree = new NbtTree();
            tree.Name = "dwa";
            tree.Root.Add("Width", new TagNodeShort((short)vertex.Count));
            tree.Root.Add("Height", new TagNodeShort((short)vertex[0].Count));
            tree.Root.Add("Length", new TagNodeShort((short)vertex[0][0].Count));
            tree.Root.Add("Blocks", new TagNodeByteArray(bytes.ToArray()));
            tree.Root.Add("Data", new TagNodeByteArray(data.ToArray()));
            tree.Root.Add("Entities", new TagNodeList(TagType.TAG_BYTE));
            tree.Root.Add("TileEntities", new TagNodeList(TagType.TAG_BYTE));
            tree.Root.Add("Materials", new TagNodeString("Classic"));
            tree.WriteTo(file);
            file.Close();

        }

        private void ConvertModel(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < model.Polygons.Length; i++)
            {
                model.Polygons[i].Point1.X *= (float)scale.Value;
                model.Polygons[i].Point1.Y *= (float)scale.Value;
                model.Polygons[i].Point1.Z *= (float)scale.Value;
                model.Polygons[i].Point2.X *= (float)scale.Value;
                model.Polygons[i].Point2.Y *= (float)scale.Value;
                model.Polygons[i].Point2.Z *= (float)scale.Value;
                model.Polygons[i].Point3.X *= (float)scale.Value;
                model.Polygons[i].Point3.Y *= (float)scale.Value;
                model.Polygons[i].Point3.Z *= (float)scale.Value;
            }
            model.XMin *= (float)scale.Value;
            model.YMin *= (float)scale.Value;
            model.ZMin *= (float)scale.Value;
            model.XMax *= (float)scale.Value;
            model.YMax *= (float)scale.Value;
            model.ZMax *= (float)scale.Value;
            vertex = ModelConverter.PolygonToVoxel3(model);
            writeButton.IsEnabled = true;
        }

        private void scaleChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            heightTB.Text = ((YMax - YMin) * scale.Value).ToString();
        }
    }
}
