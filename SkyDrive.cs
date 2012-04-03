// © 2011, Blake Coverett - distribute freely under the terms of the Ms-PL
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

[DataContract]
class Item: Json<Item> {
	[DataMember] public string id { get; set; }
	[DataMember] public string type { get; set; }
	[DataMember] public string name { get; set; }
	[DataMember] public string description { get; set; }
	[DataMember] public string parent_id { get; set; }
	[DataMember] public int count { get; set; }
	[DataMember] public int size { get; set; }
	[DataMember] public string created_time { get; set; }
	[DataMember] public string updated_time { get; set; }
}
[DataContract]
class ItemUpdate: Json<ItemUpdate> {
	[DataMember] public string name { get; set; }
	[DataMember] public string description { get; set; }
}
[DataContract]
class ItemList: Json<ItemList> {
	[DataMember] public Item[] data { get; set; }
}

public abstract class SkyDriveItem {
	protected LiveConnect lc;
	SkyDriveFolder parent;
	internal Item item;

	internal SkyDriveItem(LiveConnect lc, SkyDriveFolder parent, Item item) { 
		this.lc = lc; 
		this.parent = parent; 
		this.item = item; 
	}
	internal static SkyDriveItem Create(LiveConnect lc, SkyDriveFolder parent, Item item) {
		if (item.type == "folder" || item.type == "album")
			return new SkyDriveFolder(lc, parent, item);
		else
			return new SkyDriveFile(lc, parent, item);
	}
	public override string ToString() { return item.name; }
	public string Id { get { return item.id; } }
	void UpdateMetadata() {
		lc.PutJson<ItemUpdate, ItemUpdate>(
			item.id, new ItemUpdate { name = item.name, description = item.description });
	}
	public string Name { get { return item.name; } set { item.name = value; UpdateMetadata(); } }
	public string Description { get { return item.description; } set { item.description = value; UpdateMetadata(); } }
	public SkyDriveFolder Parent { get { return parent; } }
	public string FullName { get { return parent != null ? parent.FullName + "/" + item.name : ""; } }
	public string Extension { get { return Path.GetExtension(item.name); } }
	DateTime ParseTime(string time) { return time != null ? DateTime.Parse(time) : DateTime.MinValue; }
	public DateTime CreationTime { get { return ParseTime(item.created_time); } }
	public DateTime LastWriteTime { get { return ParseTime(item.updated_time); } }
	public virtual void Refresh() { item = lc.GetJson<Item>(item.id); }
	public void MoveTo(SkyDriveFolder destination) { /*TODO*/ }
	public void Delete() { /*TODO*/ }
}
public class SkyDriveFolder: SkyDriveItem {
	SkyDriveItem[] children = null;
	internal SkyDriveFolder(LiveConnect lc, SkyDriveFolder parent, Item item) :base(lc, parent, item) {}
	public int Count { get { return item.count; } }
	public override void Refresh() {
		base.Refresh();
		children = null;
	} 
	public SkyDriveItem[] GetItems() {
		if (children == null)
			children = lc.GetJson<ItemList>(item.id + "/files")
				.data.Select(it => SkyDriveItem.Create(lc, this, it)).ToArray();
		return children;
	}
	public SkyDriveFolder[] GetFolders() { return GetItems().OfType<SkyDriveFolder>().ToArray(); }
	public SkyDriveFile[] GetFiles() { return GetItems().OfType<SkyDriveFile>().ToArray(); }
	public SkyDriveFolder CreateSubfolder(string name, string description = null) {
		return new SkyDriveFolder(lc, this, 
			lc.PutJson<ItemUpdate, Item>(item.id, 
				new ItemUpdate { name = name, description = description }, "POST"));
	}
	public SkyDriveFile CreateFile(string name, string fileContents) {
		var file = new SkyDriveFile(lc, this, lc.PutString<Item>(item.id + "/files/" + name, fileContents));
		file.Refresh();
		return file;
	}
	public SkyDriveFile CreateFile(string name, byte[] fileContents) {
		var file = new SkyDriveFile(lc, this, lc.PutBytes<Item>(item.id + "/files/" + name, fileContents));
		file.Refresh();
		return file;
	}
	public SkyDriveFile UploadFile(string fileName) {
		return CreateFile(Path.GetFileName(fileName), File.ReadAllBytes(fileName));
	}
}
public class SkyDriveFile: SkyDriveItem {
	internal SkyDriveFile(LiveConnect lc, SkyDriveFolder parent, Item item) :base(lc, parent, item) {}
	public int Length { get { return item.size; } }
	public string ReadAllText() { return lc.GetString(this.Id + "/content"); }
	public string[] ReadAllLines() { return ReadAllText().Split(new string[] {"\r\n"}, StringSplitOptions.None); }
	public byte[] ReadAllBytes() { return lc.GetBytes(this.Id + "/content"); }
	public void WriteAllText(string fileContent) { lc.PutString<Item>(this.Id + "/content", fileContent); }
	public void WriteAllLines(string[] fileContent) { WriteAllText(string.Join("\r\n", fileContent)); }
	public void WriteAllBytes(byte[] fileContent) { lc.PutBytes<Item>(this.Id + "/content", fileContent); }
	public void UploadFile(string fileName) { WriteAllBytes(File.ReadAllBytes(fileName)); }
	public void DownloadFile(string fileName) { File.WriteAllBytes(fileName, ReadAllBytes()); }
	public void CopyTo(SkyDriveFolder destination) { /*TODO*/ }
}

