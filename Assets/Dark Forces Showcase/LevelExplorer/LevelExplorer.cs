using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Web;
using System.Text;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MZZT.DarkForces.FileFormats;
using System.Globalization;


namespace MZZT.DarkForces.Showcase {
	/// <summary>
	/// Script which powers the Level Explorer showcase.
	/// </summary>
	/// 

	
	public class LevelExplorer : Singleton<LevelExplorer> {
		
		private bool FirstTime;
		private static HttpListener listener;
		private static int httpPort;
		private string LEVPath;
		private string GOBPath;
		public bool UpdateCamera;
		
		public List<FileFormats.DfLevel.Sector> sectors;
		public List<FileFormats.DfLevelObjects.Object> objects;
        public List<int> sectorFilter;
		public List<int> objectFilter;
		public List<int> sectorDeletes;
		public List<int> objectDeletes;



		private async void Start() {
			// This is here in case you run directly from the LevelExplorer sccene instead of the menu.
			Debug.Log("New Start");

			int width = PlayerPrefs.GetInt("width", Screen.width);
			int height = PlayerPrefs.GetInt("height", Screen.height);
			bool full = PlayerPrefs.GetInt("fullscreen", 0) == 0 ? false : true ;
			Screen.SetResolution(width, height, full);

			// Unity won't process commas
			CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
			CultureInfo.CurrentCulture = new CultureInfo("en-US");
			Debug.Log(string.Format("Changing Default Culture to {0}.", CultureInfo.DefaultThreadCurrentCulture.Name));

			if (!FileLoader.Instance.Gobs.Any()) {
				await FileLoader.Instance.LoadStandardGobFilesAsync();
			}

			// This is no longer needed as this is made to preview WDFUSE instead of base game
			// This version is for WDFUSE. 
			//await FileLoader.Instance.AddGobFileAsync(Path.Combine(FileLoader.Instance.DarkForcesFolder, "dark.gob"), true);

			await PauseMenu.Instance.BeginLoadingAsync();

			ResourceCache.Instance.ClearWarnings();

			await LevelLoader.Instance.LoadLevelListAsync(true, true);

			await LevelLoader.Instance.ShowWarningsAsync("JEDI.LVL");

			// Different behavior depending on first launch or not. 
			this.FirstTime = true;
			this.UpdateCamera = true;
			this.sectorFilter = new List<int>();
			this.objectFilter = new List<int>();

			if (LevelLoader.Instance.CurrentLevelIndex >= 0) {
				await this.LoadAndRenderLevelAsync(LevelLoader.Instance.CurrentLevelIndex);
			}


			PauseMenu.Instance.EndLoading();
			
			this.FirstTime = false;
			this.UpdateCamera = false;
			await this.UpdateLevel();
		}

		/// <summary>
		/// Load a level and generate Unity objects.
		/// </summary>
		public async Task LoadAndRenderLevelAsync(int levelIndex) {
			// Clear out existing level data.
			LevelMusic.Instance.Stop();
			LevelGeometryGenerator.Instance.Clear();
			ObjectGenerator.Instance.Clear();
			
			Debug.Log(string.Format("In load and render"));
			if (this.FirstTime == true) {
				await PauseMenu.Instance.BeginLoadingAsync(); 
			}
			else
            {
				Debug.Log(string.Format("Awaiting gobpath {0}", this.GOBPath));
				await FileLoader.Instance.AddGobFileAsync(this.GOBPath, true);				
			}

			await LevelLoader.Instance.LoadLevelListAsync(false, false);
			
			// Make sure you load only the level specified by WDFUSE
			DfLevelList LevelList = LevelLoader.Instance.LevelList;
			for (int i = 0; i < LevelList.Levels.Count; i++)
			{
				Debug.Log(string.Format("Looking at index {0} for at value {1}", i, LevelList.Levels[i].FileName));
				if (LevelList.Levels[i].FileName == this.LEVPath)
				{
					levelIndex = i;
					Debug.Log(string.Format("Found index {0} for level {1}", i, this.LEVPath));
					break;
				}
			}

			await LevelMusic.Instance.PlayAsync(levelIndex);

			await LevelLoader.Instance.LoadLevelAsync(levelIndex, this.FirstTime);
			if (LevelLoader.Instance.Level != null) {
				await LevelLoader.Instance.LoadColormapAsync(this.FirstTime);
				if (LevelLoader.Instance.ColorMap != null) {
					await LevelLoader.Instance.LoadPaletteAsync(this.FirstTime);
					if (LevelLoader.Instance.Palette != null) {
						await LevelGeometryGenerator.Instance.GenerateAsync(this.sectorFilter);

						await LevelLoader.Instance.LoadObjectsAsync(this.FirstTime);
						if (LevelLoader.Instance.Objects != null) {
							await ObjectGenerator.Instance.GenerateAsync(this.UpdateCamera, this.objectFilter);
						}
					}
				}
			}

			if (this.FirstTime == true)
			{
				await LevelLoader.Instance.ShowWarningsAsync();
				PauseMenu.Instance.EndLoading();
			}
		}
		
