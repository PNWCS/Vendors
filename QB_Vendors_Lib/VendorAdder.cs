using QBFC16Lib;
using Serilog;
using System;
using System.Collections.Generic;
using QB_Vendors_Lib;

namespace QB_Vendors_Lib
{
    public class VendorAdder
    {
        static VendorAdder()
        {
            LoggerConfig.ConfigureLogging(); // Safe to call (only initializes once)
            Log.Information("VendorAdder Initialized.");
            // Initialize the QuickBooks session manager here if needed.
        }

        public static void AddVendors(List<Vendor> vendorInfo)
        {
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var vendor in vendorInfo)
                {
                    string qbID = AddVendor(qbSession, vendor.Name, vendor.CompanyName);
                    vendor.QB_ID = qbID; // Store the returned QB ListID.
                }
            }
            Log.Information("VendorAdder Completed");
        }

        private static string AddVendor(QuickBooksSession qbSession, string name, string companyName)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IVendorAdd vendorAddRq = requestMsgSet.AppendVendorAddRq();
            vendorAddRq.Name.SetValue(name);
            vendorAddRq.CompanyName.SetValue(companyName);
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