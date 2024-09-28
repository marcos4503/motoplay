using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Motoplay.Scripts
{
    /*
     * This class manage the load and save of Motoplay settings
    */

    public class Preferences
    {
        //Classes of script
        public class LoadedData
        {
            //*** Data to be saved ***//

            public SaveInfo[] saveInfo = new SaveInfo[0];

            public string appLang = "en-us";
            public ConfiguredObdBtAdapter configuredObdBtAdapter = new ConfiguredObdBtAdapter();

            public float keyboardHeightScreenPercent = 0.45f;

            public string bluetoothSerialPortToUse = "/dev/rfcomm14";
            public int bluetoothSerialPortChannelToUse = 1;
            public int invervalOfObdConnectionRetry = 30;
            public int maxOfObdConnectionRetry = 999999999;
            public int bluetoothBaudRate = 9600;
            public int vehicleMaxRpm = 18000;
        }

        //Private variables
        private string saveFilePath = "";

        //Public variables
        public LoadedData loadedData = null;

        //Core methods

        public Preferences(string saveFilePath)
        {
            //Check if save file exists
            bool saveExists = File.Exists(saveFilePath);

            //Store the save path
            this.saveFilePath = saveFilePath;

            //If have a save file, load it
            if (saveExists == true)
                Load();
            //If a save file don't exists, create it
            if (saveExists == false)
                Save();
        }

        private void Load()
        {
            //Load the data
            string loadedDataString = File.ReadAllText(saveFilePath);

            //Convert it to a loaded data object
            loadedData = JsonConvert.DeserializeObject<LoadedData>(loadedDataString);
        }

        //Public methods

        public void Save()
        {
            //If the loaded data is null, create one
            if (loadedData == null)
                loadedData = new LoadedData();

            //Save the data
            File.WriteAllText(saveFilePath, JsonConvert.SerializeObject(loadedData));

            //Load the data to update loaded data
            Load();
        }
    }

    /*
     * Auxiliar classes
     * 
     * Classes that are objects that will be used, only to organize data inside 
     * "LoadedData" object in the saves.
    */

    public class SaveInfo
    {
        public string key = "";
        public string value = "";
    }

    public class ConfiguredObdBtAdapter()
    {
        public bool haveConfigured = false;
        public string deviceName = "";
        public string deviceMac = "";
        public string devicePassword = "";
    }
}