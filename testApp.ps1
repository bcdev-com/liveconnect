csc `@wpf.rsp -D:WPF TestApp.cs SkyDrive.cs LiveConnect.cs Json.cs /win32icon:SkyDrive.ico
if ($?) {
	.\TestApp.exe TestConfig.json
}
