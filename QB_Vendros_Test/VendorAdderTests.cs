using System.Diagnostics;
using Serilog;
using QB_Vendors_Lib;
using QBFC16Lib;
using static QB_Vendors_Test.CommonMethods; // for EnsureLogFileClosed, DeleteOldLogFiles, etc.
using Xunit;

namespace QB_Vendors_Test
{
    [Collection("Sequential Tests")]
    public class VendorAdderTests
    {
        [Fact]
        public void AddMultipleVendors_UsingVendorAdder_AndVerifyInQB_And_ValidateLogs()
        {
            // 1) Prep: ensure Serilog has closed the file, remove old logs, reset logger
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            // 2) Build a list of random Vendor objects (Name = random, Fax = simulated "Company ID")
            const int VENDOR_COUNT = 5;
            const int STARTING_COMPANY_ID = 200;
            var vendorsToAdd = new List<Vendor>();
            for (int i = 0; i < VENDOR_COUNT; i++)
            {
                string randomName = "AdderTestVendor_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int companyID = STARTING_COMPANY_ID + i;
                string fax = companyID.ToString(); // reusing Fax field for our "Company ID"
                vendorsToAdd.Add(new Vendor(randomName, fax));
            }

            // 3) Call the method under test: VendorAdder.AddVendors(...)
            VendorAdder.AddVendors(vendorsToAdd);

            // 4) Verify each newly added vendor actually exists in QuickBooks by direct QBFC calls (no Reader code).
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var vend in vendorsToAdd)
                {
                    Assert.False(string.IsNullOrEmpty(vend.QB_ID),
                                 $"VendorAdder did not set QB_ID for {vend.Name}.");

                    var qbVendor = QueryVendorByListID(qbSession, vend.QB_ID);
                    Assert.NotNull(qbVendor);
                    Assert.Equal(vend.Name, qbVendor?.Name);
                    Assert.Equal(vend.Fax, qbVendor?.Fax);
                }
            }

            // 5) Cleanup: remove the test vendors from QB.
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var vend in vendorsToAdd.Where(v => !string.IsNullOrEmpty(v.QB_ID)))
                {
                    DeleteVendor(qbSession, vend.QB_ID);
                }
            }

            // 6) Ensure logs have been written and closed.
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);
            string logContents = File.ReadAllText(logFile);

            // 7) Verify that the Adder wrote expected log messages.
            Assert.Contains("VendorAdder Initialized", logContents);
            Assert.Contains("VendorAdder Completed", logContents);
            foreach (var vend in vendorsToAdd)
            {
                string expectedAddMsg = $"Successfully added {vend.Name} to QuickBooks";
                Assert.Contains(expectedAddMsg, logContents);
            }
        }

        /// <summary>
        /// Queries QuickBooks directly (without using the Reader code) for a single vendor
        /// by ListID and returns a Vendor object if found, or null if not found.
        /// </summary>
        private Vendor? QueryVendorByListID(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IVendorQuery vendorQueryRq = requestMsgSet.AppendVendorQueryRq();
            vendorQueryRq.ORVendorListQuery.ListIDList.Add(listID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0) return null;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode != 0) return null; // something went wrong

            // VendorRet list can contain multiple vendors, but we only asked for one ListID.
            IVendorRetList? vendRetList = response.Detail as IVendorRetList;
            if (vendRetList == null || vendRetList.Count == 0) return null;

            IVendorRet vendRet = vendRetList.GetAt(0);
            // Convert IVendorRet fields into your Vendor model
            var found = new Vendor(
                name: vendRet.Name?.GetValue() ?? "",
                fax: vendRet.Fax?.GetValue() ?? ""
            );
            found.QB_ID = vendRet.ListID?.GetValue() ?? "";
            return found;
        }

        /// <summary>
        /// Directly deletes a vendor from QuickBooks by its ListID (no Reader code).
        /// </summary>
        private void DeleteVendor(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IListDel listDelRq = requestMsgSet.AppendListDelRq();
            listDelRq.ListDelType.SetValue(ENListDelType.ldtVendor);
            listDelRq.ListID.SetValue(listID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0) return;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode == 0)
            {
                Debug.WriteLine($"Successfully deleted Vendor (ListID: {listID}).");
            }
            else
            {
                throw new Exception($"Error Deleting Vendor (ListID: {listID}): {response.StatusMessage} " +
                                    $"(Status code: {response.StatusCode}).");
            }
        }
    }
}