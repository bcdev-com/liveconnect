#csc `@forms.rsp -D:WINFORMS .\LiveConnect.cs .\Json.cs /win32icon:LiveConnect.ico
csc `@wpf.rsp -D:WPF .\LiveConnect.cs .\Json.cs /win32icon:LiveConnect.ico
if ($?) {
	.\LiveConnect ExampleConfig.json
}