		/// <summary>
		/// Deserialize JSON payload from WDFUSE
		/// </summary>
		public static object Deserialize(string path)
		{
			var serializer = new JsonSerializer();

			using (var sw = new StreamReader(path))
			using (var reader = new JsonTextReader(sw))
			{
				return serializer.Deserialize(reader);
			}
		}

		/// <summary>
		/// Respond immediately tho WDFUSE 
		/// </summary>
		public static void SendResponse(HttpListenerContext context)
        {
			HttpListenerResponse response = context.Response;
			string responseString = "Processed. ";
			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
			response.ContentLength64 = buffer.Length;
			System.IO.Stream output = response.OutputStream;
			output.Write(buffer, 0, buffer.Length);
			output.Close();
		}

		/// <summary>
		/// Handle the JSON request. It includes parsing the Sector and Object deltas
		/// </summary>
		public async Task  ProcessRequest(HttpListenerContext context)
		{
			HttpListenerRequest request = context.Request;
			System.IO.Stream body = request.InputStream;
			System.Text.Encoding encoding = request.ContentEncoding;
			System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);

			Debug.Log(string.Format("Start WDFUSE data (Length = {0}", request.ContentLength64));
			string rootjson = WebUtility.UrlDecode(reader.ReadToEnd());
			Dictionary<string, string> jsoninputs = JsonConvert.DeserializeObject<Dictionary<string, string>>(rootjson);

			// Once we get results immedaitely respond
			SendResponse(context);

			this.GOBPath = jsoninputs["GOBPath"];
			this.LEVPath = jsoninputs["LEVPath"];

			Debug.Log(string.Format("Received GOB {0} and level {1}", this.GOBPath, this.LEVPath));

			this.sectorFilter.Clear();
			this.objectFilter.Clear();
			this.UpdateCamera = jsoninputs.ContainsKey("UpdateCamera");

