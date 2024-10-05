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
            public int rpmDisplayType = 0;   //0 = Raw, 1 = Interpolated, 2 = Interpolated and Raw
            public int rpmInterpolationSampleIntervalMs = 350;
            public float rpmInterpolationAggressiveness = 1.0f;
            public int rpmTextDisplayType = 0;   //0 = Raw, 1 = Interpolated
            public int speedDisplayUnit = 1;   //0 = mph, 1 = kmh
            public int temperatureUnit = 0;   //0 = °C, 1 = °F
            public int maxTransmissionGears = 6;
            public int minGear1RpmToChangeToGear2 = 4000;
            public int minGear1SpeedToChangeToGear2 = 30;
            public int maxPossibleGear1Speed = 62;
            public int maxPossibleGear2Speed = 95;
            public int maxPossibleGear3Speed = 120;
            public int maxPossibleGear4Speed = 142;
            public int maxPossibleGear5Speed = 156;
            public int maxPossibleGear6Speed = 170;
            public string letterToUseAsGearStopped = "S";
            public string letterToUseAsClutchPressed = "C";
            public int panelColorScheme = 0;   //0 = Automatic, 1 = Dark, 2 = Light

            public int playerVolume = 75;
            public bool resetSystemVolumeOnPlaySong = true;
            public bool randomizeMusicList = false;
            public int equalizerProfile = 0;   //0 = Disabled, 1 = Flat, 2 = Custom
            public int equalizerAmplifierValue = 0;
            public int equalizerBand31hz = 0;
            public int equalizerBand62hz = 0;
            public int equalizerBand125hz = 0;
            public int equalizerBand250hz = 0;
            public int equalizerBand500hz = 0;
            public int equalizerBand1khz = 0;
            public int equalizerBand2khz = 0;
            public int equalizerBand4khz = 0;
            public int equalizerBand8khz = 0;
            public int equalizerBand16khz = 0;
            public bool autoPauseOnStopVehicle = false;
            public bool autoPlayOnVehicleMove = false;
            public int outputSelectorEmulateMoveStep1x = 1920;
            public int outputSelectorEmulateMoveStep1y = -1080;
            public int outputSelectorEmulateMoveStep2x = -110;
            public int outputSelectorEmulateMoveStep2y = 0;
            public bool automaticVolume = false;
            public int mark1volumeSpeed = 0;
            public int mark1volumeTarget = 32;
            public int mark2volumeSpeed = 25;
            public int mark2volumeTarget = 45;
            public int mark3volumeSpeed = 34;
            public int mark3volumeTarget = 53;
            public int mark4volumeSpeed = 48;
            public int mark4volumeTarget = 63;
            public int mark5volumeSpeed = 65;
            public int mark5volumeTarget = 70;
            public int mark6volumeSpeed = 74;
            public int mark6volumeTarget = 86;
            public int mark7volumeSpeed = 85;
            public int mark7volumeTarget = 105;
            public int volumeBoostOnMaxRpm = 30;

            public int cameraProjectionQuality = 2; //0 = Low, 1 = Middle, 2 = High, 3 = Perfect
            public int cameraMaxQueuingFrames = 1;
            public bool cameraMultiThread = false;
            public bool cameraShowStatistics = false;
            public int cameraMiniviewSize = 0; //0 = Small, 1 = Medium, 2 = Large
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