using System;
using System.IO;

public static class PngCloak {
	static int Main(string[] args) {
		if (args.Length < 2 || args.Length > 3) {
			Console.Error.WriteLine("usage: PngCloak <png-file> <data-file> <output-png-file>");
			Console.Error.WriteLine("   or: PngCloak <png-file> <output-data-file>");
			return 1;
		}
		try {
			if (args.Length == 3) 
				File.WriteAllBytes(args[2], Inject(File.ReadAllBytes(args[0]), File.ReadAllBytes(args[1])));
			else
				File.WriteAllBytes(args[1], Extract(File.ReadAllBytes(args[0])));
			return 0;
		} catch (Exception e) {
			Console.Error.WriteLine(e.Message);
			return 1;
		}
	}
	static readonly byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
	static readonly byte[] iend = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
	static readonly byte[] tag = { 0x64, 0x61, 0x54, 0x61 };
	static void VerifyPng(byte[] png) {
		for(var i = 0; i < sig.Length; ++i) 
			if (sig[i] != png[i]) throw new ArgumentException("Missing PNG signature.", "png");
		for(int i = 0, offset = png.Length - iend.Length; i < iend.Length; ++i) 
			if (iend[i] != png[offset + i]) throw new ArgumentException("Does not end with IEND chunk.", "png");
	}
	public static byte[] Inject(byte[] png, byte[] data) {
		VerifyPng(png);
		var size = new byte[] { (byte)((data.Length >> 24) & 0xff), (byte)((data.Length >> 16) & 0xff), 
								(byte)((data.Length >> 8) & 0xff),  (byte)(data.Length & 0xff) };
		var crc = new Crc32().ComputeHash(data);
		var result = new byte[png.Length + size.Length + tag.Length + data.Length + crc.Length];
		var index = 0;
		png.CopyTo(result, index); index += png.Length - iend.Length;
		size.CopyTo(result, index); index += size.Length;
		tag.CopyTo(result, index); index += tag.Length;
		data.CopyTo(result, index); index += data.Length;
		crc.CopyTo(result, index); index += crc.Length;
		iend.CopyTo(result, index);
		return result;
	}
	public static byte[] Extract(byte[] png) {
		VerifyPng(png);
		Func<byte[],int,int> getFour = (b,j) => b[j] << 24 | b[j + 1] << 16 | b[j + 2] << 8 | b[j + 3];
		var dataTag = getFour(tag, 0);
		var i = sig.Length;
		while (i < png.Length) {
 			var length = getFour(png, i);
			i += 4;
			if (getFour(png, i) == dataTag) {
				var result = new byte[length];
				i += 4;
				Array.Copy(png, i, result, 0, length);
				var crc = new Crc32().ComputeHash(result);
				i += length;
				if (getFour(crc, 0) != getFour(png, i))
					throw new ArgumentException("PNG's daTa chunk fails CRC.", "png");
				return result;
			}
			i += length + 8;
		}
		throw new ArgumentException("PNG does not contain a daTa chunk.", "png");
	}
}
