using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Kinovea.Services
{
    public class CaptureFolder
    {
        /// <summary>
        /// Unique id for this capture folder.
        /// The screens reference the folder by id so even if the 
        /// path change they are still pointing to the right folder.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name to be displayed in menus.
        /// </summary>
        public string FriendlyName 
        {
            get { return string.IsNullOrEmpty(ShortName) ? Path : ShortName; } 
        }

        /// <summary>
        /// A short custom name to identify the folder in menus.
        /// This may be null or empty.
        /// </summary>
        public string ShortName { get; set; }
        
        /// <summary>
        /// Full path to the folder. This may contain variables and should never be used as-is.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Optional filename template for this folder (e.g. A-%dateb%-%time%). If set, used when this folder is selected.
        /// </summary>
        public string DefaultFileName { get; set; }

        public CaptureFolder()
        {
            Id = Guid.NewGuid();

            string root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Path = System.IO.Path.Combine(root, "Capture");
        }

        public CaptureFolder Clone()
        {
            CaptureFolder clone = new CaptureFolder
            {
                Id = this.Id,
                ShortName = this.ShortName,
                Path = this.Path,
                DefaultFileName = this.DefaultFileName
            };

            return clone;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(ShortName) ? Path : ShortName;
        }


        #region Serialization
        public void WriteXML(XmlWriter w)
        {
            w.WriteElementString("Id", Id.ToString());
            w.WriteElementString("ShortName", ShortName);
            w.WriteElementString("Path", Path);
            if (!string.IsNullOrEmpty(DefaultFileName))
                w.WriteElementString("DefaultFileName", DefaultFileName);
        }

        public CaptureFolder(XmlReader r)
           : this()
        {
            r.ReadStartElement();

            while (r.NodeType == XmlNodeType.Element)
            {
                switch (r.Name)
                {
                    case "Id":
                        Id = XmlHelper.ParseGuid(r.ReadElementContentAsString());
                        break;
                    case "ShortName":
                        ShortName = r.ReadElementContentAsString();
                        break;
                    case "Path":
                        Path = r.ReadElementContentAsString();
                        break;
                    case "DefaultFileName":
                        DefaultFileName = r.ReadElementContentAsString();
                        break;
                    default:
                        r.ReadOuterXml();
                        break;
                }
            }

            r.ReadEndElement();
        }
        #endregion
    }
}
