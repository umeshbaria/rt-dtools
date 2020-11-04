using Dicom;
using RT.Core.Planning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RT.Core.IO.Loaders
{
    public class PlanLoader
    {
        public void Load(DicomFile[] files, DicomPlanObject plan)
        {
            DicomFile file = files[0];
            string modality = file.Dataset.GetString(DicomTag.Modality);
            if (!modality.Contains("PLAN"))
                throw (new Exception("DICOM file was not RTPLAN"));

            plan.Name = file.Dataset.GetString(DicomTag.RTPlanName);
            plan.Label = file.Dataset.GetString(DicomTag.RTPlanLabel);

            Dictionary<int, double> beamMetersets = getBeamMetersets(file);

            var beam_sequence = file.Dataset.GetDicomItem<DicomSequence>(DicomTag.BeamSequence);
            foreach (var child in beam_sequence)
            {
                Beam beam = new Beam();
                beam.TreatmentMachineName = child.GetString(DicomTag.TreatmentMachineName);
                beam.SAD = child.GetValue<double>(DicomTag.SourceAxisDistance, 100);
                beam.MU = beamMetersets[child.GetSingleValue<int>(DicomTag.BeamNumber)];
                beam.Name = child.GetString(DicomTag.BeamName);


                var totalCumMetersetWeight = child.GetValueOrDefault<double>(DicomTag.FinalCumulativeMetersetWeight, 0, 1);

                var control_point_sequence = child.GetDicomItem<DicomSequence>(DicomTag.ControlPointSequence);
                foreach (var control_point in control_point_sequence)
                {
                    ControlPoint cp = new ControlPoint();
                    cp.Isocentre = child.GetValues<double>(DicomTag.IsocenterPosition);
                    cp.CumulativeMetersetWeight = control_point.GetSingleValue<double>(DicomTag.CumulativeMetersetWeight);
                    cp.Index = control_point.GetSingleValue<int>(DicomTag.ControlPointIndex);
                    if (cp.CumulativeMetersetWeight != totalCumMetersetWeight)
                    {
                        cp.NominalBeamEnergy = control_point.GetValueOrDefault<int>(DicomTag.NominalBeamEnergy, 0, 0);
                        cp.GantryAngle = control_point.GetValue<double>(DicomTag.GantryAngle, 0);
                        cp.CollimatorAngle = control_point.GetValue<double>(DicomTag.BeamLimitingDeviceAngle, 0);
                        cp.CouchAngle = control_point.GetValue<double>(DicomTag.PatientSupportAngle, 0);


                        var beam_limiting_sequence = control_point.GetDicomItem<DicomSequence>(DicomTag.BeamLimitingDevicePositionSequence);
                        foreach (var beamlimit in beam_limiting_sequence)
                        {
                            string type = beamlimit.GetString(DicomTag.RTBeamLimitingDeviceType);
                            var posns = beamlimit.GetValues<double>(DicomTag.LeafJawPositions);

                            if (type == "ASYMX" || type == "X")
                            {
                                cp.XJaw = new Jaw()
                                {
                                    Direction = Geometry.Direction.X,
                                    NegativeJawPosition = posns[0],
                                    PositiveJawPosition = posns[1],
                                };
                            }
                            else if (type == "ASYMY" || type == "Y")
                            {
                                cp.YJaw = new Jaw()
                                {
                                    Direction = Geometry.Direction.Y,
                                    NegativeJawPosition = posns[0],
                                    PositiveJawPosition = posns[1],
                                };
                            }
                            else if (type == "MLCX")
                            {

                            }
                        }
                    }
                    beam.ControlPoints.Add(cp);
                }
                plan.Beams.Add(beam);
            }
        }

        public  Dictionary<int, double> getBeamMetersets(DicomFile file)
        {
            Dictionary<int, double> metersets = new Dictionary<int, double>();
            //only works if one fraction...
            var fraction_group_sequence = file.Dataset.GetDicomItem<DicomSequence>(DicomTag.FractionGroupSequence);
            var ref_beam_sequence = fraction_group_sequence.First().GetDicomItem<DicomSequence>(DicomTag.ReferencedBeamSequence);
            foreach (var ref_beam in ref_beam_sequence)
            {
                metersets.Add(ref_beam.GetSingleValue<int>(DicomTag.ReferencedBeamNumber), ref_beam.GetSingleValue<double>(DicomTag.BeamMeterset));
            }
            return metersets;
        }
    }
}
