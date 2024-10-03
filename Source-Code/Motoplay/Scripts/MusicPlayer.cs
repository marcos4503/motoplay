using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Motoplay.Scripts.MusicPlayer;
using System.Xml.Linq;
using static Motoplay.Scripts.MusicPlayer.ClassDelegates;
using static Motoplay.Scripts.ObdAdapterHandler.ClassDelegates;

namespace Motoplay.Scripts
{
    /*
     * This class manage the Music Playing using LibVLCSharp
    */

    public class MusicPlayer
    {
        //Enums of script
        public enum MusicMetadata
        {
            Current,
            Next2,
            Next3,
            Next4,
            Next5
        }

        //Classes of script
        public class ClassDelegates
        {
            public delegate void OnStartLoadingNewMusic();
            public delegate void OnLoadMusicMetadata(MusicMetadata music, Bitmap coverBitmap, string musicName, string musicAuthor, string musicExtension);
            public delegate void OnPaused();
            public delegate void OnPlayed();
            public delegate void OnStopped();
            public delegate void OnUpdateTime(string currentTime, string maxTime, float percentProgress);
            public delegate void OnFinished();
        }

        //Cache variables
        private MediaPlayer currentPlayingMedia = null;
        private DateTime lastMediaTimeUpdate = DateTime.Now;

        //Private variables
        private LibVLC libVlc = null;
        private event ClassDelegates.OnPaused onPaused = null;
        private event ClassDelegates.OnPlayed onPlayed = null;
        private event ClassDelegates.OnStopped onStopped = null;
        private event ClassDelegates.OnUpdateTime onUpdateTime = null;
        private event ClassDelegates.OnFinished onFinished = null;
        private event ClassDelegates.OnStartLoadingNewMusic onStartLoadingNewMusic = null;
        private event ClassDelegates.OnLoadMusicMetadata onLoadMusicMetadata = null;
        private int volumePercent = 0;
        private Equalizer activeEqualizer = null;

        //Core methods

        public MusicPlayer(int volumePercent)
        {
            //Initialize the LibVLCSharp
            Core.Initialize();

            //Create a lib vlc instance
            libVlc = new LibVLC("--file-caching=15000", "--disc-caching=15000");

            //Set the initial volume
            this.volumePercent = volumePercent;
        }
    
        //Public methods

        public void RegisterOnPausedCallback(ClassDelegates.OnPaused onPaused)
        {
            //Register the callback
            this.onPaused = onPaused;
        }

        public void RegisterOnPlayedCallback(ClassDelegates.OnPlayed onPlayed)
        {
            //Register the callback
            this.onPlayed = onPlayed;
        }

        public void RegisterOnStoppedCallback(ClassDelegates.OnStopped onStopped)
        {
            //Register the callback
            this.onStopped = onStopped;
        }

        public void RegisterOnUpdateTimeCallback(ClassDelegates.OnUpdateTime onUpdateTime)
        {
            //Register the callback
            this.onUpdateTime = onUpdateTime;
        }

        public void RegisterOnFinishedTimeCallback(ClassDelegates.OnFinished onFinished)
        {
            //Register the callback
            this.onFinished = onFinished;
        }

        public void RegisterOnStartLoadingNewMusicCallback(ClassDelegates.OnStartLoadingNewMusic onStartLoadingNewMusic)
        {
            //Register the callback
            this.onStartLoadingNewMusic = onStartLoadingNewMusic;
        }

        public void RegisterOnLoadMusicMetadataCallback(ClassDelegates.OnLoadMusicMetadata onLoadMusicMetadata)
        {
            //Register the callback
            this.onLoadMusicMetadata = onLoadMusicMetadata;
        }

        //Control methods

