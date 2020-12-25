using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using CSCore;
using CSCore.SoundIn;
using CSCore.Streams;

namespace iHateSilence
{
    class Program
    {
        static bool isMusicPlaying = false;
        static bool isLogFileExists = true;
        static bool noSoundIsRunning = false;
        static bool newDataIsRunning = false;
        static string myConfig = "iHateSilence.cfg";

        static int appMagic = 20201225;
        static string appName = "winamp";
        static string appPath = @"C:\Program Files (x86)\Winamp\winamp.exe";
        static string appARGV = @"http://webradio.mcm.fm:10000/hit128";
        static int delayBeforeRestartProgram = 30;
        static void Main(string[] args)
        {
            myConfig = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".cfg";
            if (!ReadConfig() & args.Length == 0)
            {
                WriteLog("ERROR! (Main): Can't read a configuration file. Please, create a new one.");
                Array.Resize(ref args, 1);
                args[0] = "/config";
            }

            string myFullName = System.Reflection.Assembly.GetExecutingAssembly().GetName().FullName;
            WriteLog("(Main): Program started. (" + myFullName + ")");
            string sArgs = string.Join(" ", args);
            WriteLog("(Main): Arguments: " + sArgs);
            
            if (sArgs.Contains(@"/?"))
            {
                Console.WriteLine("");

                Console.WriteLine("/?       -   Print this message and exit.");
                Console.WriteLine("/config  -   Configure program and exit.");

                Console.WriteLine("\n Press any key to exit...");
                Console.ReadKey();
                Exit(0);
            }

            if (sArgs.Contains(@"/config"))
            {
                Console.WriteLine("");
                
                Console.WriteLine($"Please, enter a full path of media player application (default is: {appPath}): ");
                string a = Console.ReadLine();
                if (a.Length == 0) a = appPath;
                if (!IsValidFullPath(a))
                {
                    Console.WriteLine("ERROR! Wrong application path.");
                    a = appPath;
                }
                appPath = a;
                appName = string.Join(".", Pop(System.IO.Path.GetFileName(appPath).Split('.')));

                Console.WriteLine($"Please, enter arguments for application if needed (default is: {appARGV}): ");
                a = Console.ReadLine();
                if(a!=null & a.Length > 0)
                {
                    appARGV = a;
                }

                Console.WriteLine($"Please, enter a value of delay (in seconds >=5 ) before application will be restarted (default is: {delayBeforeRestartProgram}): ");
                a = Console.ReadLine();
                bool e = false;
                if (a.Length == 0) a = delayBeforeRestartProgram.ToString();
                int b = StrToInt(a, ref e);
                if (b >= 5 & !e)
                {
                    delayBeforeRestartProgram = b;
                }
                else
                {
                    Console.WriteLine("ERROR! Wrong delay value! Should be >=5.");
                }

                while (true)
                {
                    Console.WriteLine("Is configuration below correct?");
                    Console.WriteLine($"\nappPath: {appPath}\nappName: {appName}\nappARGV: {appARGV}\ndelayBeforeRestartProgram: {delayBeforeRestartProgram}");
                    Console.Write("(Y/N): ");
                    a = Console.ReadLine();
                    if (a == "Y" || a == "y")
                    {
                        WriteConfig();
                        break;
                    }
                    else if (a == "N" || a == "n")
                    {
                        Console.WriteLine("Please, rerun this application with /config argument to try again.");
                        break;
                    }
                }
                

                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                Exit(0);
            }
            
            var soundIn = new WasapiLoopbackCapture();
            WriteLog ("(Main): Working with: " + soundIn.Device.FriendlyName);
            try
            {
                soundIn.Initialize();
            }
            catch
            {
                WriteLog("ERROR! (Main): Error while initializing device(39). Exiting.");
                Exit(1);
            }
            var soundInSource = new SoundInSource(soundIn);
            try
            {
                ISampleSource source = soundInSource.ToSampleSource();
                soundInSource.DataAvailable += (s, aEvent) => NewData(source);
            }
            catch
            {
                WriteLog("ERROR! (Main): Error while initializing device(50). Exiting.");
                Exit(1);
            }
            
            WriteLog("(Main): Trying to start sound capturing...");
            try
            {
                soundIn.Start();
                Thread.Sleep(2000);
                if (!newDataIsRunning & !noSoundIsRunning)
                {
                    Thread noSound = new Thread(NoSound);
                    noSound.IsBackground = true;
                    noSound.Start();
                }
            }
            catch
            {
                WriteLog("ERROR! (Main): Error while sound capturing. Exiting.");
                Exit(1);
            }

            WriteLog("(Main): Started.");
        }

