﻿using Dicom;
using RT.Core.Geometry;
using RT.Core.ROIs;
using RT.Core.DICOM;
using System;
using System.Collections.Generic;

namespace RT.Core.IO.Loaders
{
    public class ROILoader:BaseDicomLoader
    {
        public void Load(DicomFile[] files, StructureSet structureSet, IProgress<double> progress)
        {
            base.Load(files, structureSet, progress);
            DicomFile file = files[0];

            structureSet.Name = file.Dataset.GetString(DicomTag.StructureSetLabel);

            Dictionary<int, string> roi_names = new Dictionary<int, string>();
            DicomSequence structs = file.Dataset.GetDicomItem<DicomSequence>(DicomTag.StructureSetROISequence);
            foreach (DicomDataset item in structs)
            {
                roi_names.Add(item.GetSingleValue<int>(DicomTag.ROINumber), item.GetSingleValue<string>(DicomTag.ROIName));
            }

            List<RegionOfInterest> rois = new List<RegionOfInterest>();

            DicomSequence s = file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence);

            //Track the item number to report progress
            double total = s.Items.Count;
            double num = 0;

            foreach (DicomDataset item in s.Items)
            {
                num++;
                progress.Report(100 * num / total);

                RegionOfInterest roi = new RegionOfInterest();
                int[] color = item.GetSingleValueOrDefault<int[]>(DicomTag.ROIDisplayColor,new int[] { 0, 0, 0 });
                roi.Color = DicomColor.FromRgb(color[0],color[1],color[2]);
                roi.ROINumber = item.GetSingleValue<int>(DicomTag.ReferencedROINumber);
                if (roi_names.ContainsKey(roi.ROINumber))
                    roi.Name = roi_names[roi.ROINumber];

                DicomSequence roi_definitions;
                try
                {
                    roi_definitions = item.GetDicomItem<DicomSequence>(DicomTag.ContourSequence);
                }
                catch (Exception e)
                {
                    continue;
                }

                double xmin = double.MaxValue, ymin = double.MaxValue, zmin = double.MaxValue, xmax = double.MinValue, ymax = double.MinValue, zmax = double.MinValue;


                foreach (DicomDataset contourSlice in roi_definitions.Items)
                {
                    int vertex_count = contourSlice.GetSingleValue<int>(DicomTag.NumberOfContourPoints);
                    double[] vertices = contourSlice.GetValues<double>(DicomTag.ContourData);

                    //Attempt to get the contour type from the dicom tag
                    string type = contourSlice.GetString(DicomTag.ContourGeometricType);
                    Enum.TryParse<ContourType>(type, out ContourType contourType);
                    //Assume that each contour of the roi is of the same type...
                    roi.Type = contourType;

                    PlanarPolygon poly = new PlanarPolygon();
                    // we divide the number of vertices here by 1.5 because we are going from a 3d poly to a 2d poly on the z plane
                    poly.Vertices = new double[(int)(vertices.Length / 1.5)];
                    double zcoord = vertices[2];
                    int polyIndex = 0;

                    RegionOfInterestSlice slice = roi.GetSlice(zcoord);
                    if (slice == null)
                        slice = new RegionOfInterestSlice() { ZCoord = zcoord, };

                    for (int i = 0; i < vertices.Length; i += 3)
                    {
                        poly.Vertices[polyIndex] = vertices[i];
                        poly.Vertices[polyIndex + 1] = vertices[i + 1];
                        if (vertices[i] < xmin) xmin = vertices[i];
                        if (vertices[i] > xmax) xmax = vertices[i];
                        if (vertices[i + 1] < ymin) ymin = vertices[i + 1];
                        if (vertices[i + 1] > ymax) ymax = vertices[i + 1];
                        if (zcoord < zmin) zmin = zcoord;
                        if (zcoord > zmax) zmax = zcoord;
                        polyIndex += 2;
                    }
                    if (zmin < roi.ZRange.Minimum)
                        roi.ZRange.Minimum = zmin;
                    if (zmax > roi.ZRange.Maximum)
                        roi.ZRange.Maximum = zmax;

                    slice.AddPolygon(poly);

                    roi.AddSlice(slice, zcoord);
                }

                roi.XRange = new Geometry.Range(xmin, xmax);
                roi.YRange = new Geometry.Range(ymin, ymax);
                roi.ZRange = new Geometry.Range(zmin, zmax);

                for (int i = 0; i < roi.RegionOfInterestSlices.Count; i++)
                {
                    for (int j = 0; j < roi.RegionOfInterestSlices[i].Polygons.Count; j++)
                    {
                        roi.RegionOfInterestSlices[i].Polygons[j].XRange = new Geometry.Range(xmin, xmax);
                        roi.RegionOfInterestSlices[i].Polygons[j].YRange = new Geometry.Range(ymin, ymax);
                    }
                    roi.RegionOfInterestSlices[i].ComputeBinaryMask();
                }
                rois.Add(roi);
            }
            GC.Collect();
            structureSet.ROIs = rois;
        }
    }
}
