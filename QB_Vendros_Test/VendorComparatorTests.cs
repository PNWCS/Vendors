using System.Diagnostics;
using Serilog;
using QB_Vendors_Lib;      // Vendor, VendorStatus, VendorComparator
using QBFC16Lib;          // QuickBooks SDK
using static QB_Terms_Test.CommonMethods;  // Re-use logging helpers

namespace QB_Vendors_Test
{
    [Collection("Sequential Tests")]
    public class VendorComparatorTests
    {
        [Fact]
        public void CompareVendors_InMemoryScenario_And_Verify_Logs()
        {
            // ⚙  Test-data setup (five entirely new vendors)
            const string COMPANY_NAME = "ACME Widgets";
            const int    VENDOR_COUNT = 5;

            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            var initialVendors = new List<Vendor>();
            for (int i = 0; i < VENDOR_COUNT; i++)
            {
                string uniqueName = $"TestVendor_{Guid.NewGuid():N}".Substring(0, 22); // QB name ≤41 chars
                initialVendors.Add(new Vendor(uniqueName, COMPANY_NAME));
            }

            List<Vendor> firstCompareResult  = null;
            List<Vendor> secondCompareResult = null;

            try
            {
                // 🔄 1st compare → every vendor should be Added
                firstCompareResult = VendorComparator.CompareVendors(initialVendors);
                foreach (var v in firstCompareResult
                                 .Where(v => initialVendors.Any(x => x.Name == v.Name)))
                {
                    Assert.Equal(VendorStatus.Added, v.Status);
                }

                // ✏️  Mutate list: remove one (Missing) & rename another (Different)
                var updatedVendors   = new List<Vendor>(initialVendors);
                var removedVendor    = updatedVendors[0];         // → Missing
                var renamedVendor    = updatedVendors[1];         // → Different
                updatedVendors.Remove(removedVendor);
                renamedVendor.Name += "_Mod";

                // 🔄 2nd compare → expect Missing / Different / Unchanged
                secondCompareResult = VendorComparator.CompareVendors(updatedVendors);
                var secondDict      = secondCompareResult.ToDictionary(v => v.Name);

                // -- Missing
                Assert.Contains(removedVendor.Name, secondDict.Keys);
                Assert.Equal(VendorStatus.Missing, secondDict[removedVendor.Name].Status);

                // -- Different
                Assert.Contains(renamedVendor.Name, secondDict.Keys);
                Assert.Equal(VendorStatus.Different, secondDict[renamedVendor.Name].Status);

                // -- Unchanged
                foreach (var v in updatedVendors
                                 .Except(new[] { renamedVendor }))
                {
                    Assert.Equal(VendorStatus.Unchanged, secondDict[v.Name].Status);
                }
            }
            finally
            {
                // 🧹  Clean up: delete every vendor we added in pass 1
                var addedVendors = firstCompareResult?
                                   .Where(v => !string.IsNullOrEmpty(v.QB_ID))
                                   .ToList();

                if (addedVendors is { Count: >0 })
                {
                    using var qb   = new QuickBooksSession(AppConfig.QB_APP_NAME);
                    foreach (var v in addedVendors)
                        DeleteVendor(qb, v.QB_ID);
                }
            }

            // 📑  Log-file assertions
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);

            string log = File.ReadAllText(logFile);
            Assert.Contains("VendorComparator Initialized", log);
            Assert.Contains("VendorComparator Completed",   log);

            foreach (var v in firstCompareResult.Concat(secondCompareResult))
            {
                string expected = $"Vendor {v.Name} is {v.Status}.";
                Assert.Contains(expected, log);
            }
        }

        // QuickBooks cleanup helper
        private void DeleteVendor(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IListDel delRq         = request.AppendListDelRq();
            delRq.ListDelType.SetValue(ENListDelType.ldtVendor);
            delRq.ListID.SetValue(listID);

            IMsgSetResponse response = qbSession.SendRequest(request);
            WalkListDelResponse(response, listID);
        }

        private static void WalkListDelResponse(IMsgSetResponse resp, string listID)
        {
            var list = resp.ResponseList;
            if (list is null || list.Count == 0) return;

            var r = list.GetAt(0);
            Debug.WriteLine(r.StatusCode == 0
                ? $"✔ Deleted Vendor (ListID: {listID})."
                : $"✖ Delete Vendor failed: {r.StatusMessage}");
        }
    }
}