        static void NewData(ISampleSource source)
        {
            newDataIsRunning = true;
            if(source == null)
            {
                WriteLog("ERROR! (newData): Something went wrong (source is null). Exiting.");
                Exit(1);
            }
            int read;
            bool res = false;
            float[] buffer = new float[source.WaveFormat.BytesPerSecond / 1000];
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0);
            int i = 0;
            float sum = 0;
            foreach (float item in buffer)
            {
                sum += item;
                i++;
            }
            sum = Math.Abs(sum);
            if(sum.GetHashCode() > 0)
            {
                res = true;
            }

            bool ret = IsProgramRunning(appName);
            if (isMusicPlaying & !res & !noSoundIsRunning)
            {
                Thread noSound = new Thread(NoSound);
                noSound.IsBackground = true;
                noSound.Start();
                
            }
            else if (!isMusicPlaying & !res & !noSoundIsRunning)
            {
                Thread noSound = new Thread(NoSound);
                noSound.IsBackground = true;
                noSound.Start();
            }
            else if (!isMusicPlaying & res)
            {
                WriteLog("(newData): We have sound! Player is running: " + ret.ToString());
            }

            isMusicPlaying = res;
            
            Thread.Sleep(100);
        }

        static bool IsProgramRunning(string name, bool killIt = false)
        {
            bool res = false;
            Process[] processes;
            
            if(name.Length <= 0 || name == null)
            {
                return false;
            }
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch
            {
                return false;
            }
            if(processes.Length <= 0)
            {
                return false;
            }
            res = true;
            if (killIt)
            {
                foreach (var item in processes)
                {
                    try
                    {
                        item.Kill();
                    }
                    catch
                    {
                        return res;
                    }
                }
            }

            return res;
        }

        static void WriteLog(string logString, bool writeToFile = true, bool writeToConsole = true)
        {
            string output = DateTime.Now.ToString() + ": " + logString;
            if (writeToFile & isLogFileExists)
            {
                string path = "log.txt";
                if (!File.Exists(path))
                {
                    WriteLog("(writeLog): Log file does not exist. Creating...", false);
                    try
                    {
                        using (StreamWriter sw = File.CreateText(path))
                        {
                            sw.WriteLine(DateTime.Now.ToString() + ": (writeLog) Log file has been created!");
                            sw.WriteLine(output);
                        }
                    }
                    catch
                    {
                        isLogFileExists = false;
                        WriteLog("ERROR! (writeLog): Can't create a log file. Please check the directory permissions (current directory).", false);
                    }
                    
                }
                else
                {
                    try
                    {
                        using (StreamWriter sw = File.AppendText(path))
                        {
                            sw.WriteLine(output);
                        }
                    }
                    catch
                    {
                        isLogFileExists = false;
                        WriteLog("ERROR! (writeLog): Error while writing the log file.", false);
                    }
                    
                }
            }
            if (writeToConsole)
            {
                Console.WriteLine(output);
            }
            Debug.WriteLine(output);
        }

        static void Exit(int errorCode = 0)
        {
            Environment.Exit(errorCode);
        }

        static string[] Pop(string[] array)
        {
            string[] res = { };
            if (array.Length > 1)
            {
                Array.Resize(ref array, array.Length - 1);
            }
            res = array;
            return res;
        }

        static bool IsValidFullPath(string path)
        {
            bool res = true;

            try
            {
                Regex rgx = new Regex(@"^[a-zA-Z]:\\([a-zA-Zа-яА-Я0-9_\-\.\(\)\s]*\\)*[a-zA-Zа-яА-Я0-9_\-\.\(\)\s]+\.(exe|bat|ps1)$");
                if (!rgx.IsMatch(path)) res = false;
                if (path.Length > 250) res = false;
            }
            catch
            {
                WriteLog("ERROR! (IsValidFullPath): Something went wrong while parsing a full path. Exiting.");
                Exit(1);
            }

            return res;
        }

        static int StrToInt(string str, ref bool err)
        {
            int res = 0;
            string rstr = "";
            err = false;
            try
            {
                rstr = Regex.Replace(str, @"\D", "", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(0.5));
            }
            catch
            {
                err = true;
                WriteLog("ERROR! (StrToInt): Something went wrong while removing non-digit characters from string.");
            }

            if(rstr.Length > 0)
            {
                try
                {
                    res = int.Parse(rstr);
                }
                catch
                {
                    err = true;
                    WriteLog("ERROR! (StrToInt): Something went wrong while converting string to int");
                }
            }
            else
            {
                err = true;
                WriteLog("ERROR! (StrToInt): Wrong delay value.");
            }
            return res;
        }