public class SkyDrive: IDisposable {
	const string scopes = "wl.offline_access wl.skydrive wl.skydrive_update wl.contacts_skydrive";
	LiveConnect lc;
	SkyDriveFolder root;
	public SkyDriveFolder Root { get { return root; } }

	public static string CreateConfig(string clientId, string clientSecret) {
		return LiveConnect.CreateConfig(clientId, clientSecret, scopes);
	}
	public static void CreateConfigFile(string clientId, string clientSecret, string fileName) {
		File.WriteAllText(CreateConfig(clientId, clientSecret), fileName);
	}

	public SkyDrive(string configFile = "SkyDrive.json"): this(new LiveConnect(configFile)) {}
	public SkyDrive(Func<string> readConfig, Action<string> writeConfig):
   		this(new LiveConnect(readConfig, writeConfig)) {}
	public SkyDrive(LiveConnect lc) {
		this.lc = lc;
		root = new SkyDriveFolder(lc, null, lc.GetJson<Item>("me/skydrive")); 
	}
	public SkyDriveItem GetItem(string path) {
		var rootPath = Path.GetPathRoot(path);
		if (rootPath == null || rootPath.Length != 1 || rootPath[0] != Path.DirectorySeparatorChar) 
			throw new IOException("Path must start at root.");
		if (path == rootPath) return root;
		var segments = new List<string>();
		for(var p = path; p != rootPath; p = Path.GetDirectoryName(p))
			segments.Insert(0, Path.GetFileName(p));
		var parent = root;
		for(var i = 0; i < segments.Count - 1; ++i) {
			var child = parent.GetFolders().SingleOrDefault(f => f.Name == segments[i]);
			if (child == null) 
				throw new DirectoryNotFoundException(
					string.Format("Folder \"{0}\" not found.", parent.FullName + "/" + segments[i]));
			parent = child;
		}
		var item = parent.GetItems().SingleOrDefault(f => f.Name == segments.Last());
		if (item == null) 
			throw new FileNotFoundException(
				string.Format("File \"{0}\" not found.", parent.FullName + "/" + segments.Last()));
		return item;
	}
	public SkyDriveFolder GetFolder(string path) {
		var f = GetItem(path) as SkyDriveFolder;
		if (f == null) throw new DirectoryNotFoundException(string.Format("\"{0}\" is a file, not a folder.", path));
		return f;
	}
	public SkyDriveFile GetFile(string path) {
		var f = GetItem(path) as SkyDriveFile;
		if (f == null) throw new FileNotFoundException(string.Format("\"{0}\" is a folder, not a file.", path));
		return f;
	}
	public void Dispose() { 
		if (lc != null) {
			lc.Dispose(); 
			lc = null;
		}
	}
}
