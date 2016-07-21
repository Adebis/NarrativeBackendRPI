using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;

using Newtonsoft.Json.Linq;
using Dialogue_Data_Entry;
using System.Linq;

// See: https://gist.github.com/aksakalli/9191056
class SimpleHTTPServer
{
	private static IDictionary<string, string> _mimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
		#region extension to MIME type list
		{".asf", "video/x-ms-asf"},
		{".asx", "video/x-ms-asf"},
		{".avi", "video/x-msvideo"},
		{".bin", "application/octet-stream"},
		{".cco", "application/x-cocoa"},
		{".crt", "application/x-x509-ca-cert"},
		{".css", "text/css"},
		{".deb", "application/octet-stream"},
		{".der", "application/x-x509-ca-cert"},
		{".dll", "application/octet-stream"},
		{".dmg", "application/octet-stream"},
		{".ear", "application/java-archive"},
		{".eot", "application/octet-stream"},
		{".exe", "application/octet-stream"},
		{".flv", "video/x-flv"},
		{".gif", "image/gif"},
		{".hqx", "application/mac-binhex40"},
		{".htc", "text/x-component"},
		{".htm", "text/html"},
		{".html", "text/html"},
		{".ico", "image/x-icon"},
		{".img", "application/octet-stream"},
		{".iso", "application/octet-stream"},
		{".jar", "application/java-archive"},
		{".jardiff", "application/x-java-archive-diff"},
		{".jng", "image/x-jng"},
		{".jnlp", "application/x-java-jnlp-file"},
		{".jpeg", "image/jpeg"},
		{".jpg", "image/jpeg"},
		{".js", "application/x-javascript"},
		{".mml", "text/mathml"},
		{".mng", "video/x-mng"},
		{".mov", "video/quicktime"},
		{".mp3", "audio/mpeg"},
		{".mpeg", "video/mpeg"},
		{".mpg", "video/mpeg"},
		{".msi", "application/octet-stream"},
		{".msm", "application/octet-stream"},
		{".msp", "application/octet-stream"},
		{".pdb", "application/x-pilot"},
		{".pdf", "application/pdf"},
		{".pem", "application/x-x509-ca-cert"},
		{".pl", "application/x-perl"},
		{".pm", "application/x-perl"},
		{".png", "image/png"},
		{".prc", "application/x-pilot"},
		{".ra", "audio/x-realaudio"},
		{".rar", "application/x-rar-compressed"},
		{".rpm", "application/x-redhat-package-manager"},
		{".rss", "text/xml"},
		{".run", "application/x-makeself"},
		{".sea", "application/x-sea"},
		{".shtml", "text/html"},
		{".sit", "application/x-stuffit"},
		{".swf", "application/x-shockwave-flash"},
		{".tcl", "application/x-tcl"},
		{".tk", "application/x-tcl"},
		{".txt", "text/plain"},
		{".war", "application/java-archive"},
		{".wbmp", "image/vnd.wap.wbmp"},
		{".wmv", "video/x-ms-wmv"},
		{".xml", "text/xml"},
		{".xpi", "application/x-xpinstall"},
		{".zip", "application/zip"},
		#endregion
	};
	private Thread _serverThread;
	private string _rootDirectory;
	private HttpListener _listener;
	private int _port;

	private QueryHandler handler;

	public int Port
	{
		get { return _port; }
		private set { }
	}

	/// <summary>
	/// Construct server with given port.
	/// </summary>
	/// <param name="path">Directory path to serve.</param>
	/// <param name="port">Port of the server.</param>
	public SimpleHTTPServer(string path, int port, QueryHandler handler)
	{
		this.handler = handler;
		this.Initialize(path, port);
	}

	/// <summary>
	/// Construct server with suitable port.
	/// </summary>
	/// <param name="path">Directory path to serve.</param>
	public SimpleHTTPServer(string path)
	{
		//get an empty port
		TcpListener l = new TcpListener(IPAddress.Loopback, 0);
		l.Start();
		int port = ((IPEndPoint)l.LocalEndpoint).Port;
		l.Stop();
		this.Initialize(path, port);
	}

	/// <summary>
	/// Stop server and dispose all functions.
	/// </summary>
	public void Stop()
	{
		_serverThread.Abort();
		_listener.Stop();
	}

	private void Listen()
	{
		string listener_prefix = "http://*:" + _port.ToString() + "/";
		//Give permission to the listener's URL
		//Full argument is: netsh http add urlacl url=[listener_prefix here] user=DOMAIN\user
		string arguments = "http add urlacl url=" + listener_prefix + " user=DOMAIN\\user";
		ProcessStartInfo start_info = new ProcessStartInfo("netsh", arguments);
		start_info.RedirectStandardOutput = true;
		start_info.UseShellExecute = false;
		start_info.CreateNoWindow = true;
		Process.Start(start_info);

		_listener = new HttpListener();
		//_listener.Prefixes.Add("http://localhost:" + _port.ToString() + "/");
		//_listener.Prefixes.Add("http://localhost:" + _port.ToString() + "/chronology/");
		_listener.Prefixes.Add("http://*:" + _port.ToString() + "/");
		_listener.Prefixes.Add("http://*:" + _port.ToString() + "/chronology/");
		_listener.Start();
		while (true)
		{
			try
			{
				HttpListenerContext context = _listener.GetContext();
				HTTPProcess(context);
			}
			catch (Exception ex)
			{

			}
		}
	}

	private void HTTPProcess(HttpListenerContext context)
	{

		string filename = context.Request.Url.AbsolutePath;

		string body = new StreamReader(context.Request.InputStream).ReadToEnd();

		Console.WriteLine("body: " + body);

		dynamic data = JObject.Parse(body);

		switch (filename) {
			case "/chronology":
				// Get the data from the HTTP stream

				string query = "CHRONOLOGY:" + data.id + ":" + data.turns;
				string result = handler.ParseInput(query);

				dynamic response = new JObject();
				//split and ignore last empty one
				response.sequence = new JArray(result.Split(new string[] { "::" }, StringSplitOptions.None).Reverse().Skip(1).Reverse().ToArray());

				//write response
				byte[] b = Encoding.UTF8.GetBytes(response.ToString());
				context.Response.StatusCode = (int)HttpStatusCode.OK;
				context.Response.KeepAlive = false;
				context.Response.ContentLength64 = b.Length;
				context.Response.OutputStream.Write(b, 0, b.Length);
				context.Response.OutputStream.Close();

				break;

			case "/":

				//maybe list the options here?

				break;
		}
	}

	private void Initialize(string path, int port)
	{
		this._rootDirectory = path;
		this._port = port;
		_serverThread = new Thread(this.Listen);
		_serverThread.Start();
	}


}