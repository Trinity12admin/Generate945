using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;

namespace Trinity12.InventoryImportService
{
    public class InventoryRecord
    {
        public string Brand { get; set; }
        public string Pattern { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public int OnHand { get; set; }
        public int Future { get; set; }
        public string SKU { get; set; }
        public double Price { get; set; }
    }

    public sealed class InventoryRecordMap : CsvClassMap<InventoryRecord>
    {
        public InventoryRecordMap()
        {
            Map(m => m.Brand).Name("BRAND");
            Map(m => m.Pattern).Name("PATTERN");
            Map(m => m.Color).Name("COLOR");
            Map(m => m.Size).Name("SIZE");
            Map(m => m.OnHand).Name("ONHAND");
            Map(m => m.Future).Name("FUTURE");
            Map(m => m.SKU).Name("SKU");
            Map(m => m.Price).Name("PRICE");
        }
    }
}
