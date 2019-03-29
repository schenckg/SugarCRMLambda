using System;
using System.Collections.Generic;
using System.Text;

namespace SugarCRMLibrary
{
    public class Contact : Record
    {
        public string AccountID { get; }
        public string AccountName { get; }

        public Contact(string strID, string strName, string strAccountID = "", string strAccountName = "") : base("Contact", strID, strName)
        {
            AccountID = strAccountID;
            AccountName = strAccountName;
        }
    }
}
