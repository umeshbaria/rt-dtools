using Dicom;
using RT.Core;
using RT.Core.DICOM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RT.Core.IO.Loaders
{
    public class BaseDicomLoader
    {
        public BaseDicomLoader() { }
        protected void Load(DicomFile[] files, DicomObject dicomObject, IProgress<double> progress)
        {
            dicomObject.FileNames = new string[files.Length];
            for(int i = 0; i < files.Length; i++)
            {
                dicomObject.FileNames[i] = files[i].File.Name;
            }
            if(files.Length > 0)
            {
                dicomObject.PatientId = files[0].Dataset.GetString(DicomTag.PatientID);
                dicomObject.PatientName = files[0].Dataset.GetString(DicomTag.PatientName);
                dicomObject.SeriesUid = files[0].Dataset.GetString(DicomTag.SeriesInstanceUID);
                dicomObject.Modality = files[0].Dataset.GetString(DicomTag.Modality);
                dicomObject.SeriesDescription = files[0].Dataset.GetString(DicomTag.SeriesDescription);
                dicomObject.StudyDescription = files[0].Dataset.GetString(DicomTag.StudyDescription);
            }
        }
    }
}