			// Full Reload of the Map
			if (jsoninputs.ContainsKey("Reset"))
			{
				await LoadAndRenderLevelAsync(0);
			}
			else
			{
				// Partial Reload of Modified Items

				// ------- SECTORS --------- //

				if (jsoninputs.ContainsKey("Sectors"))
				{
					sectors = LevelLoader.Instance.Level.Sectors;
					string rawSectors = jsoninputs["Sectors"];
					if (rawSectors != "")
					{
						SortedDictionary<int, string> sector_updates = JsonConvert.DeserializeObject<SortedDictionary<int, string>>(rawSectors);
						Dictionary<int, string> newsectors = new Dictionary<int, string>();

						// First add the raw sectors to the sector list
						foreach (var (sectorId, sectorstr) in sector_updates.Select(x => (x.Key, x.Value)))
						{
							Debug.Log(string.Format("Parsing Sector {0}", sectorId));

							// Store sector ids so we can adjoin them later
							newsectors[sectorId] = sectorstr;

							// Used for the Unity Game Object filter
							sectorFilter.Add(sectorId);

							// Deserialize them and repopulate the sector map
							FileFormats.DfLevel.Sector newsector = DeserializeSector(sectorstr);

							if (sectors.Count <= sectorId)
							{
								sectors.Add(newsector);
							}
							else
							{
								LevelGeometryGenerator.Instance.DeleteObjectByHash(sectors[sectorId]);
								sectors[sectorId] = newsector;
							}
						}

						//Then update all the sector references
						foreach (var (sectorId, sectorstr) in newsectors.Select(x => (x.Key, x.Value)))
						{
							Debug.Log(string.Format("Fixing Sector Refs {0}", sectorId));
							UpdateReferences(sectorId, sectorstr);
						}

					}

					string rawSectorDels = jsoninputs["SectorsDel"];
					sectorDeletes = JsonConvert.DeserializeObject<List<int>>(rawSectorDels);

					// Reverse the list - you want to delete from the end.
					sectorDeletes.Sort();
					sectorDeletes.Reverse();
					
					foreach (int sectorId in sectorDeletes)
					{
						//Debug.Log(string.Format("Removing Sector {0}", sectorId));
						LevelGeometryGenerator.Instance.DeleteObjectByHash(sectors[sectorId]);
						sectors.RemoveAt(sectorId);
					}

					LevelLoader.Instance.Level.Sectors = sectors;
					await LevelGeometryGenerator.Instance.GenerateAsync(this.sectorFilter);
					LevelGeometryGenerator.Instance.RefreshVisiblity();
				}



				// ------- OBJECTS -------- //
				if (jsoninputs.ContainsKey("Objects"))
				{
					string rawObjects = jsoninputs["Objects"];
					objects = LevelLoader.Instance.Objects.Objects;


					for (int i = 0; i < LevelLoader.Instance.Objects.Objects.Count; i++)
					{
						FileFormats.DfLevelObjects.Object obj = LevelLoader.Instance.Objects.Objects[i];
						Debug.Log(string.Format("Obj {0} Hash {1} OBJPOS {2} OBJLOG {3}", i, obj.GetHashCode(), obj.Position, obj.Logic));
					}

					Debug.Log(string.Format("Received Objects {0}", rawObjects));
					if (rawObjects != "")
					{
						SortedDictionary<int, string> object_updates = JsonConvert.DeserializeObject<SortedDictionary<int, string>>(rawObjects);


						foreach (var (objectId, objectstring) in object_updates.Select(x => (x.Key, x.Value)))
						{
							Debug.Log(string.Format("Parsing Object {0}", objectId));
							Debug.Log(string.Format("Objstring = {0}", objectstring));

							FileFormats.DfLevelObjects.Object newobject = DeserializeObject(objectstring);

							objectFilter.Add(objectId);

							if (objects.Count <= objectId)
							{
								objects.Add(newobject);
							}
							else
							{
								ObjectGenerator.Instance.DeleteObjectByHash(objects[objectId]);
								objects[objectId] = newobject;
							}
						}
					}

					string rawObjectDels = jsoninputs["ObjectsDel"];
					Debug.Log(string.Format("Received Delete Objects {0}", rawObjectDels));
					objectDeletes = JsonConvert.DeserializeObject<List<int>>(rawObjectDels);

					// Reverse the list - you want to delete from the end.
					objectDeletes.Sort();
					objectDeletes.Reverse();
					foreach (int objectId in objectDeletes)
					{
						//Debug.Log(string.Format("Removing Object {0}", objectId));
						ObjectGenerator.Instance.DeleteObjectByHash(objects[objectId]);
						objects.RemoveAt(objectId);
					}

					LevelLoader.Instance.Objects.Objects = objects;
					if (objectFilter.Count > 0)
					{
						await ObjectGenerator.Instance.GenerateAsync(this.UpdateCamera, this.objectFilter);
					}

					for (int i = 0; i < LevelLoader.Instance.Objects.Objects.Count; i++)
					{
						FileFormats.DfLevelObjects.Object obj = LevelLoader.Instance.Objects.Objects[i];
						Debug.Log(string.Format("Obj {0} Hash {1} OBJPOS {2} OBJLOG {3}", i, obj.GetHashCode(), obj.Position, obj.Logic));
					}

				}
							
			}
			body.Close();
			reader.Close();
		}
		
		/// <summary>
		/// Deserialize the Sector Definition
		/// </summary>
		public FileFormats.DfLevel.Sector DeserializeSector(string json)
        {
			Debug.Log("Deserializing " + json);
			FileFormats.DfLevel.Sector sector = JsonConvert.DeserializeObject<FileFormats.DfLevel.Sector>(json);			
			return sector;
		}

