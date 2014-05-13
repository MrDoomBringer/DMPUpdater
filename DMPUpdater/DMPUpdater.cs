using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DMPUpdater
{
    class DMPUpdater
    {
        private const string DefaultUpdaterAddress = "http://chrisand.no-ip.info/dmp/updater/";
        private static string _mode;
        private static string _updateType;
        private static string _throwError;
        private const string UpdateAddress = DefaultUpdaterAddress;
        private static string _applicationDirectory;
        private static string[] _versionIndex;
        private static string[] _fileIndex;
        private static bool _batchMode;
        #region Main Logic
        public static int Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "-b" || args[0] == "--batch"))
            {
                Console.WriteLine("Running in batch mode");
                _batchMode = true;
            }
            _applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!SetMode())
            {
                Console.Error.WriteLine("Badly formatted version.");
                Console.Error.WriteLine("File name should be DMPUpdater-(version).exe");
                AskToExitIfInteractive();
                return 1;
            }
            Console.WriteLine("Using the " + _mode + " version");
            _updateType = "";
            if (File.Exists(Path.Combine(_applicationDirectory, "DMPServer.exe")))
            {
                _updateType = "server";
            }
            if (File.Exists(Path.Combine(_applicationDirectory, "KSP.exe")) || Directory.Exists(Path.Combine(_applicationDirectory, "KSP.app")) || File.Exists(Path.Combine(_applicationDirectory, "KSP.x86")))
            {
                _updateType = "client";
            }
            if (_updateType == "")
            {
                Console.Error.WriteLine("Cannot find client or server in: " + _applicationDirectory);
                Console.Error.WriteLine("Place DMPUpdater next to KSP.exe or DMPServer.exe");
                AskToExitIfInteractive();
                return 2;
            }
            Console.WriteLine("Updating " + _updateType);
            Console.Write("Downloading version index...");
            if (!GetVersionIndex())
            {
                Console.Error.WriteLine(" failed!");
                Console.Error.WriteLine(_throwError);
                AskToExitIfInteractive();
                return 3;
            }
            Console.WriteLine(" done!");
            Console.Write("Checking version...");
            if (!CheckVersion())
            {
                Console.Error.WriteLine(" failed!");
                Console.Error.WriteLine("Version does not exist, Current versions are:");
                foreach (var version in _versionIndex)
                {
                    Console.WriteLine(version);
                }
                AskToExitIfInteractive();
                return 4;
            }
            Console.WriteLine(" ok!");
            Console.Write("Downloading " + _mode + " index...");
            if (!GetFileIndex())
            {
                Console.Error.WriteLine(" failed!");
                Console.Error.WriteLine(_throwError);
                AskToExitIfInteractive();
                return 5;
            }
            Console.WriteLine(" ok!");
            Console.WriteLine("Parsing " + _mode + " index...");
            if (!ParseFileIndex())
            {
                Console.Error.WriteLine(" failed!");
                Console.Error.WriteLine(_throwError);
                AskToExitIfInteractive();
                return 6;
            }
            Console.WriteLine("Your DMP " + _updateType + " is up to date!");
            AskToExitIfInteractive();
            return 0;
        }
        //Asks to exit if running interactively
        private static void AskToExitIfInteractive()
        {
            if (!_batchMode)
            {
                Console.WriteLine("\nPress any key to exit");
                Console.ReadKey();
            }
        }
        #endregion
        #region Setup logic
        private static bool SetMode()
        {
            var exeName = AppDomain.CurrentDomain.FriendlyName;
            if (!exeName.Contains("-") || !exeName.Contains(".exe"))
            {
                if (exeName == "DMPUpdater.exe")
                {
                    _mode = "release";
                }
                else
                {
                    return false;
                }
            }
            else
            {
                _mode = exeName.Remove(0, exeName.LastIndexOf("-", StringComparison.Ordinal) + 1).Replace(".exe", "").ToLowerInvariant();
            }
            return true;
        }

        private static bool CheckVersion()
        {
            return _versionIndex.Any(version => version == _mode);
        }

        #endregion
        #region Download indexes
        private static bool GetVersionIndex()
        {
            try
            {
                using (var wc = new WebClient())
                {
                    _versionIndex = Encoding.UTF8.GetString(wc.DownloadData(UpdateAddress + "/index.txt")).Split(new[] { "\n" }, StringSplitOptions.None);
                }
            }
            catch (Exception e)
            {
                _throwError = e.Message;
                return false;
            }
            return true;
        }

        private static bool GetFileIndex()
        {
            try
            {
                using (var wc = new WebClient())
                {
                    _fileIndex = Encoding.UTF8.GetString(wc.DownloadData(UpdateAddress + "/versions/" + _mode + "/" + _updateType + ".txt")).Split(new[] { "\n" }, StringSplitOptions.None);
                }
            }
            catch (Exception e)
            {
                _throwError = e.Message;
                return false;
            }
            return true;
        }
        #endregion
        #region File handling logic
        private static bool ParseFileIndex()
        {
            foreach (var fileentry in _fileIndex)
            {
                if (!fileentry.Contains("=")) continue;
                var file = fileentry.Remove(fileentry.LastIndexOf("=", StringComparison.Ordinal));
                var shaSum = fileentry.Remove(0, fileentry.LastIndexOf("=", StringComparison.Ordinal) + 1);
                if (file.Contains("/"))
                {
                    CheckPathExists(file.Remove(file.LastIndexOf("/", StringComparison.Ordinal)));
                }
                if (!FileNeedsUpdating(file, shaSum)) continue;
                Console.Write("Downloading " + file + " ");
                if (!UpdateFile(file, shaSum))
                {
                    Console.WriteLine(" error!");
                    return false;
                }
                Console.WriteLine(" done!");
            }
            return true;
        }

        private static void CheckPathExists(string directory)
        {
            //Recursively create any parent directories.
            if (Directory.Exists(Path.Combine(_applicationDirectory, directory))) return;
            CheckPathExists(Directory.GetParent(directory).ToString());
            Directory.CreateDirectory(Path.Combine(_applicationDirectory, directory));
        }

        private static bool FileNeedsUpdating(string file, string shaSum)
        {
            if (!File.Exists(Path.Combine(_applicationDirectory, file)))
            {
                return true;
            }
            using (var fs = new FileStream(Path.Combine(_applicationDirectory, file), FileMode.Open, FileAccess.Read))
            {
                using (var sha = new SHA256Managed())
                {
                    var fileSha = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                    if (shaSum != fileSha)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool UpdateFile(string file, string shaSum)
        {

            if (File.Exists(Path.Combine(_applicationDirectory, file)))
            {
                File.Delete(Path.Combine(_applicationDirectory, file));
            }
            using (var fs = new FileStream(Path.Combine(_applicationDirectory, file), FileMode.Create, FileAccess.Write))
            {
                using (var wc = new WebClient())
                {
                    try
                    {
                        var fileBytes = wc.DownloadData(new Uri(UpdateAddress + "versions/" + _mode + "/objects/" + shaSum));
                        fs.Write(fileBytes, 0, fileBytes.Length);
                    }
                    catch (Exception e)
                    {
                        _throwError = e.Message;
                        return false;
                    }
                }
            }
            return true;
        }
        #endregion
    }
}
