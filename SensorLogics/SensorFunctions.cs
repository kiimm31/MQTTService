using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Diagnostics;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Dapper;
using System.Data;
using System.Data.SqlClient;
using Newtonsoft.Json;
using System.Configuration;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Threading;

namespace SensorLogics
{
    public class SensorFunctions
    {
        public IMqttClient _mqttClient;
        public List<string> TopicStrings;
        private string UEIOSConnectionString;
        private string InternalConnectionString;
        private string SensorConnectionString;
        private string Sensor61ConnectionString;
        private string ueios61ConnectionString;
        private string internal61ConnectionString;

        public SensorFunctions(string ueiosCS, string internalCS, string sensorCS, string sensor61, string ueios61, string internal61)
        {

            if (string.IsNullOrWhiteSpace(ueiosCS) == false &&
                string.IsNullOrWhiteSpace(internalCS) == false &&
                string.IsNullOrWhiteSpace(sensorCS) == false &&
                string.IsNullOrWhiteSpace(sensor61) == false &&
                string.IsNullOrWhiteSpace(ueios61) == false &&
                string.IsNullOrWhiteSpace(internal61) == false)
            {
                UEIOSConnectionString = ueiosCS;
                InternalConnectionString = internalCS;
                SensorConnectionString = sensorCS;
                Sensor61ConnectionString = sensor61;
                ueios61ConnectionString = ueios61;
                internal61ConnectionString = internal61;
                TopicStrings = getListOfMQTTTopic();
            }

            Console.WriteLine("Start");
            if(TopicStrings != null && TopicStrings.Count > 0)
            {
                SubscribeToMQTTTopic(TopicStrings);
            }
            
            StartTimer();
        }

        private async void SubscribeToMQTTTopic(List<string> Topics)
        {
            try
            {
                if (_mqttClient == null || _mqttClient.IsConnected == false)
                {
                    //continue connect
                }
                else
                {
                    await _mqttClient.DisconnectAsync();
                    _mqttClient.UseApplicationMessageReceivedHandler(HandleReceivedApplicationMessage);
                }

                Console.WriteLine("Connecting...");
                
                //Step 1 Set MQTT TLS
                MqttFactory mqttFactory = new MqttFactory();

                MqttClientTlsOptions tlsOptions = new MqttClientTlsOptions
                {
                    UseTls = false,
                    IgnoreCertificateChainErrors = true,
                    IgnoreCertificateRevocationErrors = true,
                    AllowUntrustedCertificates = true
                };
                
                //Step 2 Set MQTT Client Options
                MqttClientOptions options = new MqttClientOptions
                {
                    ClientId = Guid.NewGuid().ToString(),
                    ProtocolVersion = MqttProtocolVersion.V500
                };

                options.ChannelOptions = new MqttClientTcpOptions
                {
                    //Server = "broker.hivemq.com",
                    Server = ConfigurationManager.AppSettings.Get("brokerServer"),
                    //Port = 1883,
                    Port = int.Parse(ConfigurationManager.AppSettings.Get("brokerPort")),
                    TlsOptions = tlsOptions
                };

                if (options.ChannelOptions == null)
                {
                    throw new InvalidOperationException();
                }

                options.CleanSession = true;
                options.KeepAlivePeriod = TimeSpan.FromSeconds(15);
                options.ProtocolVersion = MqttProtocolVersion.V311;
                                
                _mqttClient = mqttFactory.CreateMqttClient();

                _mqttClient.UseApplicationMessageReceivedHandler(HandleReceivedApplicationMessage);

                await _mqttClient.ConnectAsync(options);

                foreach (string topic in Topics)
                {
                    TopicFilter topicFilter = new TopicFilter { Topic = topic, QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce };
                    await _mqttClient.SubscribeAsync(topicFilter);
                }
            }
            catch (Exception ex)
            {
               /* Console.WriteLine(ex);*/
                //throw ex;
                Thread.Sleep(1000);
            }

        }

