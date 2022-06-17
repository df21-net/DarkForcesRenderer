using MZZT.DarkForces.FileFormats;
using MZZT.DarkForces.Showcase;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;

namespace MZZT.DarkForces {
	/// <summary>
	/// Loads a level and all related data.
	/// </summary>
	/// 
	
	[Serializable]
	public class MyClass
	{
		public int level;
		public float timeElapsed;
		public string playerName;
	}

	public class LevelLoader : Singleton<LevelLoader> {
		/// <summary>
		/// The data from the LEV file.
		/// </summary>
		public DfLevel Level { get; private set; }
		/// <summary>
		/// The data from the INF file.
		/// </summary>
		public DfLevelInformation Information { get; private set; }
		/// <summary>
		/// The data from the O file.
		/// </summary>
		public DfLevelObjects Objects { get; private set; }
		/// <summary>
		/// The data from the PAL file.
		/// </summary>
		public DfPalette Palette { get; private set; }
		/// <summary>
		/// The data from the CMP file.
		/// </summary>
		public DfColormap ColorMap { get; private set; }

		[SerializeField, Header("Level")]
		private int currentLevel = -1;
		/// <summary>
		/// The current level index in the level list.
		/// </summary>
		public int CurrentLevelIndex => this.currentLevel;
		/// <summary>
		/// The current level display name.
		/// </summary>
		public string CurrentLevelName => this.currentLevel >= 0 ?
			this.LevelList.Levels[this.currentLevel].FileName : null;

		/// <summary>
		/// The data from JEVI.LVL.
		/// </summary>
		public DfLevelList LevelList { get; private set; }

		/// <summary>
		/// Load JEDI.LVL.
		/// </summary>
		public async Task LoadLevelListAsync(bool addHiddenLevels = false, bool FirstTime = true) {
			this.LevelList = null;

			if (FirstTime) { await PauseMenu.Instance.BeginLoadingAsync(); }

			try {
				this.LevelList = await FileLoader.Instance.LoadGobFileAsync<DfLevelList>("JEDI.LVL");
				Debug.Log("Loading Level List success");
			} catch (Exception e) {
				ResourceCache.Instance.AddError("JEDI.LVL", e);

				this.LevelList = new DfLevelList();
				Debug.Log("Loading Level List fail");
			}

			Debug.Log(string.Format("total level list size is {0}", this.LevelList.Levels.Count));

			if (addHiddenLevels) {
				Debug.Log("oh no ... going for hidden maps");
				// Find the GOB file with the levels.
				string path = Mod.Instance.Gob ?? Path.Combine(FileLoader.Instance.DarkForcesFolder, "DARK.GOB");
				Debug.Log("Got past path");
				// Find any .LEV files in that GOB.
				string[] levels = FileLoader.Instance.FindGobFiles("*.LEV", path).Select(x => x.ToUpper()).ToArray();
				// Exclude any files in the level list.
				levels = levels.Except(this.LevelList.Levels.Select(x => $"{x.FileName.ToUpper()}.LEV")).ToArray();
				// Add any files in the GOB but not in the level list so the user can view them.
				this.LevelList.Levels.AddRange(levels.Select(x => new DfLevelList.Level() {
					FileName = Path.GetFileNameWithoutExtension(x),
					DisplayName = $"{Path.GetFileNameWithoutExtension(x)} (Unused)"
				}));
			}

			if (FirstTime) { PauseMenu.Instance.EndLoading(); }
		}

		/// <summary>
		/// Load a level's CMP file.
		/// </summary>
		public async Task LoadColormapAsync(bool FirstTime = true) {
			this.ColorMap = null;

			if (FirstTime)
			{
				await PauseMenu.Instance.BeginLoadingAsync();
			}

			string levelFile = this.CurrentLevelName;

			this.ColorMap = await ResourceCache.Instance.GetColormapAsync($"{levelFile}.CMP");

			if (FirstTime)
			{
				PauseMenu.Instance.EndLoading();
			}
		}

		/// <summary>
		/// Load a level's PAL file.
		/// </summary>
		public async Task LoadPaletteAsync(bool FirstTime = true) {
			this.Palette = null;

			if (FirstTime)
			{
				await PauseMenu.Instance.BeginLoadingAsync();
			}

			this.Palette = await ResourceCache.Instance.GetPaletteAsync(this.Level.PaletteFile);

			if (FirstTime)
			{
				PauseMenu.Instance.EndLoading();
			}
		}

