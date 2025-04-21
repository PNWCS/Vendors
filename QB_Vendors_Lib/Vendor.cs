using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QB_Vendors_Lib
{
    public class Vendor
    {
        public string Name { get; set; }
        public string CompanyName { get; set; }
        public string QB_ID { get; set; }

        public Vendor(string name, string companyName)
        {
            Name = name;
            CompanyName = companyName;
        }
    }
}