﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

namespace MZZT.DataBinding {
	[RequireComponent(typeof(TMP_Dropdown))]
	public class DataboundTmpEnumDropdown : DataboundUi<int> {
		protected TMP_Dropdown Dropdown => this.Selectable as TMP_Dropdown;

		private int[] indexToEnumValue;
		private Dictionary<int, int> enumToIndexValue;

		private void Init() {
			if (this.enumToIndexValue != null) {
				return;
			}

			Type type = this.MemberType;
			Assert.IsTrue(type.IsEnum);

			(TMP_Dropdown.OptionData option, int value)[] values = Enum.GetValues(type).Cast<int>().Select(value => {
				string name = Enum.GetName(type, value);
				FieldInfo field = type.GetField(name);
				name = field.GetCustomAttribute<XmlEnumAttribute>()?.Name ?? name;
				name = field.GetCustomAttribute<DataMemberAttribute>()?.Name ?? name;
				name = field.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? name;

				return (new TMP_Dropdown.OptionData(name), value);
			}).ToArray();
			this.indexToEnumValue = values.Select(x => x.value).ToArray();
			this.enumToIndexValue = this.indexToEnumValue.Select((value, index) => (value, index)).ToDictionary(x => x.value, x => x.index);

			this.Dropdown.ClearOptions();
			this.Dropdown.AddOptions(values.Select(x => x.option).ToList());

			this.Dropdown.onValueChanged.AddListener(value => this.OnUserEnteredValueChanged());
		}

		private void Start() => this.Init();

		protected override int UserEnteredValue {
			get {
				this.Init();
				return this.indexToEnumValue[this.Dropdown.value];
			}
			set {
				this.Init();
				this.Dropdown.value = this.enumToIndexValue[value];
			}
		}
	}
}
