using System;
using QB_Customers_Lib; // Assuming you have a similar library for vendors
using QB_Vendors_Lib;
using QBFC16Lib;

namespace QB_Vendors_CLI
{
    class DeleteAllVendors
    {
        static void Main()
        {
            try
            {
                using var qbSession = new QuickBooksSession("QB Vendor Cleanup Tool");

                // Step 1: Query all vendors
                var allVendors = VendorReader.QueryAllVendors();

                Console.WriteLine($"Found {allVendors.Count} vendors.");

                foreach (var vendor in allVendors)
                {
                    DeleteVendor(qbSession, vendor.QB_ID);
                }

                Console.WriteLine("All vendors deleted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred: " + ex.Message);
            }
        }

        private static void DeleteVendor(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest req = qbSession.CreateRequestSet();
            IListDel del = req.AppendListDelRq();
            del.ListDelType.SetValue(ENListDelType.ldtVendor);
            del.ListID.SetValue(listID);

            IMsgSetResponse resp = qbSession.SendRequest(req);

            IResponseList responses = resp.ResponseList;
            if (responses is null || responses.Count == 0) return;

            IResponse respItem = responses.GetAt(0);

            if (respItem.StatusCode == 0)
                Console.WriteLine($"Deleted Vendor (ListID: {listID})");
            else
                Console.WriteLine($"Failed to delete (ListID: {listID}): {respItem.StatusMessage}");
        }
    }
}