        public void ChangeMusicTo(string[] currentMusicAndNext4musicsFilePaths, bool andPlay)
        {
            //Start a new thread to change music
            new Thread(() =>
            {
                //If already exists a current playing media, despose it
                if (currentPlayingMedia != null)
                    Stop();

                //Create a new media to play
                currentPlayingMedia = new MediaPlayer(new Media(libVlc, currentMusicAndNext4musicsFilePaths[0]));

                //Load metadata of current music and next 4 musics
                LoadMusicMetadata(currentMusicAndNext4musicsFilePaths);

                //Reset the timer of time changed callback
                lastMediaTimeUpdate = DateTime.Now;

                //Prepare the callback of time
                currentPlayingMedia.TimeChanged += (s, e) =>
                {
                    //If not passed more than 200ms since last time update, cancel
                    if (new TimeSpan(DateTime.Now.Ticks - lastMediaTimeUpdate.Ticks).TotalMilliseconds < 200)
                        return;

                    //Run on Main Thread
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        //Get times
                        TimeSpan currentTime = TimeSpan.FromMilliseconds(e.Time);
                        TimeSpan totalTime = TimeSpan.FromMilliseconds(currentPlayingMedia.Length);

                        //Do the callback
                        if (onUpdateTime != null)
                            onUpdateTime(currentTime.ToString(@"mm\:ss"), totalTime.ToString(@"mm\:ss"), (((float)currentTime.Ticks / (float)totalTime.Ticks) * 100.0f));

                    }, DispatcherPriority.MaxValue);

                    //Inform the new time of last time update
                    lastMediaTimeUpdate = DateTime.Now;
                };

                //Prepare the callback of finish
                currentPlayingMedia.EndReached += (s, e) =>
                {
                    //Create a new thread to run the code. This will allow the current thread that is playing, finish, and the new thread will run the callback code
                    new Thread(() =>
                    {
                        //Run on Main Thread
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            //Do the callback
                            if (onFinished != null)
                                onFinished();

                        }, DispatcherPriority.MaxValue);

                        //...
                    }).Start();
                };

                //Set the current volume
                currentPlayingMedia.Volume = volumePercent;

                //Setup the equalizer, if have one
                if (activeEqualizer != null)
                    currentPlayingMedia.SetEqualizer(activeEqualizer);

                //Set the audio delay
                currentPlayingMedia.SetAudioDelay((long)(new TimeSpan(TimeSpan.FromMilliseconds(250).Ticks).TotalMicroseconds));

                //If is desired to auto play, play it
                if (andPlay == true)
                    Play();

                //If is not desired auto play, send pause callback to simulate it
                if (andPlay == false)
                    if (onPaused != null)
                        Dispatcher.UIThread.Invoke(() => { onPaused(); }, DispatcherPriority.MaxValue);

