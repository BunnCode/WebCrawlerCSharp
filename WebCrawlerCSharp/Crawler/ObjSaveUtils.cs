using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace WebCrawlerCSharp.Crawler {

    /**
     * Class used for saving objects to and loading objects from the disk
     *
     * @author mrsma
     */
    public class ObjSaveUtils {

        string baseFolder;

        public ObjSaveUtils(string baseFolder) {
            this.baseFolder = baseFolder;
        }

        public void SaveObject(string name, object objIn, bool overWrite) {
            int fileVersion = 1;
            string fileName = baseFolder + name + ".xml";

            //Overwrite prevention- ex: file, file_v1, file_v2, file_v3......
            while (File.Exists(baseFolder + name) && !overWrite) {
                fileVersion++;
                fileName = baseFolder + name + "_v " + fileVersion;
            }

            DataContractSerializer serializer = new DataContractSerializer(objIn.GetType());
            using (XmlWriter writer = XmlWriter.Create(fileName)) {
                serializer.WriteObject(writer, objIn);
            }

            CU.WCol(fileName + " saved successfully." + CU.nl, CU.g);
        }

        public object LoadObject(string name, Type type) {
            object objOut = new object();
            string fileName = baseFolder + name + ".xml";
        
            DataContractSerializer serializer = new DataContractSerializer(type);
            using (XmlReader reader = XmlReader.Create(fileName)) {
                objOut = serializer.ReadObject(reader);
            }
            CU.WCol(fileName + " loaded successfully." + CU.nl, CU.g);
            return objOut;
        }
    }

}
