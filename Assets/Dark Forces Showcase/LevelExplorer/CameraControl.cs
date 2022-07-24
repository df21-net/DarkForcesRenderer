using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using MZZT.DarkForces.FileFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MZZT.DarkForces;
using MZZT.DarkForces.Showcase;

namespace MZZT {

	
	public class CameraControl : MonoBehaviour {
		[SerializeField]
		private Vector2 lookSensitivity = Vector2.one;
		public Vector2 LookSensitivity {
			get => this.lookSensitivity;
			set => this.lookSensitivity = value;
		}
		[SerializeField]
		private Vector2 moveSensitivity = Vector2.one;
		public Vector2 MoveSensitivity {
			get => this.moveSensitivity;
			set => this.moveSensitivity = value;
		}
		[SerializeField]
		private float runningMultiplier = 2;
		public float RunningMultiplier {
			get => this.runningMultiplier;
			set => this.runningMultiplier = value;
		}
		[SerializeField]
		private float upDownSensitivity = 1;
		public float UpDownSensitivity {
			get => this.upDownSensitivity;
			set => this.upDownSensitivity = value;
		}
		[SerializeField]
		private Vector2 yAngleClamp = new Vector2(-60, 60);
		[SerializeField]
		private bool invertY = true;

		[SerializeField]
		public bool enableCameraPos = false;


		public bool InvertY {
			get => this.invertY;
			set => this.invertY = value;
		}

		public bool CamPosition
		{
			get => this.enableCameraPos;
			set => this.enableCameraPos = value;
		}

		private Vector2 lookDelta;
		public void OnLook(InputAction.CallbackContext context) {
			this.lookDelta = context.ReadValue<Vector2>();
		}

		private Vector2 moveDelta;
		public void OnMove(InputAction.CallbackContext context) {
			this.moveDelta = context.ReadValue<Vector2>();
		}

		private float upDownDelta;
		public void OnUpDown(InputAction.CallbackContext context) {
			this.upDownDelta = context.ReadValue<float>();
		}

		private bool running;
		public void OnRun(InputAction.CallbackContext context) {
			this.running = context.ReadValueAsButton();
		}

		private bool panCursor = false;

		void OnGUI()
		{
			if (this.enableCameraPos == true)
			{
				GUIStyle PosStyle = new GUIStyle();
				PosStyle.normal.textColor = Color.red;
				PosStyle.fontSize = 30;
				Vector3 origpos = Camera.main.transform.position;
				origpos.x = origpos.x / LevelGeometryGenerator.GEOMETRY_SCALE;
				origpos.y = origpos.y / LevelGeometryGenerator.GEOMETRY_SCALE;
				origpos.z = origpos.z / LevelGeometryGenerator.GEOMETRY_SCALE;
				GUI.Label(new Rect(10, 10, 100, 20), string.Format("POS {0}",
						  origpos), PosStyle);
			}
		}
		

		private void Look(Vector2 value) {
			Transform camera = Camera.main.transform;
			camera.Rotate(Vector3.up, value.x * this.lookSensitivity.x, Space.World);

			float yAngle = (camera.rotation.eulerAngles.x + 180) % 360 - 180;
			float yDelta = (-value.y * this.lookSensitivity.y * (this.invertY ? -1 : 1) + 180) % 360 - 180;
			float yFinalAngle = Mathf.Clamp(yAngle + yDelta, this.yAngleClamp.x, this.yAngleClamp.y);

			Vector3 rotation = camera.rotation.eulerAngles;
			rotation.x = yFinalAngle;
			camera.rotation = Quaternion.Euler(rotation);
			//camera.Rotate(Vector3.right, yDelta + (yFinalAngle - yAngle), Space.Self);
		}

		private void Move(Vector2 value) {
			Transform camera = Camera.main.transform;
			Vector3 direction = (this.running ? this.runningMultiplier : 1) * Time.deltaTime *
				((this.moveSensitivity.x *  value.y * camera.forward) +
				(this.moveSensitivity.y * value.x * camera.right));
			camera.position += direction;
			//camera.GetComponent<Rigidbody>().AddForce(direction);
		}

		private void UpDown(float value) {
			Transform camera = Camera.main.transform;
			Vector3 direction = 
				(this.running ? this.runningMultiplier : 1) * this.upDownSensitivity * Time.deltaTime * value *
				Vector3.down;
			camera.position += direction;
			//camera.GetComponent<Rigidbody>().AddForce(direction);
		}

		private void Update()
		{
			// Set Panning cursor.
			PanKeyPressed();

			if (!panCursor)
			{
				Cursor.lockState = Time.timeScale > 0 && Application.isFocused ? CursorLockMode.Locked : CursorLockMode.None;
				Cursor.visible = Time.timeScale <= 0 || !Application.isFocused;
			}


			//Debug.Log(string.Format("Lock state = {0}", Cursor.lockState));

			if (!Cursor.visible)
			{
				if (Time.timeScale > 0)
				{
					if (this.moveDelta != Vector2.zero)
					{
						this.Move(this.moveDelta);
					}
					if (this.lookDelta != Vector2.zero)
					{
						this.Look(this.lookDelta);
					}
					if (this.upDownDelta != 0)
					{
						this.UpDown(this.upDownDelta);
					}
				}
			}		

		}

		void OnApplicationFocus()
        {
			panCursor = false;
		}

		// Special function - when you hold ALT you can now pan your mouse outside the renderer engine. 
		void PanKeyPressed()
		{
			Keyboard kboard = Keyboard.current;
			
			if (kboard[Key.LeftAlt].wasPressedThisFrame)
            {
				Texture2D texture = MZZT.DarkForces.Showcase.Menu.cursorTexture;
				Cursor.SetCursor(texture, Vector2.zero, CursorMode.Auto);
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
				panCursor = true;
			}

			if (kboard[Key.LeftAlt].wasReleasedThisFrame)
			{
				panCursor = false;
			}
		}
	}
}
