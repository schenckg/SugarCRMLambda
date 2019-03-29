using System;
using System.Collections.Generic;
using System.Text;

namespace SugarCRMLibrary
{
    public class Record
    {
        public string ID { get; }
        public string Name { get; }
        public string Type { get; }

        public Record(string strType, string strID, string strName)
        {
            Type = strType;
            ID = strID;
            Name = strName;
        }

        public override string ToString()
        {
            return $"{Type}: {Name}";
        }
    }
}
