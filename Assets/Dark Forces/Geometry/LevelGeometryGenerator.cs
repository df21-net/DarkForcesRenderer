using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static MZZT.DarkForces.FileFormats.DfLevel;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;

namespace MZZT.DarkForces {
	/// <summary>
	/// Generate level geometry.
	/// </summary>
	public class LevelGeometryGenerator : Singleton<LevelGeometryGenerator> {
		/// <summary>
		/// 1 DFU is about 25cm. 1 Unity unit is 1 meter. So about 1/40 scale.
		/// Scale to Unity size for physics reasons.
		/// </summary>
		public const float GEOMETRY_SCALE =  1 / 40f;
		/// <summary>
		/// Scale texture size to match DF.
		/// </summary>
		public const float TEXTURE_SCALE = 1 / 8f;

		[SerializeField, Header("Layers")]
		private bool showAllLayers = true;
		/// <summary>
		/// Show all layers.
		/// </summary>
		public bool ShowAllLayers { get => this.showAllLayers; set => this.showAllLayers = value; }
		[SerializeField]
		private int layer = 0;
		/// <summary>
		/// Show specific layer.
		/// </summary>
		public int Layer { get => this.layer; set => this.layer = value; }

		/// <summary>
		/// Remove all generated geometry.
		/// </summary>
		public void Clear() {
			foreach (GameObject child in this.transform.Cast<Transform>().Select(x => x.gameObject).ToArray()) {
				DestroyImmediate(child);
			}
		}

		/// <summary>
        /// Delete a specific sector geometry
        /// </summary>
        /// <param name="SecId">ID of Sector to delete</param>
		public void DeleteObjectByHash(Sector SecId)
		{
			// We store the sector data as a HASH so we can isntantly find it. We cannot use a sector # because the #
			// changes as we add or delete sectors. Hash of a sector is the only solid way to keep track of them. 
			// When finding the game object that matches the sector hash - we delete it. 
			foreach (GameObject child in this.transform.Cast<Transform>().Select(x => x.gameObject).ToArray())
			{
				if (child.name == SecId.GetHashCode().ToString())
				{			
					// Sector 
					foreach (Transform sccomponent in child.transform)
                    {
						string componentname = sccomponent.gameObject.name;
						
						// Floor Ceiling
						if (componentname == "Floor" || componentname == "Ceiling")
                        {
							DestroyImmediate(sccomponent.gameObject);
						}
						
						// Walls
                        else
                        {
							foreach (Transform wallcomponent in sccomponent.gameObject.transform)
                            {
								DestroyImmediate(wallcomponent.gameObject);
							}
						}
                    }

					DestroyImmediate(child);
				}
			}
		}

		/// <summary>
		/// Generate geometry for level.
		/// </summary>
		public async Task GenerateAsync(List<int> sectorFilters) {

			// Don't destroy geometry if you are filtering. Use cache. 
			//this.Clear();
			if (sectorFilters == null) this.Clear();

			Stopwatch watch = new Stopwatch();
			watch.Start();

			foreach ((Sector sectorInfo, int i) in LevelLoader.Instance.Level.Sectors.Select((x, i) => (x, i))) {

				// Filter pre-built geometry
				if (sectorFilters.Count > 0 && !sectorFilters.Contains(i))
				{
					continue;
				}

				GameObject sector = new GameObject {
					name = LevelLoader.Instance.Level.Sectors.IndexOf(sectorInfo).ToString(),
					//name = sectorInfo.Name ?? LevelLoader.Instance.Level.Sectors.IndexOf(sectorInfo).ToString(),
					layer = LayerMask.NameToLayer("Geometry")
				};
				sector.transform.SetParent(this.transform);

				SectorRenderer renderer = sector.AddComponent<SectorRenderer>();
				await renderer.RenderAsync(sectorInfo);

				if (!this.showAllLayers) {
					sector.SetActive(this.layer == sectorInfo.Layer);
				}
			}

			watch.Stop();
			Debug.Log($"Level geometry generated in {watch.Elapsed}!");
		}

		/// <summary>
		/// Change visibility of sectors based on layer selection.
		/// </summary>
		public void RefreshVisiblity() {
			foreach (SectorRenderer renderer in this.GetComponentsInChildren<SectorRenderer>(true)) {
				if (this.showAllLayers) {
					renderer.gameObject.SetActive(true);
					continue;
				}

				renderer.gameObject.SetActive(renderer.Sector.Layer == this.layer);
			}
		}
	}
}
