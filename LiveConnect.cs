// © 2011, Blake Coverett - distribute freely under the terms of the Ms-PL
#if WPF && WINFORMS
#error Only one of WPF or WINFORMS may be defined
#endif
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Runtime.Serialization;
using System.Web;
#if WPF
using System.Windows;
using System.Windows.Controls;
#endif
#if WINFORMS
using System.Windows.Forms;
#endif

public class LiveConnectException: Exception {
	internal LiveConnectException(string code, string message): base(string.Format("{0}: {1}", code, message)) {}
}

public class LiveConnect: IDisposable {
	const string apiTemplate = "https://beta.apis.live.net/v5.0/{0}?access_token={1}";
	const string authTemplate = 
		"https://oauth.live.com/authorize?client_id={0}&redirect_uri={1}&response_type={2}&scope={3}";
	const string tokenTemplate = 
		"https://oauth.live.com/token?client_id={0}&redirect_uri={1}&client_secret={2}&grant_type={3}{4}";
	const string grantAuth = "authorization_code&code=";
	const string refreshAuth = "refresh_token&refresh_token=";
	const string redirectUrl = "https://oauth.live.com/desktop";
	public const string AuthCodeGrant = "code";
	public const string ImplicitGrant = "token";

#if (WPF || WINFORMS) && APP
	[STAThread]
	static int Main(string[] args) {
		if (args.Length != 1) {
			Console.Error.WriteLine("usage: LiveConnect <config-file>");
			return 1;
		}
		try {
			var lc = new LiveConnect(args[0]);
			var me = lc.GetJson<User>("me");
			Console.Error.WriteLine("Authenticated as {0}/{1}.", me.id, me.name);
			Console.Error.WriteLine("Consent granted for {0}.", lc.config.scopes);
			Console.Error.WriteLine("Refresh token {0}available.", lc.CanRefresh ? "" : "un");
			Console.Error.WriteLine("Access token valid for {0}.", lc.TimeRemaining);
			Console.WriteLine(apiTemplate, "{0}", lc.auth.access_token);
			return 0;
		} catch (Exception e) {
			Console.Error.WriteLine(e);
			return 1;
		}
	}
#endif

	[DataContract]
	class Config: Json<Config> {
		[DataMember] public string client_id { get; set; }
		[DataMember] public string client_secret { get; set; }
		[DataMember] public string scopes { get; set; }
		[DataMember] public string refresh_token { get; set; }
	}
	[DataContract]
	class User: Json<User> {
		[DataMember] public string id { get; set; }
		[DataMember] public string name { get; set; }
	}
	[DataContract]
	class OAuthError: Json<OAuthError> {
		[DataMember] public string error { get; set; }
		[DataMember] public string error_description { get; set; }
	}
	[DataContract]
	class ApiError: Json<ApiError> {
		[DataContract]
		public class Error: Json<Error> {
			[DataMember] public string code { get; set; }
			[DataMember] public string message { get; set; }
		}
		[DataMember] public Error error { get; set; }
	}
	[DataContract]
	class Auth: Json<Auth> {
		[DataMember] public string access_token { get; set; }
		[DataMember] public int expires_in { get; set; }
		[DataMember] public string refresh_token { get; set; }
		[DataMember] public string scope { get; set; }
		[DataMember] public string token_type { get; set; }
	}
	[DataContract]
	class ApplicationCreate: Json<ApplicationCreate> {
		[DataMember] public string name { get; set; }
		[DataMember] public string uri { get; set; }
		[DataMember] public string terms_of_service_link { get; set; }
		[DataMember] public string privacy_link { get; set; }
	}
	[DataContract]
	class Application: Json<Application> {
		[DataMember] public string id { get; set; }
		[DataMember] public string name { get; set; }
		[DataMember] public string client_id { get; set; }
		[DataMember] public string client_secret { get; set; }
		[DataMember] public string terms_of_service_link { get; set; }
		[DataMember] public string privacy_link { get; set; }
	}

	Config config;
	Action<string> writeConfig;
	WebClient wc;
	Auth auth;
	DateTime validUntil;