		/// <summary>
		/// Deserialization wrecks the object pointers - need to rebuild references 
		/// </summary>
		public void UpdateReferences(int sectorid, string json)
        {
			FileFormats.DfLevel.Sector sector = sectors[sectorid];
			JObject secobj = JObject.Parse(json);

			// Fix the wall refs
			foreach ((FileFormats.DfLevel.Wall wallInfo, int j) in sector.Walls.Select((x, i) => (x, i)))
			{
				wallInfo.Sector = sector;

				// Rebuild wall refs
				int sectorrefid = Int32.Parse(secobj["Walls"][j]["Adjoin"].ToString());
				int mirrorrefid = Int32.Parse(secobj["Walls"][j]["Mirror"].ToString());

				 if (sectorrefid != -1)
				{ 
					// Update target sector refs (Referred Sector Wall adjoin points back to Local wall)
					FileFormats.DfLevel.Sector refsector = sectors[sectorrefid];

					if (!sectorFilter.Contains(sectorrefid)) 
					{
						sectorFilter.Add(sectorrefid);
                    }

					refsector.Walls[mirrorrefid].Adjoined = wallInfo;

					// Update local sector refs (Local sector wall adjoin points to Referred wall)
					wallInfo.Adjoined = refsector.Walls[mirrorrefid];
				}	
			}
		}


		/// <summary>
		/// Deserialization the object JSON
		/// </summary>
		public FileFormats.DfLevelObjects.Object DeserializeObject(string json)
		{
			FileFormats.DfLevelObjects.Object ob = JsonConvert.DeserializeObject<FileFormats.DfLevelObjects.Object>(json);

			// Rest Difficulty to Enum.
			int[] difficulties = Enum.GetValues(typeof(FileFormats.DfLevelObjects.Difficulties)).Cast<int>().ToArray();
			ob.Difficulty = Array.IndexOf(difficulties, ob.Difficulty) < 0 ? FileFormats.DfLevelObjects.Difficulties.EasyMediumHard : (FileFormats.DfLevelObjects.Difficulties) ob.Difficulty;
			return ob;
		}

		/// <summary>
		/// Loop through the ports and create an HTTP Listener. This is the entrypoint
		/// </summary>
		public async Task UpdateLevel()
		{

			// Create a listener on 8080 (try to launch through 8083)
			httpPort = 8080;
			
			if (listener is null)
			{

				for (int i = 0; i <= 3; i++)
				{
					try
					{
						httpPort = 8080 + i;
						listener = new HttpListener();
						Debug.Log(string.Format("Starting Listener on port {0}", httpPort));
						listener.Prefixes.Add(string.Format("http://localhost:{0}/", httpPort));
						listener.Start();
						break;
					}
					catch
					{
						Debug.Log(string.Format("Failed to connect on port {0}", httpPort));
					}
				}
			}


			// Just loop forever processing requests from WDFUSE
			while (true)
			{
				string sep = new string('-', 40);
				Debug.Log(sep + " Listening " + sep);

				var requests = new HashSet<Task>();
				requests.Add(listener.GetContextAsync());

				Task t = await Task.WhenAny(requests);
				requests.Remove(t);
				HttpListenerContext context = (t as Task<HttpListenerContext>).Result;
				try
				{
					await this.ProcessRequest(context);					
				}
				catch (Exception e)
				{
					Debug.Log(string.Format("Failed to process request due to {0}", e));
				}
				Debug.Log("Done");
			}
			

		}

		/// <summary>
		/// Destructor through UNITY
		/// </summary>
		private void Stop()
		{
			if (listener != null && listener.IsListening)
			{
				Debug.Log("Stopping Listener");
				listener.Stop();
			}
		}

		/// <summary>
		/// Destructor through CS
		/// </summary>
		~LevelExplorer()
        {

			if (listener != null && listener.IsListening)


			{
				Debug.Log("Stopping Listener");
				listener.Stop();
			}
        }

		private void OnApplicationQuit()
		{
			PlayerPrefs.SetInt("width", Screen.width);
			PlayerPrefs.SetInt("height", Screen.height);
			PlayerPrefs.SetInt("fullscreen", (Screen.fullScreen == true) ? 1 : 0);
		}

	}


}
