# The SkyDrive API only allows files with extensions from a specific whitelist to be uploaded.
# This code lets you stuff an arbitrary file inside an arbitrary PNG and extract it later.
# If you combine the preceding facts and someone gets upset, don't blame me.
csc PngCloak.cs Crc32.cs 
if ($?) { 
	.\PngCloak.exe PngCloak.png PngCloak.exe test.png 
	if ($?) {
		.\PngCloak.exe test.png extracted.exe
	}
};