	public static string CreateConfig(string clientId, string clientSecret, string scopes) {
		return new Config {client_id = clientId, client_secret = clientSecret, scopes = scopes}.ToJson();
	}
	public static void CreateConfigFile(string clientId, string clientSecret, string scopes, string fileName) {
		File.WriteAllText(CreateConfig(clientId, clientSecret, scopes), fileName);
	}
	public static string CreateConfig(string name, string tosUrl, string privacyUrl, string scopes) {
		var lc = new LiveConnect("0000000044066909", null, "wl.applications wl.applications_create");
		lc.Authenticate(ImplicitGrant);
		var app = lc.PutJson<ApplicationCreate, Application>("me/applications", 
			new ApplicationCreate {name = name, terms_of_service_link = tosUrl, privacy_link = privacyUrl}, "POST");
		return CreateConfig(app.client_id, app.client_secret, scopes);
	}
	public static void CreateConfigFile(string name, string tosUrl, string privacyUrl, string scopes, string fileName) {
		File.WriteAllText(CreateConfig(name, tosUrl, privacyUrl, scopes), fileName);
	}

	public LiveConnect(string clientId, string clientSecret, string scopes):
		this(() => CreateConfig(clientId, clientSecret, scopes), s=>{}) {}
	public LiveConnect(string configFile):
		this(() => File.ReadAllText(configFile), s => File.WriteAllText(configFile, s)) {}
	public LiveConnect(Func<string> readConfig, Action<string> writeConfig) {
		config = Config.FromJson(readConfig());
		this.writeConfig = writeConfig;
		wc = new WebClient();
		StoreAuth(null);
	}
	public void Dispose() { 
		if (wc != null) {
			wc.Dispose(); 
			wc = null;
		}
	}

	public bool IsAuthenticated { get { return DateTime.UtcNow < validUntil; } }
	public TimeSpan TimeRemaining { get { return validUntil - DateTime.UtcNow; } }
	public bool CanRefresh { get { return config.refresh_token != null; } }
	public override string ToString() {
		return string.Format("LiveConnect: Authenticated: {0}, Remaining: {1}, Can Refresh: {2}",
			this.IsAuthenticated, this.TimeRemaining, this.CanRefresh);
	}

	string GetUrl(string path) { return string.Format(apiTemplate, path, auth.access_token); }
	void HandleError(WebException we, bool auth = false) {
		if (we.Response.Headers[HttpResponseHeader.ContentType].StartsWith("application/json"))
			using (var s = new StreamReader(we.Response.GetResponseStream())) {
				var json = s.ReadToEnd();
				if (auth) {
					var e = OAuthError.FromJson(json);
					if (e.error != null) throw new LiveConnectException(e.error, e.error_description);
				} else {
					var e = ApiError.FromJson(json);
					if (e.error != null) throw new LiveConnectException(e.error.code, e.error.message);
				}
			}
	}
	public string GetString(string path) {
		CheckAuthentication();
		try { 
			return wc.DownloadString(GetUrl(path));
		} catch (WebException e) { 
			HandleError(e); 
			throw; 
		}
	}
	public T PutString<T>(string path, string data, string contentType = null, string method = "PUT") {
		CheckAuthentication();
		try {
			if (contentType != null) wc.Headers.Add(HttpRequestHeader.ContentType, contentType);
			return Json<T>.FromJson(wc.UploadString(GetUrl(path), method, data));
		} catch (WebException e) { 
			HandleError(e); 
			throw; 
		} finally {
			if (contentType != null) wc.Headers.Remove(HttpRequestHeader.ContentType);
		}
	}
	public T GetJson<T>(string path) {
		return Json<T>.FromJson(GetString(path));
	}
	public U PutJson<T, U>(string path, T arg, string method = "PUT") where T: Json<T> where U: Json<U> {
		return PutString<U>(path, arg.ToJson(), "application/json", method);
	}
	public byte[] GetBytes(string path) {
		CheckAuthentication();
		try {
			return wc.DownloadData(GetUrl(path));
		} catch (WebException e) { 
			HandleError(e); 
			throw; 
		}
	}
	public T PutBytes<T>(string path, byte[] data, string contentType = null) {
		CheckAuthentication();
		try {
			if (contentType != null) wc.Headers.Add(HttpRequestHeader.ContentType, contentType);
			var result = wc.UploadData(GetUrl(path), "PUT", data);
			var ct = wc.ResponseHeaders[HttpResponseHeader.ContentType];
			if (ct != "application/json; charset=UTF-8")
				throw new InvalidOperationException(string.Format("Expected UTF-8 json reply, received {1}", ct));
			var json = Encoding.UTF8.GetString(result);
			return Json<T>.FromJson(json);
		} catch (WebException e) {
			HandleError(e); 
			throw; 
		} finally {
			if (contentType != null) wc.Headers.Remove(HttpRequestHeader.ContentType);
		}
	}

