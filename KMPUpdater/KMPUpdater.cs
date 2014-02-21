using System;
using System.Net;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace KMPUpdater
{
	class KMPUpdater
	{
		private const string DEFAULT_UPDATER_ADDRESS = "http://godarklight.kerbalcentral.com:82/kmp/updater/";
		private static string mode;
		private static string updateType;
		private static string throwError;
		private static string updateAddress = DEFAULT_UPDATER_ADDRESS;
		private static string[] versionIndex;
		private static string[] fileIndex;
		#region Main Logic
		public static int Main (string[] args)
		{
			if (!SetMode ()) {
				Console.WriteLine ("Badly formatted version.");
				Console.WriteLine ("File name should be KMPUpdater-(version).exe");
				Console.WriteLine ("Press any key to exit.");
				Console.ReadKey ();
				return 1;
			} else {
				Console.WriteLine ("Using the " + mode + " version");
			}
			if (File.Exists ("KMPServer.exe") && !File.Exists ("KSP.exe")) {
				Console.WriteLine ("Updating server");
				updateType = "server";
			}
			if (File.Exists ("KSP.exe")) {
				Console.WriteLine ("Updating client");
				updateType = "client";
			}
			if (!File.Exists ("KMPServer.exe") && !File.Exists ("KSP.exe")) {
				Console.WriteLine ("Place this file next to KSP.exe or KMPServer.exe");
				Console.WriteLine ("Press any key to exit.");
				Console.ReadKey ();
				return 2;
			}
			Console.Write ("Downloading version index...");
			if (!GetVersionIndex ()) {
				Console.WriteLine (" failed!");
				Console.WriteLine (throwError);
				Console.WriteLine ("Press any key to exit.");
				Console.ReadKey ();
				return 3;
			} else {
				Console.WriteLine (" done!");
			}
			Console.Write ("Checking version...");
			if (!CheckVersion ()) {
				Console.WriteLine (" failed!");
				Console.WriteLine ("Version does not exist, Current versions are:");
				foreach (string version in versionIndex) {
					Console.WriteLine (version);
				}
				Console.WriteLine ("\nPress any key to exit.");
				Console.ReadKey ();
				return 4;
			} else {
				Console.WriteLine (" ok!");
			}
			Console.Write ("Downloading " + mode + " index...");
			if (!GetFileIndex ()) {
				Console.WriteLine (" failed!");
				Console.WriteLine (throwError);
				Console.WriteLine ("\nPress any key to exit.");
				Console.ReadKey ();
				return 5;
			} else {
				Console.WriteLine (" ok!");
			}
			Console.WriteLine ("Parsing " + mode + " index...");
			if (!parseFileIndex ()) {
				Console.WriteLine (throwError);
				Console.WriteLine ("\nPress any key to exit.");
				Console.ReadKey ();
				return 6;
			}
			Console.WriteLine ("Your KMP " + updateType + " is up to date!");
			Console.WriteLine ("Press any key to exit.");
			Console.ReadKey ();
			return 0;
		}
		#endregion
		#region Setup logic
		private static bool SetMode ()
		{
			string exeName = AppDomain.CurrentDomain.FriendlyName;
			if (!exeName.Contains ("-") || !exeName.Contains (".exe")) {
				if (exeName == "KMPUpdater.exe") {
					mode = "release";
				} else {
					return false;
				}
			} else {
				mode = exeName.Remove (0, exeName.LastIndexOf ("-") + 1).Replace (".exe", "").ToLowerInvariant ();
			}
			return true;
		}

		private static bool CheckVersion ()
		{
			foreach (string version in versionIndex) {
				if (version == mode) {
					return true;
				}
			}
			return false;
		}
		#endregion
		#region Download indexes
		private static bool GetVersionIndex ()
		{ 
			try {
				using (WebClient wc = new WebClient()) {
					versionIndex = Encoding.UTF8.GetString (wc.DownloadData (updateAddress + "/index.txt")).Split (new string[] { "\n" }, StringSplitOptions.None);
				}
			} catch (Exception e) {
				throwError = e.Message;
				return false;
			}
			return true;
		}

		private static bool GetFileIndex ()
		{ 
			try {
				using (WebClient wc = new WebClient()) {
					fileIndex = Encoding.UTF8.GetString (wc.DownloadData (updateAddress + "/versions/" + mode + "/" + updateType + ".txt")).Split (new string[] { "\n" }, StringSplitOptions.None);
				}
			} catch (Exception e) {
				throwError = e.Message;
				return false;
			}
			return true;
		}
		#endregion
		#region File handling logic
		private static bool parseFileIndex ()
		{
			foreach (string fileentry in fileIndex) {
				if (fileentry.Contains ("=")) {
					string file = fileentry.Remove (fileentry.LastIndexOf ("="));
					string shaSum = fileentry.Remove (0, fileentry.LastIndexOf ("=") + 1);
					if (file.Contains ("/")) {
						CheckPathExists (file.Remove (file.LastIndexOf ("/")));
					}
					if (FileNeedsUpdating (file, shaSum)) {
						Console.Write ("Downloading " + file + " ");
						if (!UpdateFile (file, shaSum)) {
							Console.WriteLine (" error!");
							return false;
						} else {
							Console.WriteLine (" done!"); 
						}

					}
				}
			}
			return true;
		}

		private static void CheckPathExists (string directory)
		{
			//Recursively create any parent directories.
			if (!Directory.Exists (directory)) {
				CheckPathExists (Directory.GetParent (directory).ToString ());
				Directory.CreateDirectory (directory);
			}
		}

		private static bool FileNeedsUpdating (string file, string shaSum)
		{
			if (!File.Exists (file)) {
				return true;
			}
			using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read)) {
				using (SHA256Managed sha = new SHA256Managed()) {
					string fileSha = BitConverter.ToString (sha.ComputeHash (fs)).Replace ("-", "").ToLowerInvariant ();
					if (shaSum != fileSha) {
						return true;
					}
				}
			}
			return false;
		}

		private static bool UpdateFile (string file, string shaSum)
		{

			if (File.Exists (file)) {
				File.Delete (file);
			}
			using (FileStream fs = new FileStream(file, FileMode.Create, FileAccess.Write)) {
				using (WebClient wc = new WebClient()) {
					try {
						byte[] fileBytes = wc.DownloadData (new Uri (updateAddress + "versions/" + mode + "/objects/" + shaSum));
						fs.Write (fileBytes, 0, fileBytes.Length);
					} catch (Exception e) {
						throwError = e.Message;
						return false;
					}
				}
			}
			return true;
		}
		#endregion
	}
}
