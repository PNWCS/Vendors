using System.Diagnostics;
using QBFC16Lib;
using Serilog;

namespace QB_Vendors_Lib
{
    public class VendorAdder
    {
        // QuickBooks field length limits
        private const int QB_NAME_MAX_LENGTH = 20;
        private const int QB_FAX_MAX_LENGTH = 20;

        static VendorAdder()
        {
            LoggerConfig.ConfigureLogging(); // Safe to call (only initializes once)
            Log.Information("VendorAdder Initialized.");
        }

        public static void AddVendors(List<Vendor> vendorInfo)
        {
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var vendor in vendorInfo)
                {
                    string qbID = AddVendor(qbSession, vendor.Name, vendor.Fax, vendor.Company_ID);
                    vendor.QB_ID = qbID; // Store the returned QB ListID.
                }
            }
            Log.Information("VendorAdder Completed");
        }

        private static string AddVendor(QuickBooksSession qbSession, string name, string fax, string companyId)
        {
            // Truncate values to field limits
            name = name?.Length > QB_NAME_MAX_LENGTH ? name[..QB_NAME_MAX_LENGTH] : name;
            fax = fax?.Length > QB_FAX_MAX_LENGTH ? fax[..QB_FAX_MAX_LENGTH] : fax;

            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IVendorAdd vendorAddRq = requestMsgSet.AppendVendorAddRq();
            Debug.WriteLine($"Adding Vendor: {name}, Fax: {fax}");
            vendorAddRq.Name.SetValue(name);
            vendorAddRq.Fax.SetValue(fax);
            vendorAddRq.AccountNumber.SetValue(companyId.ToString());
            // Additional vendor fields can be set here as needed.

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            return ExtractListIDFromResponse(responseMsgSet);
        }

        private static string ExtractListIDFromResponse(IMsgSetResponse responseMsgSet)
        {
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0)
                throw new Exception("No response from VendorAddRq.");

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode != 0)
                throw new Exception($"VendorAdd failed: {response.StatusMessage}");

            IVendorRet? vendorRet = response.Detail as IVendorRet;
            if (vendorRet == null)
                throw new Exception("No IVendorRet returned after adding Vendor.");

            return vendorRet.ListID?.GetValue()
                ?? throw new Exception("ListID is missing in QuickBooks response.");
        }
    }
}