	void CheckAuthentication() {
		if (!IsAuthenticated) {
			if (CanRefresh) 
				RefreshAuthentication();
			else
				Authenticate();
		}
	}
	void StoreAuth(string json) {
		if (json == null)
			auth = new Auth();
		else {
			auth = Auth.FromJson(json);
			config.refresh_token = auth.refresh_token;
			writeConfig(config.ToJson());
		}
		validUntil = DateTime.UtcNow.AddSeconds(auth.expires_in - 10); //HACK 10 seconds to avoid races
	}
	public void Authenticate(string grantType = AuthCodeGrant) {
#if !WPF && !WINFORMS
		throw new LiveConnectException("access_denied", "No GUI available to request authentication.");
#else
		if (config.client_secret == null) grantType = ImplicitGrant;
		var authUrl = string.Format(authTemplate, config.client_id, redirectUrl, grantType, config.scopes);
		var redirectCompleted = false;
#if WPF
		var browser = new WebBrowser { Source = new Uri(authUrl) };
		var dialog = new Window { Content = browser, Width = 600, Height = 600, 
			WindowStartupLocation = WindowStartupLocation.CenterScreen };
#else
		var browser = new WebBrowser { Url = new Uri(authUrl), Dock = DockStyle.Fill };
		var dialog = new Form { Controls = { browser }, Width = 600, Height = 600,
	   		StartPosition = FormStartPosition.CenterScreen };
#endif
		var query = "";
		browser.Navigated += (o, e) => {
#if WPF
			var u = e.Uri;
			var s = u.GetLeftPart(UriPartial.Path);
			dialog.Title = s;
#else
			var u = e.Url;
			var s = u.GetLeftPart(UriPartial.Path);
			dialog.Text = s;
#endif
			if (s == redirectUrl) {
				query = u.Query == "" ? "?" + u.Fragment.Substring(1) : u.Query;
				redirectCompleted = true;
				dialog.Close();
			}
		};
		dialog.Closing += (o, e) => { 
			if (!redirectCompleted) 
				query = "?error=access_denied&error_description=User%20cancelled%20authentication."; 
		};
		dialog.ShowDialog();

		var q = HttpUtility.ParseQueryString(query);
		if (grantType == AuthCodeGrant && q["code"] != null)
			try {
				StoreAuth(wc.DownloadString(string.Format(tokenTemplate, 
					config.client_id, redirectUrl, config.client_secret, grantAuth, q[grantType])));
			} catch (WebException we) {
				HandleError(we, true);
				throw;
			}
		else if (grantType == ImplicitGrant && q["access_token"] != null)
			StoreAuth(new Auth { access_token = q["access_token"],
				expires_in = int.Parse(q["expires_in"]),
				scope = q["scope"], token_type = q["token_type"] }.ToJson());
		else
			throw new LiveConnectException(q["error"] ?? "access_denied", q["error_description"] ?? "Unknown error.");
#endif
	}
	void RefreshAuthentication() {
		try {
			StoreAuth(wc.DownloadString(string.Format(tokenTemplate, 
				config.client_id, redirectUrl, config.client_secret, refreshAuth, config.refresh_token)));
		} catch (WebException e) {
			HandleError(e, true);
			throw;
		}
	}
}
