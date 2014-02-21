THIS PROGRAM MUST RUN FROM YOUR KSP OR KMP SERVER DIRECTORY.  
  
This is a command line program that will update the KMP client or server for you.  
  
This program will automatically detect if it's in the server or client directory and update the files accordingly.  
  
This program uses sha256sum indexes, so it will only download files that have changed.  
  
If it doesn't find a "-version" part in the filename, it will default to using the release version.  
  
To switch it to the version, rename the executable to KMPUpdater-version.exe,  
where version is one of the versions listed in http://godarklight.kerbalcentral.com:82/kmp/updater/index.txt  
