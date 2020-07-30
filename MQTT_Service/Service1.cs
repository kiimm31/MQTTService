using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using SensorLogics;

namespace DP6_MQTT_Service
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            string ueios = ConfigurationManager.ConnectionStrings["UEIOS"].ConnectionString;
            string internalCS = ConfigurationManager.ConnectionStrings["INTERNAL"].ConnectionString;
            string sensorCS = ConfigurationManager.ConnectionStrings["SENSOR"].ConnectionString;
            string sensor61 = ConfigurationManager.ConnectionStrings["SENSOR_61"].ConnectionString;
            string ueios61 = ConfigurationManager.ConnectionStrings["UEIOS_61"].ConnectionString;
            string internal61 = ConfigurationManager.ConnectionStrings["INTERNAL_61"].ConnectionString;

            SensorLogics.SensorFunctions sensorLogics = new SensorFunctions(ueios, internalCS, sensorCS, sensor61, ueios61, internal61);
        }

        protected override void OnStop()
        {
        }
    }
}
