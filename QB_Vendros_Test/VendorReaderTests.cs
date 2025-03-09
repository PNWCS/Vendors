using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using QB_Vendors_Lib;  // Assumes Vendor, VendorReader, AppConfig, etc. are defined here.
using QBFC16Lib;       // For QuickBooks session and API interaction.
using Xunit;
using static QB_Vendors_Test.CommonMethods;
using System.Numerics;

namespace QB_Vendors_Test
{
    [Collection("Sequential Tests")]
    public class VendorReaderTests
    {
        [Fact]
        public void AddAndReadMultipleVendors_FromQuickBooks_And_Verify_Logs()
        {
            const int VENDOR_COUNT = 5;
            const int STARTING_COMPANY_ID = 100;
            var vendorsToAdd = new List<Vendor>();

            // 1) Ensure Serilog has released file access before deleting old logs.
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            // 2) Build a list of random Vendor objects with a name and fax (using fax for company id).
            for (int i = 0; i < VENDOR_COUNT; i++)
            {
                string randomName = "TestVendor_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int companyID = STARTING_COMPANY_ID + i;
                string fax = companyID.ToString();
                vendorsToAdd.Add(new Vendor(randomName, fax));
            }

            // 3) Add vendors directly to QuickBooks.
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var vendor in vendorsToAdd)
                {
                    string qbID = AddVendor(qbSession, vendor.Name, vendor.Fax);
                    vendor.QB_ID = qbID; // Store the returned QB ListID.
                }
            }

            // 4) Query QuickBooks to retrieve all vendors.
            var allQBVendors = VendorReader.QueryAllVendors();

            // 5) Verify that all added vendors are present in QuickBooks.
            foreach (var vendor in vendorsToAdd)
            {
                var matchingVendor = allQBVendors.FirstOrDefault(v => v.QB_ID == vendor.QB_ID);
                Assert.NotNull(matchingVendor);
                Assert.Equal(vendor.Name, matchingVendor.Name);
                Assert.Equal(vendor.Fax, matchingVendor.Fax);
            }

            // 6) Cleanup: Delete the added vendors.
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var vendor in vendorsToAdd.Where(v => !string.IsNullOrEmpty(v.QB_ID)))
                {
                    DeleteVendor(qbSession, vendor.QB_ID);
                }
            }

            // 7) Ensure logs are fully flushed before accessing them.
            EnsureLogFileClosed();

            // 8) Verify that a new log file exists.
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);

            // 9) Read the log file content.
            string logContents = File.ReadAllText(logFile);

            // 10) Assert expected log messages exist.
            Assert.Contains("VendorReader Initialized", logContents);
            Assert.Contains("VendorReader Completed", logContents);

            // 11) Verify that each retrieved vendor was logged properly.
            foreach (var vendor in vendorsToAdd)
            {
                string expectedLogMessage = $"Successfully retrieved {vendor.Name} from QB";
                Assert.Contains(expectedLogMessage, logContents);
            }
        }

        private string AddVendor(QuickBooksSession qbSession, string name, string fax)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IVendorAddRq vendorAddRq = requestMsgSet.AppendVendorAddRq();
            vendorAddRq.Name.SetValue(name);
            vendorAddRq.Fax.SetValue(fax);
            // Additional vendor fields can be set here as needed.

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            return ExtractListIDFromResponse(responseMsgSet);
        }

        private string ExtractListIDFromResponse(IMsgSetResponse responseMsgSet)
        {
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0)
                throw new Exception("No response from VendorAddRq.");

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode != 0)
                throw new Exception($"VendorAdd failed: {response.StatusMessage}");

            // Attempt to cast the response detail to IVendorRet.
            IVendorRet? vendorRet = response.Detail as IVendorRet;
            if (vendorRet == null)
                throw new Exception("No IVendorRet returned after adding Vendor.");

            return vendorRet.ListID?.GetValue()
                ?? throw new Exception("ListID is missing in QuickBooks response.");
        }

        private void DeleteVendor(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IListDel listDelRq = requestMsgSet.AppendListDelRq();
            listDelRq.ListDelType.SetValue(ENListDelType.ldtVendor);
            listDelRq.ListID.SetValue(listID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            WalkListDelResponse(responseMsgSet, listID);
        }

        private void WalkListDelResponse(IMsgSetResponse responseMsgSet, string listID)
        {
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0)
                return;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode == 0 && response.Detail != null)
            {
                Debug.WriteLine($"Successfully deleted Vendor (ListID: {listID}).");
            }
            else
            {
                throw new Exception($"Error Deleting Vendor (ListID: {listID}): {response.StatusMessage}. Status code: {response.StatusCode}");
            }
        }
    }
}
