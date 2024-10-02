using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Motoplay.Scripts.ObdAdapterHandler.ClassDelegates;

namespace Motoplay.Scripts
{
    /*
     * This class manage the Music Playing using LibVLCSharp
    */

    public class MusicPlayer
    {
        //Enums of script
        public enum CoverType
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
            public delegate void OnReceiveMetadata(string musicName, string musicAuthor, string musicExtension);
            public delegate void OnReceiveCover(CoverType type, Bitmap coverBitmap);
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
        private event ClassDelegates.OnReceiveMetadata onReceiveMetadata = null;
        private event ClassDelegates.OnReceiveCover onReceiveCover = null;
        private int volumePercent = 0;

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

        public void RegisterOnReceiveMetadataCallback(ClassDelegates.OnReceiveMetadata onReceiveMetadata)
        {
            //Register the callback
            this.onReceiveMetadata = onReceiveMetadata;
        }

        public void RegisterOnReceiveCoverCallback(ClassDelegates.OnReceiveCover onReceiveAlertDialog)
        {
            //Register the callback
            this.onReceiveCover = onReceiveAlertDialog;
        }

        //Control methods

        public void ChangeMusicTo(int index, ref List<string> musicFiles, bool andPlay)
        {
            //If already exists a current playing media, despose it
            if (currentPlayingMedia != null)
                Stop();

            //Create a new media to play
            currentPlayingMedia = new MediaPlayer(new Media(libVlc, musicFiles[index]));

            //Recover the covers of current music and from the next 4 musics
            int x = index;
            int loaded = 0;
            while (loaded < 5)
            {
                //If the current index is greather than the itens count in list, reset it
                if (x == musicFiles.Count)
                    x = 0;

                //Load the cover of this index
                LoadCover(loaded, x, ref musicFiles);

                //Increase the index
                x += 1;
                loaded += 1;
            }

            //Load the music metadata
            LoadMetadata(musicFiles[index]);

            //Reset the timer of time changed
            lastMediaTimeUpdate = DateTime.Now;

            //Prepare the callback of time
            currentPlayingMedia.TimeChanged += (s, e) => 
            {
                //If not passed more than 200ms since last update, cancel
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
                }).Start();
            };

            //Set the current volume
            currentPlayingMedia.Volume = volumePercent;

            //Set the audio delay
            currentPlayingMedia.SetAudioDelay((long)(new TimeSpan(TimeSpan.FromMilliseconds(250).Ticks).TotalMicroseconds));

            //If is desired to auto play, play it
            if (andPlay == true)
                Play();

            //If is not desired auto play, send pause callback to simulate it
            if (andPlay == false)
                if (onPaused != null)
                    onPaused();
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

            //Do the callback
            if (onPlayed != null)
                onPlayed();
        }

        public void Pause()
        {
            //If is already not playing, cancel
            if (currentPlayingMedia.IsPlaying == false)
                return;

            //Pause the music
            currentPlayingMedia.Pause();

            //Do the callback
            if (onPaused != null)
                onPaused();
        }

        public void Stop()
        {
            //Dispose from the media player
            currentPlayingMedia.Dispose();

            //Release the variable
            currentPlayingMedia = null;

            //Do the callback
            if (onStopped != null)
                onStopped();
        }

        public void SetVolume(int newVolume)
        {
            //Save the volume
            volumePercent = newVolume;

            //Apply to current media
            if (currentPlayingMedia != null)
                currentPlayingMedia.Volume = volumePercent;
        }

        //Auxiliar methods

        private void LoadMetadata(string musicFilePath)
        {
            //Prepare the data to return
            string name = "Unknown";
            string author = "Unknown";
            string extension = "UKN";

            //Try to load the metadata
            try
            {
                //Load the music file
                TagLib.File file = TagLib.File.Create(musicFilePath);
                name = file.Tag.Title;
                author = file.Tag.FirstPerformer;
                extension = Path.GetExtension(musicFilePath).ToUpper().Replace(".", "");
            }
            catch (Exception e) { }

            //Do the callback
            if (onReceiveMetadata != null)
                onReceiveMetadata(name, author, extension);
        }

        private void LoadCover(int alreadyLoadedCount, int index, ref List<string> musicFiles)
        {
            //Prepare the data to return
            CoverType type = CoverType.Current;
            if (alreadyLoadedCount == 0)
                type = CoverType.Current;
            if (alreadyLoadedCount == 1)
                type = CoverType.Next2;
            if (alreadyLoadedCount == 2)
                type = CoverType.Next3;
            if (alreadyLoadedCount == 3)
                type = CoverType.Next4;
            if (alreadyLoadedCount == 4)
                type = CoverType.Next5;
            Bitmap coverBitmap = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));

            //If is not a MP3 file, return a generic cover and stop here
            if (Path.GetExtension(musicFiles[index]).ToLower() != ".mp3")
            {
                //Do the callback
                if (onReceiveCover != null)
                    onReceiveCover(type, coverBitmap);

                //Stop here
                return;
            }

            //Try to load the cover from music file
            TagLib.File file = new TagLib.Mpeg.AudioFile(musicFiles[index]);
            if (file.Tag.Pictures.Length > 0)
            {
                TagLib.IPicture picture = file.Tag.Pictures[0];
                MemoryStream memoryStream = new MemoryStream(picture.Data.Data);
                if (memoryStream != null && memoryStream.Length > 4096)
                {
                    coverBitmap = new Bitmap(memoryStream);
                    memoryStream.Flush();
                    memoryStream.Close();
                }
            }

            //Do the callback
            if (onReceiveCover != null)
                onReceiveCover(type, coverBitmap);
        }
    }
}