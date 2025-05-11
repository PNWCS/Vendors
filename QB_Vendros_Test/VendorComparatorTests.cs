using System.Diagnostics;
using Serilog;
using QB_Vendors_Lib; // ← where your VendorsComparator lives
using QBFC16Lib;
using static QB_Vendors_Test.CommonMethods; // for EnsureLogFileClosed, DeleteOldLogFiles, etc.
using Xunit;

namespace QB_Vendors_Test
{
    [Collection("Sequential Tests")]
    public class VendorsComparatorTests
    {
        [Fact]
        public void CompareVendors_InMemoryScenario_And_Verify_Logs()
        {
            // ── 0. housekeeping ───────────────────────────────────────────────
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            // ── 1. create five unique vendors in memory (company file) ─────
            const int START_ID = 10_000;
            var initialVendors = new List<Vendor>();

            for (int i = 0; i < 5; i++)
            {
                string suffix = Guid.NewGuid().ToString("N")[..8];
                initialVendors.Add(new Vendor(
                    $"TestVend_{suffix}",
                    $"TestCo_{suffix}",
                    $"{START_ID + i}"));
                Debug.WriteLine($"Vendor {i}: {initialVendors[i].Name}");
            }

            List<Vendor> firstCompareResult = new();
            List<Vendor> secondCompareResult = new();

            try
            {
                // ── 2. first compare – expect every vendor to be Added ─────
                firstCompareResult = VendorsComparator.CompareVendors(initialVendors);
                Debug.WriteLine("First compare result");
                foreach (var vendor in firstCompareResult)
                {
                    Debug.WriteLine(vendor);
                }

                foreach (var v in firstCompareResult
                                 .Where(v => initialVendors.Any(x => x.Company_ID == v.Company_ID)))
                {
                    Assert.Equal(VendorStatus.Added, v.Status);
                }

                // ── 3. mutate list: remove one   ➜ Missing
                //                  rename one     ➜ Different
                var updated = new List<Vendor>(initialVendors);
                var removed = updated[0];
                var renamed = updated[1];

                updated.Remove(removed);
                renamed.Name += "_Renamed";

                // ── 4. second compare – expect Missing, Different, Unchanged ─
                secondCompareResult = VendorsComparator.CompareVendors(updated);

                Debug.WriteLine("Second compare result");
                foreach (var vendor in secondCompareResult)
                {
                    Debug.WriteLine(vendor);
                }

                var dict = secondCompareResult.ToDictionary(v => v.Company_ID);

                Assert.Equal(VendorStatus.Missing, dict[removed.Company_ID].Status);
                Assert.Equal(VendorStatus.Different, dict[renamed.Company_ID].Status);

                foreach (var id in updated
                                   .Select(v => v.Company_ID)
                                   .Except(new[] { renamed.Company_ID }))
                {
                    Assert.Equal(VendorStatus.Unchanged, dict[id].Status);
                }
            }
            finally
            {
                // ── 5. clean up QB (remove Added vendors) ───────────────────
                var added = firstCompareResult?
                            .Where(v => !string.IsNullOrEmpty(v.QB_ID))
                            .ToList();

                if (added is { Count: > 0 })
                {
                    using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);
                    foreach (var v in added)
                        DeleteVendor(qb, v.QB_ID);
                }
            }

            // ── 6. verify logs ────────────────────────────────────────────────
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);
            string logs = File.ReadAllText(logFile);

            Assert.Contains("VendorsComparator Initialized", logs);
            Assert.Contains("VendorsComparator Completed", logs);

            void AssertLogged(IEnumerable<Vendor> vendors)
            {
                foreach (var v in vendors)
                    Assert.Contains($"Vendor {v.Name} is {v.Status}.", logs);
            }

            AssertLogged(firstCompareResult);
            AssertLogged(secondCompareResult);
        }

        // ───────────────────── helper: delete vendor from QB ──────────────
        private static void DeleteVendor(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest req = qbSession.CreateRequestSet();
            IListDel del = req.AppendListDelRq();
            del.ListDelType.SetValue(ENListDelType.ldtVendor);
            del.ListID.SetValue(listID);

            IMsgSetResponse resp = qbSession.SendRequest(req);
            WalkListDelResponse(resp, listID);
        }

        private static void WalkListDelResponse(IMsgSetResponse respSet, string listID)
        {
            IResponseList responses = respSet.ResponseList;
            if (responses is null || responses.Count == 0) return;

            IResponse resp = responses.GetAt(0);
            Debug.WriteLine(resp.StatusCode == 0
                ? $"Successfully deleted Vendor (ListID: {listID})."
                : $"Error deleting Vendor: {resp.StatusMessage}");
        }
    }
}