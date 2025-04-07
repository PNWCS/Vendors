using System.Net.NetworkInformation;
using QB_Vendors_Lib;
//using Vendor_LIB;

namespace Vendors
{

    class Program
    {
        static void Main(string[] args)
        {
            VendorReader.QueryAllVendors();
        }
    }
}