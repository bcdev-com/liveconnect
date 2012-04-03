// © 2011, Blake Coverett - distribute freely under the terms of the Ms-PL
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

[DataContract]
public class Json<T> {
	static DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
	public static T FromJson(string json) {
		using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(json))) 
			return (T)serializer.ReadObject(ms);
	}
	public virtual string ToJson() {
		using (var ms = new MemoryStream()) {
			serializer.WriteObject(ms, this);
			ms.Position = 0;
			using (var sr = new StreamReader(ms))
				return sr.ReadToEnd();
		}
	}
	public override string ToString() { return string.Format("{0}:{1}", this.GetType(), ToJson()); }
}

