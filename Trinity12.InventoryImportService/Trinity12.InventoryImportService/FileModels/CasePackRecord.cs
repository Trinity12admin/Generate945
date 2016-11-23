using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trinity12.InventoryImportService.FileModels
{
    public class CasePackRecord
    {
        public string Run { get; set; }
        public int Case { get; set; }
        public string Size { get; set; }
        public int Pairs { get; set; }
    }
}
