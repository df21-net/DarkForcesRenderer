﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace MZZT.DataBinding {
	[RequireComponent(typeof(TMP_Dropdown))]
	public class DataboundTmpIntegerDropdown : DataboundUi<int> {
		protected TMP_Dropdown Dropdown => this.Selectable as TMP_Dropdown;

		private void Start() {
			this.Dropdown.onValueChanged.AddListener(value => this.OnUserEnteredValueChanged());
		}

		protected override int UserEnteredValue {
			get => this.Dropdown.value;
			set => this.Dropdown.value = value;
		}
	}
}