        static void WriteConfig()
        {
            try
            {
                using (BinaryWriter writer = new BinaryWriter(File.Open(myConfig, FileMode.Create)))
                {
                    writer.Write(appMagic);
                    writer.Write(delayBeforeRestartProgram);
                    writer.Write(appARGV);
                    writer.Write(appName);
                    writer.Write(appPath);
                    writer.Write(appMagic);
                    byte[] tmpSource = UTF8Encoding.UTF8.GetBytes
                        (
                            $"{delayBeforeRestartProgram.ToString()} {appARGV} {appName} {appPath}"
                        );
                    byte[] tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
                    foreach(byte item in tmpHash)
                    {
                        writer.Write(item);
                    }
                    WriteLog("(WriteConfig): Configuration file has been successfully written.");
                }
            }
            catch
            {
                WriteLog("ERROR! (WriteConfig): Can't write a configuration file. Please check the directory permissions (current directory).");
            }
        }

        static bool ReadConfig()
        {
            bool res = false;

            try
            {
                string fileName = myConfig;
                if (File.Exists(fileName))
                {
                    using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
                    {
                        int bmgk = reader.ReadInt32();
                        int _delayBeforeRestartProgram = reader.ReadInt32();
                        string _appARGV = reader.ReadString();
                        string _appName = reader.ReadString();
                        string _appPath = reader.ReadString();
                        int emgk = reader.ReadInt32();
                        byte[] md5h = { };
                        for (int i=0; i<16; i++)
                        {
                            Array.Resize(ref md5h, md5h.Length + 1);
                            md5h[i] = reader.ReadByte();
                        }

                        res = true;

                        if (bmgk != appMagic || emgk != appMagic) 
                        {
                            WriteLog("ERROR! (ReadConfig): Wrong magic. Configuration file is possible corrupted.");
                            res = false; 
                        }

                        byte[] tmpSource = UTF8Encoding.UTF8.GetBytes
                        (
                            $"{_delayBeforeRestartProgram.ToString()} {_appARGV} {_appName} {_appPath}"
                        );
                        byte[] tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);

                        if (!CompareByteArrays(md5h, tmpHash)) 
                        {
                            WriteLog("ERROR! (ReadConfig): Wrong checksum. Configuration file is possible corrupted.");
                            res = false; 
                        }

                        if (res)
                        {
                            delayBeforeRestartProgram = _delayBeforeRestartProgram;
                            appARGV = _appARGV;
                            appName = _appName;
                            appPath = _appPath;
                        }
                    }
                }
            }
            catch
            {
                res = false;
            }

            return res;
        }

        static bool CompareByteArrays(byte[] arr1, byte[] arr2)
        {
            bool res = false;

            if(arr1.Length == arr2.Length & arr1.Length>0)
            {
                for(int i=0; i<arr1.Length; i++)
                {
                    if(arr1[i] == arr2[i])
                    {
                        res = true;
                    }
                    else
                    {
                        res = false;
                        break;
                    }
                }
            }

            return res;
        }

        static bool StartMediaPlayer()
        {
            bool res = false;
            
            using (Process newPlayer = new Process())
            {
                if (File.Exists(appPath))
                {
                    try
                    {
                        newPlayer.StartInfo.FileName = appPath;
                        newPlayer.StartInfo.Arguments = appARGV;
                        newPlayer.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                        newPlayer.Start();
                    }
                    catch
                    {
                        WriteLog($"ERROR! (StartMediaPlayer): Something went wrong while trying to start the media player.");
                    }
                }
                else
                {
                    WriteLog($"ERROR! (StartMediaPlayer): File {appPath} does not exist.");
                }
            }

            res = IsProgramRunning(appName);

            return res;
        }

        static bool KillMediaPlayer()
        {
            bool res = false;

            bool ret = IsProgramRunning(appName);
            if (ret)
            {
                int i = 0;
                while (ret)
                {
                    ret = IsProgramRunning(appName, true);
                    Thread.Sleep(2000);
                    ret = IsProgramRunning(appName);
                    res = !ret;
                    if (i > 9)
                    {
                        res = false;
                        break;
                    }
                    i++;
                }
            }

            return res;
        }

        static void NoSound()
        {
            noSoundIsRunning = true;
            
            bool ret = IsProgramRunning(appName);
            WriteLog("(NoSound): No sound! Player is running: " + ret.ToString());
            WriteLog($"(NoSound): Waiting for {delayBeforeRestartProgram} seconds...");
            Thread.Sleep(delayBeforeRestartProgram * 1000);

            if (!isMusicPlaying)
            {
                ret = IsProgramRunning(appName);
                if (ret)
                {
                    WriteLog("(NoSound): Trying to kill media player...");
                    ret = KillMediaPlayer();
                    if (!ret)
                    {
                        WriteLog("(NoSound): Failed to kill media player. Exiting.");
                        Exit(1);
                    }
                }

                WriteLog("(NoSound): Trying to start new media player instance...");
                ret = StartMediaPlayer();
                if (!ret)
                {
                    WriteLog("(NoSound): Failed to start media player. Exiting.");
                    Exit(1);
                }
            }

            noSoundIsRunning = false;
        }
    }
}
