using System.Diagnostics;
using Serilog;
using QB_Vendors_Lib;
using QBFC16Lib; // For QuickBooks session and API interaction.
using static QB_Vendors_Test.CommonMethods;
using QB_Vendors_Test;

namespace QB_Vendors_Test
{
    [Collection("Sequential Tests")]
    public class VendorAdderTests
    {
        [Fact]
        public void AddMultipleVendors_ThenVerifyTheyExistInQuickBooks()
        {
            // 1) Setup: We'll create some random Vendors, then call the Adder.
            const int VENDOR_COUNT = 5;
            const int STARTING_COMPANY_ID = 5000; // Arbitrary starting ID
            var vendorsToAdd = new List<Vendor>();

            // Ensure logs are not locked, and optionally clean old logs if you wish
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            // 2) Build a list of random Vendor objects (Name + Fax as the "Company ID" field).
            for (int i = 0; i < VENDOR_COUNT; i++)
            {
                string randomName = "AdderTestVendor_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int companyID = STARTING_COMPANY_ID + i;
                string fax = companyID.ToString();  // Using fax for custom "Company ID"
                vendorsToAdd.Add(new Vendor(randomName, fax));
            }

            // 3) Call our Lib project's VendorAdder method.
            //    This is the code under test: VendorAdder.AddVendors(List<Vendor> vendors).
            VendorAdder.AddVendors(vendorsToAdd);

            // 4) Verify each added vendor now has a QB_ID (the add operation was successful).
            foreach (var vendor in vendorsToAdd)
            {
                Assert.False(string.IsNullOrWhiteSpace(vendor.QB_ID),
                    $"Vendor '{vendor.Name}' was not assigned a QB_ID after AddVendors().");
            }

            // 5) Now verify each vendor truly exists in QuickBooks by that QB_ID.
            //    We'll open a QB session and try to query them one by one.
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var vendor in vendorsToAdd)
                {
                    // Query QuickBooks for the vendor's ListID = vendor.QB_ID
                    IVendorRet? qbVendor = QueryVendorByListID(qbSession, vendor.QB_ID);
                    Assert.NotNull(qbVendor); // If this is null, it wasn't found in QB at all
                    Assert.Equal(vendor.QB_ID, qbVendor.ListID.GetValue());
                }
            }

            // 6) (Optional) Cleanup: remove the vendors from QuickBooks so they don’t pile up.
            //    In real tests, you often want to clean up the test data you introduced.
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var vendor in vendorsToAdd)
                {
                    if (!string.IsNullOrWhiteSpace(vendor.QB_ID))
                    {
                        DeleteVendor(qbSession, vendor.QB_ID);
                    }
                }
            }

            // 7) Flush logs to disk so we can read them (if you want to test logs).
            EnsureLogFileClosed();

            // 8) Verify that a new log file was written, if your production code logs.
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);

            // 9) Read and check any relevant log content, if desired.
            //    This depends on how your VendorAdder is implemented/logging internally.
            string logContents = File.ReadAllText(logFile);
            Assert.Contains("VendorAdder Initialized", logContents);
            Assert.Contains("VendorAdder Completed", logContents);

            // 10) Optionally ensure all added vendors or key steps were logged, if that is part of the Adder’s functionality.
            foreach (var vendor in vendorsToAdd)
            {
                string expectedLogMessage = $"Successfully added {vendor.Name} to QB";
                Assert.Contains(expectedLogMessage, logContents);
            }
        }

        /// <summary>
        /// Queries QuickBooks for a single Vendor by ListID (QB_ID).
        /// Returns the IVendorRet if found, otherwise null.
        /// </summary>
        private IVendorRet? QueryVendorByListID(QuickBooksSession qbSession, string qbListID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IVendorQuery vendorQuery = requestMsgSet.AppendVendorQueryRq();
            vendorQuery.ORVendorListQuery.ListIDList.Add(qbListID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0)
                return null;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode != 0 || response.Detail == null)
                return null; // No valid response

            IVendorRetList retList = response.Detail as IVendorRetList;
            if (retList == null || retList.Count == 0)
                return null;

            // We expect only one vendor with this ListID
            return retList.GetAt(0);
        }

        /// <summary>
        /// Deletes a vendor from QuickBooks using the given ListID (QB_ID).
        /// </summary>
        private void DeleteVendor(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IListDel listDelRq = requestMsgSet.AppendListDelRq();
            listDelRq.ListDelType.SetValue(ENListDelType.ldtVendor);
            listDelRq.ListID.SetValue(listID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
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
                throw new Exception($"Error Deleting Vendor (ListID: {listID}): {response.StatusMessage}. " +
                                    $"Status code: {response.StatusCode}");
            }
        }
    }
}