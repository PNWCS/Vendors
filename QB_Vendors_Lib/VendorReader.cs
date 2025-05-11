using System;
using System.Collections.Generic;
using Serilog;
using QBFC16Lib;

namespace QB_Vendors_Lib
{
    public class VendorReader
    {
        static VendorReader()
        {
            LoggerConfig.ConfigureLogging(); // Safe to call (only initializes once)
            Log.Information("VendorReader Initialized.");
        }
        private static readonly ILogger Logger = Log.Logger;

        public static List<Vendor> QueryAllVendors()
        {
            bool sessionBegun = false;
            bool connectionOpen = false;
            QBSessionManager sessionManager = null;
            List<Vendor> vendors = new List<Vendor>();

            try
            {
                // Create the session Manager object
                sessionManager = new QBSessionManager();

                // Create the message set request object to hold our request
                IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
                requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

                BuildVendorQueryRq(requestMsgSet);

                // Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", AppConfig.QB_APP_NAME);
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                // Send the request and get the response from QuickBooks
                IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

                // End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionBegun = false;
                sessionManager.CloseConnection();
                connectionOpen = false;

                vendors = WalkVendorQueryRs(responseMsgSet);
            }
            catch (Exception e)
            {
                if (sessionBegun)
                {
                    sessionManager.EndSession();
                }
                if (connectionOpen)
                {
                    sessionManager.CloseConnection();
                }

                Console.WriteLine("Error: " + e.Message);
            }

            return vendors;

            void BuildVendorQueryRq(IMsgSetRequest requestMsgSet)
            {
                IVendorQuery vendorQueryRq = requestMsgSet.AppendVendorQueryRq();
            }

            List<Vendor> WalkVendorQueryRs(IMsgSetResponse responseMsgSet)
            {
                var vendors = new List<Vendor>();
                if (responseMsgSet == null) return vendors;
                IResponseList responseList = responseMsgSet.ResponseList;
                if (responseList == null) return vendors;
                // If we sent only one request, there is only one response, we'll walk the list for this sample
                for (int i = 0; i < responseList.Count; i++)
                {
                    IResponse response = responseList.GetAt(i);
                    // Check the status code of the response, 0 = ok, >0 is warning
                    if (response.StatusCode >= 0)
                    {
                        // The request-specific response is in the details, make sure we have some
                        if (response.Detail != null)
                        {
                            // Make sure the response is the type we're expecting
                            ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                            if (responseType == ENResponseType.rtVendorQueryRs)
                            {
                                // Upcast to more specific type here, this is safe because we checked with response.Type check above
                                IVendorRetList vendorRetList = (IVendorRetList)response.Detail;
                                for (int j = 0; j < vendorRetList.Count; j++)
                                {
                                    IVendorRet vendorRet = vendorRetList.GetAt(j);
                                    if (vendorRet != null)
                                    {
                                        var name = vendorRet.Name != null ? vendorRet.Name.GetValue() : string.Empty;
                                        var fax = vendorRet.Fax != null ? vendorRet.Fax.GetValue() : string.Empty;
                                        var id = vendorRet.ListID != null ? vendorRet.ListID.GetValue() : string.Empty;
                                        var companyId = vendorRet.AccountNumber != null ? vendorRet.AccountNumber.GetValue() : string.Empty;
                                        var vendor = new Vendor(name, fax, companyId);
                                        vendor.QB_ID = id;
                                        vendors.Add(vendor);

                                        // Log the vendor information
                                        Log.Information("Successfully retrieved {Name} from QB", vendor.Name);
                                        Log.Information("Vendor Name: {Name}, Fax: {Fax}", vendor.Name, vendor.Fax);
                                    }
                                }
                            }
                        }
                    }
                }
                Log.Information("VendorReader Completed.");
                return vendors;
            }
        }
    }
}