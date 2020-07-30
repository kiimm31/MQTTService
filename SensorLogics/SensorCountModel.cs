using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SensorLogics
{
    public class SensorCountModel
    {
        public int SensorID { get; set; }
        public string SensorSerialNumber { get; set; }
        public int Count { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
