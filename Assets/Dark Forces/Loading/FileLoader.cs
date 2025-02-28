﻿using Microsoft.Win32;
using MZZT.DarkForces.FileFormats;
using MZZT.Extensions;
using MZZT.FileFormats;
using MZZT.Steam;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace MZZT.DarkForces {
	/// <summary>
	/// A class to assist in loading data from GOB and LFD files.
	/// </summary>
	public class FileLoader : Singleton<FileLoader> {
		/// <summary>
		/// The standard files which should be searched for GOB data files.
		/// </summary>
		public static readonly string[] DARK_FORCES_STANDARD_DATA_FILES = new[] {
			@"DARK.GOB",
			@"SOUNDS.GOB",
			@"SPRITES.GOB",
			@"TEXTURES.GOB",
			@"LOCAL.MSG"
		};

		private struct ResourceLocation {
			public string FilePath;
			public long Offset;
			public long Length;
		}

		private struct LfdInfo {
			public string LfdPath;
			public Dictionary<string, ResourceLocation> Files;
		}

		[SerializeField, Header("Folders")]
		private string darkForcesFolder;
		/// <summary>
		/// The Dark Forces game folder used as a base search path for data.
		/// </summary>
		public string DarkForcesFolder { get => this.darkForcesFolder; set => this.darkForcesFolder = value; }

		public string GobPath;

		/// <summary>
		/// Try and autodetect the Dark Forces folder.
		/// </summary>
		/// <returns>The folder found, or null if not.</returns>
		public async Task<string> LocateDarkForcesAsync() {
			// TODO Add support for my Knight launcher, which stores the DF path in the regsitry.
			// I haven't run it in forever and my own path it stored is now wrong so meh.
			// We could also leverage Knight data to make picking mods easier since it knows what files
			// a mod needs and which files they override.

			// Try and find Steam and see if DF is installed there.
			string path = null;
			try {
				using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
				path = (string)key?.GetValue("SteamPath");
			} catch (PlatformNotSupportedException) {
			}
			if (path == null) {
				return null;
			}
			path = path.Replace('/', Path.DirectorySeparatorChar);

			// Is Dark Forces installed in the Steam folder?
			if (File.Exists(Path.Combine(path, "SteamApps", "common", "Dark Forces", "Game", "DARK.GOB"))) {
				return Path.Combine(path, "SteamApps", "common", "Dark Forces", "Game");
			}

			// Check other library folders. Start by reading the list of library folders.
			ValveDefinitionFile libraryFoldersVdf =
				await ValveDefinitionFile.ReadAsync(Path.Combine(path, "SteamApps", "libraryfolders.vdf"));

			// Normally we could deserialize into a type but the format of this file doesn't work well for that.
			// But we can read the tokens by hand.

			int pos = 0;
			List<string> libraryFolders = new List<string>();
			while (pos < libraryFoldersVdf.Tokens.Count) {
				ValveDefinitionFile.Token token = libraryFoldersVdf.Tokens[pos];
				pos++;

				string property = (token as ValveDefinitionFile.StringToken)?.Text;
				if (property != "path" || pos >= libraryFoldersVdf.Tokens.Count) {
					continue;
				}

				token = libraryFoldersVdf.Tokens[pos];
				pos++;
				string value = (token as ValveDefinitionFile.StringToken)?.Text;
				if (value == null) {
					continue;
				}

				libraryFolders.Add(value);
			}

			foreach (string libraryFolder in libraryFolders) {
				if (File.Exists(Path.Combine(libraryFolder, "SteamApps", "common", "Dark Forces", "Game", "DARK.GOB"))) {
					return Path.Combine(libraryFolder, "SteamApps", "common", "Dark Forces", "Game");
				}
			}

			return null;
		}

		private readonly Dictionary<string, List<ResourceLocation>> gobMap = new Dictionary<string, List<ResourceLocation>>();
		private readonly Dictionary<string, LfdInfo> lfdOverrides = new Dictionary<string, LfdInfo>();
		private readonly Dictionary<string, string[]> gobFiles = new Dictionary<string, string[]>();

		/// <summary>
		/// Clear all cached data.
		/// </summary>
		public void Clear() {
			this.gobFiles.Clear();
			this.gobMap.Clear();
			this.lfdOverrides.Clear();
		}

		/// <summary>
		/// GOB files we know the contents of.
		/// </summary>
		public IEnumerable<string> Gobs => this.gobFiles.Keys;

		/// <summary>
		/// Reads in a GOB file and tracks the files inside of it so we can quickly find them later.
		/// </summary>
		/// <param name="path">Path to the GOB file.</param>
		public async Task AddGobFileAsync(string path, bool rebuild = false) {
			if (this.DarkForcesFolder != null) {
				path = Path.Combine(this.DarkForcesFolder, path);
				GobPath = path;
			}
			string key = path.ToUpper();


			// Skip if we already loaded it.
			if (this.gobFiles.ContainsKey(key) && !rebuild) {
				return;
			}

			switch (Path.GetExtension(key)) {
				case ".GOB": {
					DfGobContainer gob = await DfGobContainer.ReadAsync(path, false);
					List<string> files = new List<string>();
					foreach ((string name, uint offset, uint size) in gob.Files) {
						files.Add(name.ToUpper());
						// Track the GOB, offset, and size of every file.
						this.AddToGobMap(name, new ResourceLocation() {
							FilePath = path,
							Offset = offset,
							Length = size
						});
						Debug.Log(string.Format("Added To GOBMap Name = {0} Path = {1} Offset = {2} Length = {3}", name, path, offset, size));
					}
					this.gobFiles[key] = files.ToArray();
				} break;
				// I used to lump GOBs and LFDs together but LFDs work differently so they're separate now.
				case ".LFD": /*{
					LandruFileDirectory lfd = await LandruFileDirectory.ReadAsync(path);
					List<string> files = new List<string>();
					foreach ((string name, string type, uint offset, uint size) in lfd.Files) {
						files.Add(name.ToUpper());
						this.AddToMap($"{name}.{type}", new ResourceLocation() {
							FilePath = path,
							Offset = offset,
							Length = size,
							Priority = priority
						});
					}
					this.files[path] = files.ToArray();
				} break;*/
					throw new NotSupportedException();
				default: {
					// An individual file, add it to the tracking list.
					string file = Path.GetFileName(path);
					// It's not in a GOB so offset is 0 and size is the full file size.
					this.AddToGobMap(file, new ResourceLocation() {
						FilePath = path,
						Offset = 0,
						Length = new FileInfo(path).Length
					});
					this.gobFiles[key] = new[] { file };
				} break;
			}
		}

		private void AddToGobMap(string name, ResourceLocation info) {
			name = name.ToUpper();
			// Mods can override files in base GOBs. So just add a record into the end of a list.
			// When we remove mod files we can remove the record and use the base file instead.
			if (!this.gobMap.TryGetValue(name, out List<ResourceLocation> results)) {
				this.gobMap[name] = results = new List<ResourceLocation>();
			}
			results.Add(info);
		}

		/// <summary>
		/// Remove a GOB/file so base files will be used instead.
		/// </summary>
		/// <param name="path">Path that was passed into the Add call.</param>
		public void RemoveGobFile(string path) {
			if (this.DarkForcesFolder != null) {
				path = Path.Combine(this.DarkForcesFolder, path);
			}
			string key = path.ToUpper();
			string[] files = this.gobFiles[key];
			this.gobFiles.Remove(key);
			foreach (string file in files) {
				List<ResourceLocation> results = this.gobMap[file];
				foreach (int i in results.Select((x, i) => (x, i)).Where(x => x.x.FilePath == path)
					.Reverse().Select(x => x.i)) {

					results.RemoveAt(i);
				}
				if (results.Count == 0) {
					this.gobMap.Remove(file);
				}
			}
		}

		/// <summary>
		/// Gets a Stream for a file expected to be found in a GOB or standalone.
		/// </summary>
		/// <param name="name">The name and extension of the file.</param>
		/// <returns>The Stream you can read file data from.</returns>
		public async Task<Stream> GetGobFileStreamAsync(string name) {
			if (!this.gobMap.TryGetValue(name, out List<ResourceLocation> results)) {
				return null;
			}
			// The most recently added GOB/file will be this one, so we are using a mod override if available.
			ResourceLocation location = results.Last();
			//Debug.Log(string.Format("Name = {0}: Path = {3} Length  = {2} Offset = {1}", name, location.Offset, location.Length, location.FilePath));
			// Open the GOB/file and read in the data at the specified offset and size.
			FileStream stream = new FileStream(location.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			Stream scoped = null;
			try {
				stream.Seek(location.Offset, SeekOrigin.Begin);
				if (location.Offset > 0 || location.Length < stream.Length) {
					scoped = new MemoryStream((int)location.Length);
					await stream.CopyToWithLimitAsync(scoped, (int)location.Length);
					scoped.Position = 0;
				} else {
					scoped = stream;
				}
			} catch (Exception) {
				if (scoped != stream) {
					scoped?.Dispose();
				}
				throw;
			} finally {
				if (scoped != stream) {
					stream.Dispose();
				}
			}

			return scoped;
		}

		/// <summary>
		/// Load in a file expected to be found in a GOB or standalone.
		/// </summary>
		/// <param name="name">The name and extension of the file.</param>
		/// <returns>The loaded object.</returns>
		public async Task<IFile> LoadGobFileAsync(string name) {
			Stream stream = await this.GetGobFileStreamAsync(name);
			if (stream == null) {
				return null;
			}

			using (stream) {
				// Load the data
				name = name.ToUpper();
				if (!DfGobContainer.FileTypes.TryGetValue(name, out Type type)) {
					if (!DfGobContainer.FileTypes.TryGetValue(Path.GetExtension(name), out type)) {
						type = typeof(Raw);
					}
				}
				IFile file = (IFile)Activator.CreateInstance(type);
				await file.LoadAsync(stream);
				return file;
			}
		}

		/// <summary>
		/// Load in a file expected to be found in a GOB or standalone.
		/// </summary>
		/// <typeparam name="T">The data type of the file.</typeparam>
		/// <param name="name">The name and extension of the file.</param>
		/// <returns>The loaded object.</returns>
		public async Task<T> LoadGobFileAsync<T>(string name) where T : IFile, new() {
			Stream stream = await this.GetGobFileStreamAsync(name);
			if (stream == null) {
				return default;
			}

			using (stream) {
				// Load the data
				IFile file = (IFile)Activator.CreateInstance(typeof(T));
				await file.LoadAsync(stream);
				return (T)file;
			}
		}

		/// <summary>
		/// Track an LFD. A mod's LFD can replace a base one.
		/// </summary>
		/// <param name="path">Path of the LFD.</param>
		/// <param name="replace">A LFD to override, or none if null.</param>
		public void AddLfd(string path, string replace = null) {
			replace ??= Path.GetFileName(path);

			this.lfdOverrides[replace.ToUpper()] = new LfdInfo() {
				LfdPath = path
			};
		}

		/// <summary>
		/// Remove an LFD from tracking.
		/// </summary>
		/// <param name="path">The path passed into AddLfd.</param>
		public void RemoveLfd(string path) {
			string replace = this.lfdOverrides.FirstOrDefault(x => x.Value.LfdPath == path.ToUpper()).Key;
			if (replace == null) {
				return;
			}
			this.lfdOverrides.Remove(replace);
		}

		/// <summary>
		/// Read a file from an LFD.
		/// </summary>
		/// <param name="lfdName">The name of the LFD, without path. Will use mod overrides.</param>
		/// <param name="name">The name of the file without extension.</param>
		/// <param name="typeName">The type of the file.</param>
		/// <returns>The Stream for the file.</returns>
		public async Task<Stream> GetLfdFileStreamAsync(string lfdName, string name, string typeName) {
			if (this.lfdOverrides.TryGetValue(lfdName.ToUpper(), out LfdInfo map)) {
				lfdName = map.LfdPath;
			} else {
				map.LfdPath = lfdName;
			}
			string lfdPath = Path.Combine(this.DarkForcesFolder, "LFD", lfdName);

			Stream stream = null;
			try {
				// If we didn't read this LFD before, load in a map of its files so we don't have to read in
				// the entire LFD file directory next time.
				if (map.Files == null) {
					LandruFileDirectory lfd = await LandruFileDirectory.ReadAsync(lfdPath, async lfd => {
						// While we're here, read the file we need.
						stream = await lfd.GetFileStreamAsync(name, typeName);
					});
					map.Files = lfd.Files.ToDictionary(x => $"{x.name.ToUpper()}.{x.type.ToUpper()}", x => new ResourceLocation() {
						FilePath = lfdName,
						Offset = x.offset,
						Length = x.size
					});
					this.lfdOverrides[lfdName.ToUpper()] = map;
				} else {
					if (!map.Files.TryGetValue($"{name.ToUpper()}.{typeName.ToUpper()}", out ResourceLocation location)) {
						return null;
					}
					// Otherwise seek right to the location in the LFD where the file is and read it.
					using FileStream fileStream = new FileStream(lfdPath, FileMode.Open, FileAccess.Read, FileShare.Read);
					fileStream.Seek(location.Offset, SeekOrigin.Begin);
					stream = new MemoryStream((int)location.Length);
					await fileStream.CopyToWithLimitAsync(stream, (int)location.Length);
					stream.Position = 0;
				}
			} catch (Exception) {
				stream?.Dispose();
				throw;
			}
			return stream;
		}

		/// <summary>
		/// Read a file from an LFD.
		/// </summary>
		/// <param name="lfdName">The name of the LFD, without path. Will use mod overrides.</param>
		/// <param name="name">The name of the file without extension.</param>
		/// <param name="typeName">The type of the file.</param>
		/// <returns>The loaded object.</returns>
		public async Task<IFile> LoadLfdFileAsync(string lfdName, string name, string typeName) {
			Stream stream = await this.GetLfdFileStreamAsync(lfdName, name, typeName);
			if (stream == null) {
				return null;
			}

			using (stream) {
				if (!LandruFileDirectory.FileTypes.TryGetValue(typeName, out Type type)) {
					throw new ArgumentException("Invalid type.", nameof(typeName));
				}

				IFile file = (IFile)Activator.CreateInstance(type);
				await file.LoadAsync(stream);
				return file;
			}
		}

		/// <summary>
		/// Read a file from an LFD.
		/// </summary>
		/// <typeparam name="T">The type of the file.</typeparam>
		/// <param name="lfdName">The name of the LFD, without path. Will use mod overrides.</param>
		/// <param name="name">The name of the file without extension.</param>
		/// <returns>The loaded object.</returns>
		public async Task<T> LoadLfdFileAsync<T>(string lfdName, string name) where T : DfFile<T>, new() {
			if (!LandruFileDirectory.FileTypeNames.TryGetValue(typeof(T), out string typeName)) {
				throw new ArgumentException("Invalid type.", nameof(T));
			}

			Stream stream = await this.GetLfdFileStreamAsync(lfdName, name, typeName);
			if (stream == null) {
				return null;
			}

			using (stream) {
				return await DfFile<T>.ReadAsync(stream);
			}
		}

		/// <summary>
		/// Read standard GOB file directroy information and cache it.
		/// </summary>
		public async Task LoadStandardGobFilesAsync() {
			foreach (string name in DARK_FORCES_STANDARD_DATA_FILES) {
				await this.AddGobFileAsync(Path.Combine(this.DarkForcesFolder, name));
			}
		}

		/// <summary>
		/// Search for files of a specific type.
		/// </summary>
		/// <param name="pattern">The pattern to match/</param>
		/// <param name="gob">Optionally, a specific GOB to search, or null for all of them.</param>
		/// <returns>The filenames which match.</returns>
		public IEnumerable<string> FindGobFiles(string pattern, string gob = null) {
			string[][] files;
			if (gob != null) {
				files = new string[][] { null };
				if (!this.gobFiles.TryGetValue(gob.ToUpper(), out files[0])) {
					return Enumerable.Empty<string>();
				}
			} else {
				files = this.gobFiles.Select(x => x.Value).ToArray();
			}

			Regex patterns = new Regex("^" + string.Join("", pattern.Select(x => x switch {
				'*' => ".*",
				'?' => ".",
				_ => Regex.Escape(x.ToString())
			})) + "$", RegexOptions.IgnoreCase);
			return files.SelectMany(x => x).Where(x => patterns.IsMatch(x)).Distinct();
		}
	}
}
