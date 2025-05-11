//using System;
//using System.Collections.Generic;
//using System.IO;
//using ClosedXML.Excel;
//using QB_Vendors_Lib;

//namespace QB_Vendors_CLI
//{
//    public class Sample
//    {
//        public static void Main(string[] args)
//        {
//            LoggerConfig.ConfigureLogging();

//            string filePath = "C:\\Users\\DampuriR\\Downloads\\Example_Company_Excel.xlsx";

//            List<Vendor> companyVendors = new List<Vendor>();

//            // Ensure file exists
//            if (!File.Exists(filePath))
//                throw new FileNotFoundException($"The file '{filePath}' does not exist.");

//            using (var workbook = new XLWorkbook(filePath))
//            {
//                var worksheet = workbook.Worksheet("vendors");

//                // Get the range of used rows
//                var range = worksheet.RangeUsed();
//                if (range == null)
//                {
//                    Console.WriteLine("Warning: The worksheet is empty or contains no used range.");
//                }
//                else
//                {
//                    var rows = range.RowsUsed();
//                    foreach (var row in rows.Skip(1)) // Skip header row
//                    {
//                        string name = row.Cell(1).GetString().Trim();      // Column "Name"
//                        string companyName = row.Cell(2).GetString().Trim(); // Column "CompanyName"

//                        companyVendors.Add(new Vendor(name, companyName));
//                    }
//                }
//            }

//            List<Vendor> vendors = VendorComparator.CompareAndSync(companyVendors);
//            foreach (var vendor in vendors)
//            {
//                Console.WriteLine($"Vendor {vendor.Name} has the {vendor.Status} Status");
//            }

//            Console.WriteLine("Vendor Data Sync Completed");
//        }
//    }
//}