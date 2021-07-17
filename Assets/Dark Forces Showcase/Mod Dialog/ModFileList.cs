using MZZT.DataBinding;
using System.IO;
using UnityEngine;

namespace MZZT.DarkForces.Showcase {
	public class ModFile {
		public string FilePath;
		public string Overrides;

		public string DisplayName => Path.GetFileName(this.FilePath);
		public string AutoOverrides {
			get {
				if (this.FilePath == null) {
					return null;
				}
				switch (Path.GetExtension(this.FilePath).ToUpper()) {
					case ".GOB":
						return "Any";
					case ".LFD":
						return null;
					default:
						return this.DisplayName;
				}
			}
		}

		public string IconGlyph {
			get {
				if (this.FilePath == null) {
					return null;
				}
				switch (Path.GetExtension(this.FilePath).ToUpper()) {
					case ".GOB":
					case ".LFD":
						return "\uf1c4";
					default:
						return "\ue24d";
				}
			}
		}
	}

	public class ModFileList : DataboundList<ModFile> {
		[SerializeField]
		private GameObject headerSpacer = null;

		public override void Insert(int index, ModFile item) {
			base.Insert(index, item);

			this.OnUpdateScrollbar();
		}

		public override void Clear() {
			base.Clear();

			this.OnUpdateScrollbar();
		}

		public override void RemoveAt(int index) {
			base.RemoveAt(index);

			this.OnUpdateScrollbar();
		}

		private void OnUpdateScrollbar() {
			Canvas.ForceUpdateCanvases();

			this.headerSpacer.SetActive(((RectTransform)this.transform).rect.height > ((RectTransform)this.transform.parent).rect.height);
		}
	}
}
