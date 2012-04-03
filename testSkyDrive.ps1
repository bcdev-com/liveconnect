csc -t:library .\SkyDrive.cs .\LiveConnect.cs .\Json.cs /win32icon:SkyDrive.ico
if ($?) {
	powershell -NoExit {
		Add-Type -Path SkyDrive.dll
		$sd = New-Object SkyDrive "ExampleConfig.json"
	}
}
