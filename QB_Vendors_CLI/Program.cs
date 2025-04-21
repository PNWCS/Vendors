using System.Net.NetworkInformation;
using QB_Vendors_Lib;
//using Vendor_LIB;

namespace Vendors
{
    class Program
    {
        static void Main(string[] args)
        {
            List<Vendor> vendorsToAdd = new List<Vendor>
            {
                new Vendor ( "Danny", "Amazon" ),
                new Vendor ("Emma", "Microsoft" ),
                new Vendor ("Kapil Dev", "Deloitte" )
            };

            VendorAdder.AddVendors(vendorsToAdd);

            //VendorReader.QueryAllVendors();
        }
    }
}