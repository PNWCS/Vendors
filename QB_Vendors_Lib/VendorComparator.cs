using System.Diagnostics;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace QB_Vendors_Lib
{
    public class VendorComparator
    {
        // Static dictionary to store the last set of vendors that were processed
        // This simulates what would normally be retrieved from QuickBooks
        private static Dictionary<string, Vendor> _lastProcessedVendors = new();

        public static List<Vendor> CompareVendors(List<Vendor> companyVendors)
        {
            Log.Information("VendorsComparator Initialized");

            // Create result list to store all vendors with their determined status
            List<Vendor> resultList = new List<Vendor>();

            // Create dictionaries for faster lookups
            var companyDict = companyVendors.ToDictionary(v => v.Company_ID);

            // First, find vendors that exist in _lastProcessedVendors but not in companyVendors (Missing)
            foreach (var lastVendor in _lastProcessedVendors.Values)
            {
                if (!companyDict.ContainsKey(lastVendor.Company_ID))
                {
                    // This vendor was previously processed but is now removed from the company list
                    var missingVendor = new Vendor(
                        lastVendor.Name,
                        lastVendor.Fax,
                        lastVendor.Company_ID)
                    {
                        QB_ID = lastVendor.QB_ID,
                        Status = VendorStatus.Missing
                    };
                    resultList.Add(missingVendor);
                    Log.Information("Vendor {Name} is Missing.", missingVendor.Name);
                }
            }

            // Next, process all company vendors
            foreach (var companyVendor in companyVendors)
            {
                Vendor resultVendor;

                if (_lastProcessedVendors.TryGetValue(companyVendor.Company_ID, out var lastVendor))
                {
                    // Vendor exists in both sets, check for differences
                    resultVendor = new Vendor(
                        companyVendor.Name,
                        companyVendor.Fax,
                        companyVendor.Company_ID)
                    {
                        QB_ID = lastVendor.QB_ID
                    };

                    if (lastVendor.Name == companyVendor.Name)
                    {
                        resultVendor.Status = VendorStatus.Unchanged;
                        Log.Information("Vendor {Name} is Unchanged.", resultVendor.Name);
                    }
                    else
                    {
                        resultVendor.Status = VendorStatus.Different;
                        Log.Information("Vendor {Name} is Different.", resultVendor.Name);
                    }
                }
                else
                {
                    // Vendor is new (not in _lastProcessedVendors)
                    resultVendor = new Vendor(
                        companyVendor.Name,
                        companyVendor.Fax,
                        companyVendor.Company_ID)
                    {
                        // Simulate adding to QB and getting a QB_ID
                        QB_ID = "QB_" + companyVendor.Company_ID,
                        Status = VendorStatus.Added
                    };
                    Log.Information("Vendor {Name} is Added.", resultVendor.Name);
                }

                resultList.Add(resultVendor);
            }

            // Update our cache for next comparison
            _lastProcessedVendors = resultList
                .Where(v => v.Status != VendorStatus.Missing)
                .ToDictionary(v => v.Company_ID);

            Log.Information("VendorsComparator Completed");
            return resultList;
        }
    }
}