        private async Task HandleReceivedApplicationMessage(MqttApplicationMessageReceivedEventArgs arg)
        {
            try
            {
                string receivedString = arg.ApplicationMessage.ConvertPayloadToString();

                switch (arg.ApplicationMessage.Topic.ToUpper())
                {
                    case "SENSORCOUNT":
                        if (string.IsNullOrWhiteSpace(receivedString) == false)
                        {
                            bool mapped = loggingSensorCountToDB(receivedString);
                        }
                        break;
                    default:
                        bool success = loggingReceivedStringToDB(receivedString, arg.ApplicationMessage.Topic);
                        break;
                }
            }
            catch (Exception)
            {
                SubscribeToMQTTTopic(TopicStrings);
                /*throw ex;*/
            }

        }
        private bool loggingSensorCountToDB(string receivedString)
        {
            //SensorCountModel receivedModel = JsonConvert.DeserializeObject<SensorCountModel>(receivedString);
            List<string> listOfRecievedInfo = new List<string>();
            // received String will be in this Format "<S/N> "
            List<int> returnData = new List<int>();
            List<string> returnString = new List<string>();
            int facilityID = 0;
            int BRS_ID = 0;
            string orderNumber = "";
            int cavity = 0;

            SensorCountModel receivedModel = null;

            if (receivedString != null)
            {
                receivedModel = new SensorCountModel();
                receivedModel.SensorSerialNumber = receivedString;
                receivedModel.Timestamp = DateTime.Now.ToLocalTime();
            }

            if (receivedModel != null && receivedModel.SensorSerialNumber != "0xTEST")
            {
                using (IDbConnection connection = new SqlConnection(ueios61ConnectionString))
                {
                    string query = string.Format(@"SELECT BRS.ID BATCH_RUN_STEP_ID from XDK_SENSOR XS
                        inner join XDK_SENSOR_FACILITY_ASSET_RELATIONSHIP XSFAR on XSFAR.XDK_SENSOR_LINK_ID = XS.ID and isnull(XSFAR.IS_DELETED,0) = 0
                        inner join BATCH_RUN_STEP_EQUIPMENT BRSE on BRSE.FACILITY_ASSET_ID = XSFAR.FACILITY_ASSET_LINK_ID and isnull(BRSE.IS_DELETED,0) = 0
                        inner join BATCH_RUN_STEP BRS ON BRS.ID= BRSE.BATCH_RUN_STEP_ID AND BRS.STATUS in (3,6) and isnull(BRS.IS_DELETED,0) = 0
                        WHERE BRS.START_DATE_TIME is not null and BRS.END_DATE_TIME is null and XS.SERIAL_NUMBER = '{0}'", receivedModel.SensorSerialNumber);

                    returnData = connection.Query<int>(query).ToList();
                    foreach (int data in returnData) BRS_ID = data;

                    query = string.Format(@"SELECT XSFAR.FACILITY_ASSET_LINK_ID FROM XDK_SENSOR_FACILITY_ASSET_RELATIONSHIP XSFAR
                        inner join XDK_SENSOR XS on XS.ID = XSFAR.XDK_SENSOR_LINK_ID
                        WHERE XS.SERIAL_NUMBER = '{0}'", receivedModel.SensorSerialNumber);

                    returnData = connection.Query<int>(query).ToList();
                    foreach (int data in returnData) facilityID = data;

                    //GET ORDER NUMBER -----------------------------------------------------------------------------------------------------------------------------
                    query = string.Format(@"SELECT BR.NAME from XDK_SENSOR XS
                        inner join XDK_SENSOR_FACILITY_ASSET_RELATIONSHIP XSFAR on XSFAR.XDK_SENSOR_LINK_ID = XS.ID and isnull(XSFAR.IS_DELETED,0) = 0
                        inner join BATCH_RUN_STEP_EQUIPMENT BRSE on BRSE.FACILITY_ASSET_ID = XSFAR.FACILITY_ASSET_LINK_ID and isnull(BRSE.IS_DELETED,0) = 0
                        inner join BATCH_RUN_STEP BRS ON BRS.ID= BRSE.BATCH_RUN_STEP_ID AND BRS.STATUS in (3,6) and isnull(BRS.IS_DELETED,0) = 0
                        inner join BATCH_RUN BR ON BR.ID = BRS.BATCH_RUN_ID and isnull(BR.IS_DELETED,0) = 0
                        WHERE BRS.START_DATE_TIME is not null and BRS.END_DATE_TIME is null and XS.SERIAL_NUMBER = '{0}'", receivedModel.SensorSerialNumber);

                    returnString = connection.Query<string>(query).ToList();
                    foreach (string data in returnString) orderNumber = data;
                }

                using (IDbConnection connection = new SqlConnection(internal61ConnectionString))
                {
                    string query = string.Format(@"SELECT TOP (1) CAVITY FROM V_DP_SAP_TEMPLATE_SP
                        WHERE [PRODUCTION ORDER NUMBER] = '{0}' AND CAVITY > 0
                        ORDER BY DATE DESC", orderNumber);
                    returnData = connection.Query<int>(query).ToList();
                    foreach (int data in returnData) cavity = data;
                    receivedModel.Count = cavity;
                }
                
                using (IDbConnection connection = new SqlConnection(SensorConnectionString))
                {
                   /* Console.Write(receivedModel.SensorSerialNumber); Console.Write(" ");
                    Console.Write(receivedModel.Count); Console.Write(" ");
                    Console.WriteLine(receivedModel.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));*/

                    string insert = string.Format(@"INSERT INTO [XDK_INCREMENT_DATA] (DEVICE_ID, DATE_TIME, INCREMENT_VALUE, BATCH_RUN_STEP_ID, CAVITY)
                    VALUES ('{0}','{1}', {2}, '{3}', '{4}')", receivedModel.SensorSerialNumber, receivedModel.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"), receivedModel.Count, BRS_ID, cavity);
                    int affectedRows = connection.Execute(insert);

                    insert = string.Format(@"UPDATE SENSOR_HEARTBEAT SET BATCH_RUN_STEP_ID = {0}, DATE_TIME = '{2}' WHERE DEVICE_ID = '{1}' IF @@ROWCOUNT = 0
                    INSERT INTO SENSOR_HEARTBEAT (DEVICE_ID, DATE_TIME, BATCH_RUN_STEP_ID) VALUES('{1}', '{2}', '{0}')",
                       BRS_ID, receivedModel.SensorSerialNumber, receivedModel.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                    affectedRows = connection.Execute(insert);
                    if (affectedRows > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

/*        private List<string> decodeRecievedString(string receivedString)
        {
            List<string> returnString = new List<string>();
            if (string.IsNullOrWhiteSpace(receivedString) == false)
            {
                char[] delimiter = { ';' };

                returnString = receivedString.Split(delimiter).ToList();

            }

            return returnString;
        }*/

        private bool loggingReceivedStringToDB(string receivedString, string topic)
        {
            using(IDbConnection connection = new SqlConnection(InternalConnectionString))
            {
                string insert = string.Format(@"INSERT INTO INCOMING_ORDER_ARCHIVE (DATE_TIME,INCOMING_MESSAGE,TYPE,INCOMING_MESSAGE_JSON)
                                                    VALUES ( GETDATE(),'{0}',{1},'{2}')", topic,1,receivedString);

                int affectedRows = connection.Execute(insert);

                if(affectedRows > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private List<string> getListOfMQTTTopic()
        {
            List<string> returnList = new List<string>();

            using (IDbConnection connection = new SqlConnection(UEIOSConnectionString))
            {
                string query = @"SELECT 
                [VALUE]
                FROM SETTING
                WHERE [TARGET] = 'MQTT TOPIC'
                AND ISNULL(IS_DELETED,0) = 0";

                returnList = connection.Query<string>(query).ToList();
            }
            return returnList;
        }

        private void StartTimer()
        {
            Console.WriteLine("Starting timer.");
            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += new EventHandler(TimerEvent_tick);
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            while (_mqttClient.IsConnected == false) ;
            timer.Start();
        }

        private void TimerEvent_tick(object sender, EventArgs e)
        {
            //Console.Write("TICK");
         
            if(_mqttClient.IsConnected) //still connected
            {
                //Console.WriteLine("Connected.");
                //do nothing
                //Console.WriteLine(" - TICK");
            }
            else
            {
                Console.WriteLine("Disconnected.");
                SubscribeToMQTTTopic(TopicStrings);
            }
            
        }
    }
}
