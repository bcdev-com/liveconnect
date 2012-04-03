# Motivation
The current beta of the LiveConnect V5 SDK provides a very thin managed layer over the over the REST API, and the provided library is only usable in Metro-style applications.   While this will be important eventually, a retail release of Windows 8 is still a long way off.   This project attempts to provide a higher level of abstraction in a managed API for the Windows Live services exposed via Live Connect.

# Limitations
The first big restriction is that this library is currently exclusively synchronous.   Once the C# 5 compiler with async/await support comes closer to release this will change.   For now, the viable use-cases involve command-line tools or dedicated background threads.

This is a pre-release library written on top of a pre-release API.  It works for me, but it might eat all your sensitive files tomorrow.

# Architecture
There are two layers to the API which may be used independently:
* The LiveConnect class handles authentication/consent and provides methods to put and get strings, byte arrays and automatically JSON-serialized classes.   It also provides static helper methods for configuration management and Live Connect application creation.
* Service-specific classes, of which SkyDrive is the only one supported in the initial release.  SkyDrive provides an API that mimics the standard File, DirectoryInfo and FileInfo classes from System.IO.

The code may be compiled three ways – WPF, WinForms, or no UI libraries at all.   The browser dialog used for authentication adapts to either UI library.   When compiled without any UI libraries, an OAuth refresh token may be provided or the access token may be provided by an external tool.

To facilitate the UI-free model, the LiveConnect class includes an entry point and can be compiled as a standalone executable bound to either WPF or WinForms.   This resulting tool will create config files with the necessary refresh token or produce short-term access tokens.

# Configuration
You’ll notice there are no project files.   It will be easiest to explore from a PowerShell console, but the source is meant to be built into other projects.
The classes work with JSON formatted configuration strings.   Before you can try this code out you will need to create an application at http://manage.dev.live.com and copy your client_id and client_secret into a local config file.  See ExampleConfig.json for a starting point.

# Example
```C#
using System;
class App {
    [STAThread]
    static int Main(string[] args) {
        if (args.Length != 1) {
            Console.Error.WriteLine("usage: TestApp <json-config-file>");
            return 1;
        }
        try {
            using (var sd = new SkyDrive(args[0])) {
                DisplayFolder(sd.Root);
                var testFolder = sd.Root.CreateSubfolder("LiveConnectTesting");
                testFolder.CreateFile("test.txt", "Hello, World!");
                sd.Root.Refresh();
                var fileContents = sd.GetFile("/LiveConnectTesting/test.txt").ReadAllText();
                Console.WriteLine(fileContents);
            }
            return 0;
        } catch (Exception e) {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
    static void DisplayFolder(SkyDriveFolder folder) {
        Console.WriteLine("{0}/ - {1} children", folder.FullName, folder.Count);
        foreach(var subfolder in folder.GetFolders()) 
            DisplayFolder(subfolder);
        foreach(var file in folder.GetFiles()) 
            Console.WriteLine("{0} - {1} bytes", file.FullName, file.Length);
    }
}
```

# Future
The original motivation for this library was to provide a PowerShell provider to enable rich, easy scripting and command line access to SkyDrive.   Look for this in a future release.  Wrappers for services other than SkyDrive are likely as well.   In particular, the Calendar is probably next.

# Contact
I can be reached via email at blake@bcdev.com or feel free to open an issue in this project.