		/// <summary>
		/// Load a level's LEV file.
		/// </summary>
		/// <param name="levelIndex">The index of the level in JEDI.LVL.</param>
		public async Task LoadLevelAsync(int levelIndex, bool FirstTime = true) {
			if (Parallaxer.Instance != null) {
				Parallaxer.Instance.Reset();
			}

			this.Level = null;

			this.currentLevel = levelIndex;

			if (FirstTime)
			{
				await PauseMenu.Instance.BeginLoadingAsync();
			}
			Debug.Log(string.Format("Loading lvl index {0} with value {1}", levelIndex, this.CurrentLevelName));
			string levelFile = this.CurrentLevelName;


			try {
				this.Level = await FileLoader.Instance.LoadGobFileAsync<DfLevel>($"{levelFile}.LEV");
			} catch (Exception e) {
				ResourceCache.Instance.AddError($"{levelFile}.LEV", e);
			}
			if (this.Level != null) {
				ResourceCache.Instance.AddWarnings($"{levelFile}.LEV", this.Level);

				if (Parallaxer.Instance != null) {
					Parallaxer.Instance.Parallax = this.Level.Parallax.ToUnity();
				}
			}

			Debug.Log(string.Format("Loaded {0} sectors", this.Level.Sectors.Count));
			
			if (FirstTime)
			{
				PauseMenu.Instance.EndLoading();
			}
		}

		/// <summary>
		/// Load a level's INF file.
		/// </summary>
		public async Task LoadInformationAsync(bool FirstTime = true) {
			this.Information = null;

			if (FirstTime)
			{
				await PauseMenu.Instance.BeginLoadingAsync();
			}

			string levelFile = this.CurrentLevelName;

			try {
				this.Information = await FileLoader.Instance.LoadGobFileAsync<DfLevelInformation>($"{levelFile}.INF");
				if (this.Level != null) {
					this.Information.LoadSectorReferences(this.Level);					
				}
			} catch (Exception ex) {
				ResourceCache.Instance.AddError($"{levelFile}.INF", ex);
			}
			if (this.Information != null) {
				ResourceCache.Instance.AddWarnings($"{levelFile}.INF", this.Information);
			}

			if (FirstTime)
			{
				PauseMenu.Instance.EndLoading();
			}
		}

		/// <summary>
		/// Load a level's O file.
		/// </summary>
		public async Task LoadObjectsAsync(bool FirstTime = true) {
			this.Objects = null;

			if (FirstTime)
			{
				await PauseMenu.Instance.BeginLoadingAsync();
			}

			string levelFile = this.CurrentLevelName;

			try {
				this.Objects = await FileLoader.Instance.LoadGobFileAsync<DfLevelObjects>($"{levelFile}.O");
			} catch (Exception e) {
				ResourceCache.Instance.AddError($"{levelFile}.O", e);
			}
			if (this.Objects != null) {
				ResourceCache.Instance.AddWarnings($"{levelFile}.O", this.Objects);
			}
			/*
			for (int i = 0; i < LevelLoader.Instance.Objects.Objects.Count; i++)
			{
				FileFormats.DfLevelObjects.Object obj = LevelLoader.Instance.Objects.Objects[i];
				Debug.Log(string.Format("Obj {0} Hash {1} OBJPOS {2} OBJLOG {3}", i, obj.GetHashCode(), obj.Position, obj.Logic));
			}*/

			Debug.Log(string.Format("Loaded {0} objects", this.Objects.Objects.Count));
			if (FirstTime)
			{
				PauseMenu.Instance.EndLoading();
			}
		}

		/// <summary>
		/// Show any accumulated errors/warnings in file loading and clear them out.
		/// </summary>
		/// <param name="name">Filename to associate with the errors, null to use the current level name.</param>
		public async Task ShowWarningsAsync(string name = null) {
			ResourceCache.LoadWarning[] warnings = ResourceCache.Instance.Warnings.ToArray();
			if (warnings.Length == 0) {
				return;
			}

			if (name == null) {
				name = this.CurrentLevelName;
			}
			// Show fatal and non-fatal errors that occurred loading level data and generating Unity objects.
			string fatal = string.Join("\n", warnings
				.Where(x => x.Fatal)
				.Select(x => $"{x.FileName}{(x.Line > 0 ? $":{x.Line}" : "")} - {x.Message}"));
			string warning = string.Join("\n", warnings
				.Where(x => !x.Fatal)
				.Select(x => $"{x.FileName}{(x.Line > 0 ? $":{x.Line}" : "")} - {x.Message}"));
			if (fatal.Length > 0) {
				if (warning.Length > 0) {
					await DfMessageBox.Instance.ShowAsync($"{name} failed to load:\n\n{fatal}\n{warning}");
				} else {
					await DfMessageBox.Instance.ShowAsync($"{name} failed to load:\n\n{fatal}");
				}
			} else {
				await DfMessageBox.Instance.ShowAsync($"{name} loaded with warnings:\n\n{warning}");
			}
			ResourceCache.Instance.ClearWarnings();
		}

	}
}