                //...
            }).Start();

            //Send callback of start loading new music
            if (onStartLoadingNewMusic != null)
                onStartLoadingNewMusic();
        }

        public bool isPlaying()
        {
            //Return the information
            return currentPlayingMedia.IsPlaying;
        }

        public void Play()
        {
            //If is already playing, cancel
            if (currentPlayingMedia.IsPlaying == true)
                return;

            //Play the music
            currentPlayingMedia.Play();

            //Do the callback on main thread
            if (onPlayed != null)
                Dispatcher.UIThread.Invoke(() => { onPlayed(); }, DispatcherPriority.MaxValue);
        }

        public void Pause()
        {
            //If is already not playing, cancel
            if (currentPlayingMedia.IsPlaying == false)
                return;

            //Pause the music
            currentPlayingMedia.Pause();

            //Do the callback on main thread
            if (onPaused != null)
                Dispatcher.UIThread.Invoke(() => { onPaused(); }, DispatcherPriority.MaxValue);
        }

        public void Stop()
        {
            //Dispose from the media player
            if (currentPlayingMedia != null)
                currentPlayingMedia.Dispose();

            //Release the variable
            currentPlayingMedia = null;

            //Do the callback on main thread
            if (onStopped != null)
                Dispatcher.UIThread.Invoke(() => { onStopped(); }, DispatcherPriority.MaxValue);
        }

        public void SetVolume(int newVolume)
        {
            //Save the volume
            volumePercent = newVolume;

            //Apply to current media
            if (currentPlayingMedia != null)
                currentPlayingMedia.Volume = volumePercent;
        }

        public void SetEqualizationDisabled()
        {
            //If already have a equalizer, dispose it
            if (activeEqualizer != null)
                activeEqualizer.Dispose();
            activeEqualizer = null;
        }

        public void SetEqualization(int amp, int hz31, int hz62, int hz125, int hz250, int hz500, int khz1, int khz2, int khz4, int khz8, int khz16)
        {
            //If already have a equalizer, dispose it
            if (activeEqualizer != null)
                activeEqualizer.Dispose();
            activeEqualizer = null;

            //Set the equalizer
            activeEqualizer = new Equalizer();
            activeEqualizer.SetPreamp(amp);
            activeEqualizer.SetAmp(hz31, 0);
            activeEqualizer.SetAmp(hz62, 1);
            activeEqualizer.SetAmp(hz125, 2);
            activeEqualizer.SetAmp(hz250, 3);
            activeEqualizer.SetAmp(hz500, 4);
            activeEqualizer.SetAmp(khz1, 5);
            activeEqualizer.SetAmp(khz2, 6);
            activeEqualizer.SetAmp(khz4, 7);
            activeEqualizer.SetAmp(khz8, 8);
            activeEqualizer.SetAmp(khz16, 9);
        }

        //Auxiliar methods

        private void LoadMusicMetadata(string[] currentMusicAndNext4MusicsFilePath)
        {
            //Start a new thread to load metadata of all musics of array
            new Thread(() =>
            {
                //Get the array of musics
                string[] musicsArray = currentMusicAndNext4MusicsFilePath;

                //Create a loop to load data of all musics
                for (int i = 0; i < musicsArray.Length; i++)
                {
                    //Wait time
                    Thread.Sleep(50);

                    //Prepare the data to return
                    MusicMetadata musicMetadata = MusicMetadata.Current;
                    Bitmap coverBitmap = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
                    string name = "Unknown";
                    string author = "Unknown";
                    string extension = "UKN";

                    //Detect the music order
                    if (i == 0)
                        musicMetadata = MusicMetadata.Current;
                    if (i == 1)
                        musicMetadata = MusicMetadata.Next2;
                    if (i == 2)
                        musicMetadata = MusicMetadata.Next3;
                    if (i == 3)
                        musicMetadata = MusicMetadata.Next4;
                    if (i == 4)
                        musicMetadata = MusicMetadata.Next5;

                    //Try to load the text metadata
                    try
                    {
                        //Load the music file
                        TagLib.File file = TagLib.File.Create(musicsArray[i]);
                        name = file.Tag.Title;
                        author = file.Tag.FirstPerformer;
                        extension = Path.GetExtension(musicsArray[i]).ToUpper().Replace(".", "");
                    }
                    catch (Exception e) { }

                    //Fix the name and author, if necessary
                    if (string.IsNullOrEmpty(name) == true)
                        name = "Unknown";
                    if (string.IsNullOrEmpty(author) == true)
                        author = "Unknown";

                    //If is a MP3 file, continue to extract the cover...
                    if (Path.GetExtension(musicsArray[i]).ToLower() == ".mp3")
                    {
                        //Try to load the music file
                        TagLib.File file = new TagLib.Mpeg.AudioFile(musicsArray[i]);
                        //If have cover image, continues...
                        if (file.Tag.Pictures.Length > 0)
                        {
                            //Load the bitmap cover of music file, if have
                            TagLib.IPicture picture = file.Tag.Pictures[0];
                            MemoryStream memoryStream = new MemoryStream(picture.Data.Data);
                            if (memoryStream != null && memoryStream.Length > 4096)
                            {
                                coverBitmap = new Bitmap(memoryStream);
                                memoryStream.Flush();
                                memoryStream.Close();
                            }
                        }
                    }

                    //Send callback on UI thread
                    if (onLoadMusicMetadata != null)
                        Dispatcher.UIThread.Invoke(() => { onLoadMusicMetadata(musicMetadata, coverBitmap, name, author, extension); }, DispatcherPriority.MaxValue);
                }

                //...
            }).Start();
        }
    }
}