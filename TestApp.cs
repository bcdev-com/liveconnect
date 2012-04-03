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

