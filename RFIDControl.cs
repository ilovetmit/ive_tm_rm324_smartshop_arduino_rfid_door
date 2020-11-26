using MySql.Data.MySqlClient;
using RFIDReaderAPI;
using RFIDReaderAPI.Interface;
using RFIDReaderAPI.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartShop
{
    class RFIDControl : IAsynchronousMessage
    {
        /**********************************************
         * MySql Setting
         **********************************************/
        private static readonly MySqlConnection connectDB = new MySqlConnection(CONNECT);
        private static MySqlDataReader dbResponse;
        private const string DBHOST = "192.168.15.175";
        private const string DBPORT = "3306";
        private const string DBUSER = "rfid";
        private const string DBPASSWORD = "Qwe!23";
        private const string DBNAME = "192";
        private const string CONNECT = "server=" + DBHOST + ";port=" + DBPORT + ";uid=" + DBUSER + ";pwd=" +
                                       DBPASSWORD + ";database=" + DBNAME;

        /**********************************************
         * RFID Setting
         **********************************************/
        private const string socketURL = "192.168.15.111:9090";
        private static readonly IAsynchronousMessage self = new RFIDControl();

        /**********************************************
         * Global Vaiables
         **********************************************/
        private static bool warningLock = false;
        private static Dictionary<string, bool> dict;
        private static ArrayList cache = new ArrayList();
        private static string DictionaryHash = null;

        public static void Main(string[] args)
        {

            /* connectDB.Open();
            _ = KeepUpdating();
            Console.ReadLine();
            foreach (KeyValuePair<string, bool> entry in dict)
            {
                Console.WriteLine("TID= " + entry.Key + "   is_sold= " + entry.Value);
            }*/


            if (RFIDReader.CreateTcpConn(socketURL, self))    // Send ICMP(ping) package
            {
                Console.WriteLine("Connect RFID Reader Succeed!");
                connectDB.Open();
                Console.WriteLine("Connect Database Succeed!");

                string command;
                while (true)
                {
                    command = Console.ReadLine();
                    if ("exit".Equals(command))
                    {
                        connectDB.Close();
                        RFIDReader.CloseAllConnect();
                        Environment.Exit(0);
                    }
                    else if ("write".Equals(command))
                    {
                        string srt = RFIDReader._Tag6C.WriteEPC(socketURL, eAntennaNo._1 | eAntennaNo._2, "1111", "0003"); // 0003?
                        Console.WriteLine("Result :" + srt);
                    }
                    else if ("insert".Equals(command))
                    {
                        string value = "";
                        foreach (string tid in cache) {
                            value += "(1,'" + tid + "',0),";
                        }
                        value = value.TrimEnd(',');
                        value += ";";

                        string checkSQL = "INSERT INTO shop_product_inventories (shop_product_id,rfid_code,is_sold) VALUES " + value;
                        MySqlCommand runSQL = new MySqlCommand(checkSQL, connectDB);
                        runSQL.ExecuteNonQuery();
                    }
                    else if ("scan".Equals(command))
                    {
                        _ = KeepUpdating();
                        if (RFIDReader._Tag6C.GetEPC_TID(socketURL, eAntennaNo._1 | eAntennaNo._2, eReadType.Inventory) == 0)   // Goto OutPutTags function
                            Console.WriteLine("Scanning Tag...");
                        else
                            Console.WriteLine("Open Stream Fail! Ensure only one device is connecting to the RFID reader.");

                    }
                    else if ("kill".Equals(command))
                    {
                        int rt = RFIDReader._Tag6C.Destroy_MatchEPC(socketURL, eAntennaNo._1 | eAntennaNo._2, "76578990", "4321", 0);
                        Console.WriteLine("Result :" + rt);
                    }
                    else if ("stop".Equals(command))
                    {
                        RFIDReader._RFIDConfig.Stop(socketURL);
                    }
                    else if ("on".Equals(command))
                    {
                        ConfigGPO(eGPOState.High);
                    }
                    else if ("off".Equals(command))
                    {
                        ConfigGPO(eGPOState.Low);
                    }
                }

            }
            else
            {
                Console.WriteLine("Create TCP Connection Failed! Ensure the power of RFID reader is on and the IP address and port number is connect.");
            }

        }

        /**********************************************
         * Interface implement function
         **********************************************/
        public void OutPutTags(Tag_Model tag)
        {
            if (dict.ContainsKey(tag.EPC) == false)
                dict.Add(tag.EPC, false);
               
            Console.WriteLine("readName:" + tag.ReaderName + " ,antNo." + tag.ANT_NUM + " ,EPC:" + tag.EPC + " ,TID: " + tag.TID);

            if (!warningLock)
            {

                if (dict[tag.EPC] == false)
                {
                    Console.WriteLine("Warning");
                    _ = TurnOnAlerm();              // 3 seconds
                }
                else
                {
                    Console.WriteLine("Valid");
                }
            }
        }

        public void GPIControlMsg(GPI_Model gpi_model)
        {
            //
        }

        public void OutPutTagsOver()
        {
            //
        }

        public void PortClosing(string socketURL)
        {
            //
        }

        public void PortConnecting(string socketURL)
        {
            //
        }

        public void WriteDebugMsg(string msg)
        {
            //
        }

        public void WriteLog(string msg)
        {
            //
        }

        /**********************************************
         * Personal function
         **********************************************/
        private static void ConfigGPO(eGPOState state, eGPO gpo = eGPO._1)
        {
            RFIDReader._ReaderConfig.SetReaderGPOState(socketURL, new Dictionary<eGPO, eGPOState>() { { gpo, state } });
        }

        private static void UpdateDictionary()
        {
            MySqlCommand runSQL;
            string checkSQL = "SELECT MD5(GROUP_CONCAT(is_sold)) AS hash FROM shop_product_inventories;";
            runSQL = new MySqlCommand(checkSQL, connectDB);
            dbResponse = runSQL.ExecuteReader();
            dbResponse.Read();
            string hash = dbResponse.GetString("hash");
            dbResponse.Close();
            if (!hash.Equals(DictionaryHash))
            {
                DictionaryHash = hash;
                string SQL = "SELECT rfid_code, is_sold FROM shop_product_inventories;";
                runSQL = new MySqlCommand(SQL, connectDB);
                dbResponse = runSQL.ExecuteReader();

                Dictionary<string, bool> newDict = new Dictionary<string, bool>();
                while (dbResponse.Read())
                {
                    bool signal = false;
                    Console.WriteLine(dbResponse.GetInt16("is_sold"));
                    if (dbResponse.GetInt16("is_sold") == 2)
                        signal = true;
                    newDict.Add(dbResponse.GetString("rfid_code"), signal);
                }
                dbResponse.Close();
                dict = newDict;
                Console.WriteLine("Updated");
            }
            
        }

        /**********************************************
         * Async Task
         **********************************************/
        private static async Task TurnOnAlerm()
        {
            warningLock = true;
            ConfigGPO(eGPOState.High);
            await Task.Delay(3000);
            ConfigGPO(eGPOState.Low);
            warningLock = false;
        }

        private static async Task KeepUpdating()
        {
            while (true)
            {
                UpdateDictionary();
                await Task.Delay(5000);
            }
        }
    }
}
