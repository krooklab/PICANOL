﻿//-----------------------------------------------------------------------
// <copyright file="PointToPointGUIController.cs" company="Google">
//
// Copyright 2016 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using Tango;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading;

/// <summary>
/// GUI controller to show distance data.
/// </summary>
public class MultPointToPointGUIController : MonoBehaviour, ITangoDepth //MonoBehaviour
{
	// Constant values for overlay.
	public const float UI_LABEL_START_X = 15.0f;
	public const float UI_LABEL_START_Y = 40.0f;
	public const float UI_LABEL_SIZE_X = 1920.0f;
	public const float UI_LABEL_SIZE_Y = 100.0f;

	/// <summary>
	/// The point cloud object in the scene.
	/// </summary>
	public TangoPointCloud m_pointCloud;

	/// <summary>
	/// The line renderer to draw a line between two points.
	/// </summary>
	public LineRenderer m_lineRenderer;

	/// <summary>
	/// The scene's Tango application.
	/// </summary>
	private TangoApplication m_tangoApplication;

	/// <summary>
	/// If set, then the depth camera is on and we are waiting for the next
	/// depth update.
	/// </summary>
	private bool m_waitingForDepth;

	/// <summary>
	/// array to store touches to be able to create area of interest
	/// </summary>
	private Vector3[] points;
	private Vector3[] positionsOfPoints;
	private GameObject[] tempPoints;
	private List<Vector3> Line;

	/// <summary>
	///  counter for amount of dots
	/// </summary>
	private int m_i;
	/// <summary>
	/// The dot marker object for the VISIBLE points.
	/// </summary>
	public GameObject DotMarker;
	/// <summary>
	/// If wanted (for visual feedback if algorithm is functioning properly)
	/// </summary>
	public GameObject DotMarker2;
	/// <summary>
	/// Array to keep track of the invisble gameobjects 
	/// </summary>
	private GameObject[] DotMarkerInvisible;


	private Rect buttonRect, buttonRect2, buttonRect3, screenOverlay;
	private bool shot_taken;
	private string Path_Name;
	private Texture2D Screenshot;
	private string Screen_Shot_File_Name;
	private bool mode2D;
	//	public Texture markerTex;
	private int pointIndex;
	/// <summary>
	/// Amount of invisble markers to place ==> higher is preciser yet heavier? ==> BENCHMARK!
	/// </summary>
	private const int GRID_SIZE = 144;
	/// <summary>
	/// Array to keep locations of invisible markers
	/// </summary>
	private Vector2[] GridPosition;
	/// <summary>
	/// The margin for the grid when checking if tapped nearby a certain point.
	/// </summary>
	private const int margin = 10;
	/// <summary>
	/// Start this instance.
	/// </summary>
	private string text;
	private bool ScreenshotReady;
	private bool circle;
	private List<Vector2> lineList;

	public void Start ()
	{
		GUI.color = Color.black;
		m_tangoApplication = FindObjectOfType<TangoApplication> ();
		m_tangoApplication.Register (this);
		points = new Vector3[4];
		m_i = 0;
		Line = new List<Vector3> ();
		shot_taken = false;
		Screen_Shot_File_Name = "test.png";
		// If screenshot previously existed, remove it
		Path_Name = System.IO.Path.Combine (Application.persistentDataPath, Screen_Shot_File_Name);
		if (System.IO.File.Exists (Path_Name)) {
			System.IO.File.Delete (Path_Name);
		}
		mode2D = false; 
		points = new Vector3[GRID_SIZE];
		tempPoints = new GameObject[4];
		DotMarkerInvisible = new GameObject[GRID_SIZE];
		GridPosition = new Vector2[GRID_SIZE];
		buttonRect = new Rect (UI_LABEL_START_X,
			UI_LABEL_START_Y,
			300,
			UI_LABEL_SIZE_Y);
		buttonRect2 = new Rect (UI_LABEL_START_X,
			UI_LABEL_START_Y * 5,
			300,
			UI_LABEL_SIZE_Y);
		buttonRect3 = new Rect (UI_LABEL_START_X,
			UI_LABEL_START_Y * 9,
			300,
			UI_LABEL_SIZE_Y);
		screenOverlay = new Rect (0, 0, Screen.width, Screen.height);
		ScreenshotReady = false;
		// keep track of positions of screen to place markers if necessary (rectangle option)
		GridCalculations ();

		lineList = new List<Vector2> ();
		circle = false; 
	}

	/// <summary>
	/// Unity destroy function.
	/// </summary>
	public void OnDestroy ()
	{
		//remove screencaps
		Path_Name = System.IO.Path.Combine (Application.persistentDataPath, Screen_Shot_File_Name);
		if (System.IO.File.Exists (Path_Name)) {
			System.IO.File.Delete (Path_Name);
		}
		//unregister tango app
		m_tangoApplication.Unregister (this);
	}

	/// <summary>
	/// Update this instance.
	/// </summary>
	public void Update ()
	{
		///////////////////////
		Vector2 tmp;
		tmp = Input.mousePosition;
		tmp.y = Screen.height - tmp.y;

		if (Input.GetMouseButtonDown (0)) {
			if (!circle) {
				//if (ScreenshotReady) {
				StartCoroutine (_WaitForDepth (Input.mousePosition));
				//} 
			} else {
				//lineList.Clear ();
				lineList.Add (tmp);
				//Line.Clear ();
				// start new instance of LineList & Line, get a new LineRenderer
			}
		}

		if (Input.GetMouseButtonUp (0)) {
			if (circle) {
				StartCoroutine (_WaitForDepthCircle (Input.mousePosition));
			} else {
				// do nothing
			}
		}
		if (Input.GetMouseButton (0)) {
			if (circle) {
				lineList.Add (tmp);
				StartCoroutine (_WaitForDepthCircle (Input.mousePosition));
			} else {
				// do nothing
			}
		}
		if (Input.GetKey (KeyCode.Escape)) {
			// This is a fix for a lifecycle issue where calling
			// Application.Quit() here, and restarting the application
			// immediately results in a deadlocked app.
			AndroidHelper.AndroidQuit ();
		}
	}

	/// <summary>
	/// Display simple GUI.
	/// </summary>
	public void OnGUI ()
	{
		text = "Pic not taken yet";
		if (m_tangoApplication.HasRequestedPermissions ()) {
			if (!circle) {
				GUI.color = Color.white;
				if (shot_taken) {
					//Color colPreviousGUIColor = GUI.color;
					//GUI.color = new Color (colPreviousGUIColor.r, colPreviousGUIColor.g, colPreviousGUIColor.b, 1f);

					//GUI.DrawTexture (screenOverlay, Screenshot2);
					GUI.DrawTexture (screenOverlay, Screenshot);

					//GUI.color = colPreviousGUIColor;
					//text = "screenshot on! "; //+ Path_Name.ToString ()
				}//else text = "screenshot off";

				#pragma warning disable 618
				if (GUI.Button (buttonRect, "<size=25>" + "Circle?" + "</size>")) {
					// Function to clear the points entered (actually reposition the index)
					circle = true;
				}
				if (GUI.Button (buttonRect2, "<size=25>" + "ScreenCap" + "</size>")) {
					// Function to clear the points entered (actually reposition the index)
					screenCap ();
				}
				if (GUI.Button (buttonRect3, "<size=20>" + "new screencap" + "</size>")) {
					ClearPoints ();
					newScreenCap ();
				}
//			if (mode2D) { 
//				//array list with vector2 coords to draw temp dots
//				//GUI.DrawTextureWithTexCoords(new Rect(0,0,touchPositions[m_i].x,touchPositions[m_i].y),markerTex, new Rect(0,0,10f,10f));
//				GUI.DrawTexture (new Rect (touchPositions [m_i - 1], new Vector2 (10f, 10f)), markerTex);
//			}
				#pragma warning restore 618

				GUI.Label (new Rect (500.0f,
					UI_LABEL_START_Y,
					500.0f,
					200.0f),
					"<size=25>" + text + "</size>");
			} else {
				if (GUI.Button (buttonRect, "<size=25>" + "rectangle?" + "</size>")) {
					circle = false;
				}
				if (GUI.Button (buttonRect2, "<size=25>" + "Clear" + "</size>")) {
					// Function to clear the points entered (actually reposition the index)
					ClearPoints ();
				}
			}
		}

	}

	/// <summary>
	/// This is called each time new depth data is available.
	/// 
	/// On the Tango tablet, the depth callback occurs at 5 Hz.
	/// </summary>
	/// <param name="tangoDepth">Tango depth.</param>
	public void OnTangoDepthAvailable (TangoUnityDepth tangoDepth)
	{
		// Don't handle depth here because the PointCloud may not have been
		// updated yet. Just tell the coroutine it can continue.
		m_waitingForDepth = false;
	}

	/// <summary>
	/// This is called when successfully connected to Tango service.
	/// </summary>
	public void OnTangoServiceConnected ()
	{
		m_tangoApplication.SetDepthCameraRate (
			TangoEnums.TangoDepthCameraRate.DISABLED);
	}

	/// <summary>
	/// This is called when disconnected from the Tango service.
	/// </summary>
	public void OnTangoServiceDisconnected ()
	{
	}

	/// <summary>
	/// Wait for the next depth update, then find the nearest point in the point
	/// cloud.
	/// </summary>
	/// <param name="touchPosition">Touch position on the screen.</param>
	/// <returns>Coroutine IEnumerator.</returns>
	private IEnumerator _WaitForDepthCircle (Vector2 touchPosition)
	{
		
		m_waitingForDepth = true;

		// Turn on the camera and wait for a single depth update
		m_tangoApplication.SetDepthCameraRate (
			TangoEnums.TangoDepthCameraRate.MAXIMUM);
		while (m_waitingForDepth) {
			yield return null;
		}

		m_tangoApplication.SetDepthCameraRate (TangoEnums.TangoDepthCameraRate.DISABLED);
		Camera cam = Camera.main;
		pointIndex = m_pointCloud.FindClosestPoint (cam, touchPosition, 10);
		Vector3 lastPoint = m_pointCloud.m_points [pointIndex];
		if (pointIndex > -1) {
			UpdateCircle (lastPoint);
		}
	}

	void UpdateCircle (Vector3 lastPoint)
	{
		//enable linerenderer
		m_lineRenderer.enabled = true;
		Line.Add (lastPoint);
		positionsOfPoints = Line.ToArray ();
		m_lineRenderer.numPositions = positionsOfPoints.Length; // add this
		m_lineRenderer.SetPositions (positionsOfPoints);
	}

	/// <summary>
	/// Wait for the next depth update, then find the nearest point in the point
	/// cloud.
	/// </summary>
	/// <param name="touchPosition">Touch position on the screen.</param>
	/// <returns>Coroutine IEnumerator.</returns>
	private IEnumerator _WaitForDepth (Vector2 touchPosition)
	{
		// if max dots placed don't place markers or wait for depth
		if (m_i >= 4) {
			yield break;
		}
		m_waitingForDepth = true;

		// Turn on the camera and wait for a single depth update
		m_tangoApplication.SetDepthCameraRate (
			TangoEnums.TangoDepthCameraRate.MAXIMUM);
		while (m_waitingForDepth) {
			yield return null;
		}

		m_tangoApplication.SetDepthCameraRate (
			TangoEnums.TangoDepthCameraRate.DISABLED);
		int distance = 0;
		distance = (int)Math.Sqrt ((float)Math.Pow ((float)Screen.width / 8f, 2f) + (float)Math.Pow ((float)Screen.height / 8f, 2f));// ==> teveel overlap met schuine!
		//With screenoverlay
		if (shot_taken) {
			//we take the smallest distance between points, to have no overlap when searching for the correct point in the invisble grid.
			//		if (Screen.height > Screen.width)
			//			distance = (int)Screen.width / 8 - margin;
			//		else
			//			distance = (int)Screen.height / 8 - margin;
			pointIndex = FindClosestPointGrid (touchPosition, distance);
			if (pointIndex > -1) {
				enableDot (DotMarkerInvisible [pointIndex].transform.position);
				if (m_i < 3) {
					m_i++;
				} else {
					mode2D = false;
					UpdateLine ();
					m_i++;
				}
			}
		}
		//WITHOUT screenoverlay (through camera)
		else {
			Camera cam = Camera.main;
			pointIndex = m_pointCloud.FindClosestPoint (cam, touchPosition, 10);
			if (pointIndex > -1) {
				enableDot (m_pointCloud.m_points [pointIndex]);
				if (m_i < 3) {
					m_i++;
				} else {
					mode2D = false;
					UpdateLine ();
					m_i++;
				}
			}
		}

	}

	void UpdateLine ()
	{
		//enable linerenderer
		m_lineRenderer.enabled = true;
		GameObject[] DotList = GameObject.FindGameObjectsWithTag ("marker");
		foreach (GameObject t in DotList) {
			Line.Add (t.transform.position);
		}
		Line.Add (DotList [0].transform.position);
		positionsOfPoints = Line.ToArray ();
		m_lineRenderer.numPositions = positionsOfPoints.Length; // add this
		m_lineRenderer.SetPositions (positionsOfPoints);
	}

	/// <summary>
	/// Places the markerdots.
	/// </summary>
	private GameObject showDots (GameObject marker, string strmarker, Vector3 MarkerPlace)
	{
		tempPoints [m_i] = (GameObject)Instantiate (marker);
		tempPoints [m_i].transform.position = MarkerPlace;
		tempPoints [m_i].tag = strmarker;
		return tempPoints [m_i];
	}

	/// <summary>
	/// Enables the dots which are being approx tapped .
	/// </summary>
	/// <returns>The dots.</returns>
	private GameObject enableDot (Vector3 pos)
	{
		GameObject test = (GameObject)Instantiate (DotMarker);
		test.transform.position = pos;
		test.tag = "marker";
		return test;
	}

	/// <summary>
	/// Clears the Marker dots & the line renderer.
	/// </summary>
	void ClearPoints ()
	{
		Line.Clear ();
		m_lineRenderer.enabled = false;
		m_i = 0;
		// remove all game objects based on marker tag
		GameObject[] enemies = GameObject.FindGameObjectsWithTag ("marker");
		foreach (GameObject enemy in enemies) {
			GameObject.Destroy (enemy);
		}
		enemies = GameObject.FindGameObjectsWithTag ("marker_invisible");
		foreach (GameObject enemy in enemies) {
			GameObject.Destroy (enemy);
		}
		//clear points array (CORRECT WAY?)
		tempPoints = new GameObject[4];
	}

	void screenCap ()
	{			
		// We turn the screenshot on or off
		//get rid of screenshot overlay by falsifying the screenshotBoolean
		Path_Name = System.IO.Path.Combine (Application.persistentDataPath, Screen_Shot_File_Name);
		ScreenshotReady = false;
		//Application.CaptureScreenshot (Screen_Shot_File_Name);
		//StartCoroutine(screenShotCoroutine());
		if (System.IO.File.Exists (Path_Name)) {
			byte[] Bytes_File = System.IO.File.ReadAllBytes (Path_Name);
			Screenshot = new Texture2D (0, 0, TextureFormat.RGBA32, false);
			ScreenshotReady = Screenshot.LoadImage (Bytes_File);

		}	
//		while (!ScreenshotReady) {
//		}
		shot_taken = !shot_taken;

	}

	void newScreenCap ()
	{
		Path_Name = System.IO.Path.Combine (Application.persistentDataPath, Screen_Shot_File_Name);
		if (System.IO.File.Exists (Path_Name)) {
			System.IO.File.Delete (Path_Name);
		}

		ScreenshotReady = false;
		Application.CaptureScreenshot (Screen_Shot_File_Name);
		//////////////////////////////////////////////////////////////////////////////////////////////////////////
		FillGrid ();
		if (System.IO.File.Exists (Path_Name)) {
			byte[] Bytes_File = System.IO.File.ReadAllBytes (Path_Name);
			Screenshot = new Texture2D (0, 0, TextureFormat.RGBA32, false);
			ScreenshotReady = Screenshot.LoadImage (Bytes_File);

		}	
	}

	/// @endcond
	/// <summary>
	/// Finds the closest point from a Grid to a position on screen.
	/// 
	/// This function is slow, as it looks at every single point in the absolute Grid.
	/// Avoid calling this more than once a frame.
	/// </summary>
	/// <returns>The index of the closest point, or -1 if not found.</returns>
	/// <param name="cam">The current camera.</param>
	/// <param name="pos">Position on screen (in pixels).</param>
	/// <param name="maxDist">The maximum pixel distance to allow.</param>
	public int FindClosestPointGrid (Vector2 pos, int maxDist)
	{
		int bestIndex = -1;
		float bestDistSqr = 0;

		for (int i = 0; i < GRID_SIZE; i++) {
			
			Vector2 screenPos = GridPosition [i];
			float distSqr = Vector2.SqrMagnitude (screenPos - pos);
			if (distSqr > maxDist * maxDist) {
				continue;
			}

			if (bestIndex == -1 || distSqr < bestDistSqr) {
				bestIndex = i;
				bestDistSqr = distSqr;
			}
		}

		return bestIndex;
	}

	/// <summary>
	/// Fills the grid.
	/// GameObject[] DotMarkerInvisible keeps track of where in absolute coords we've placed invisible markers
	/// </summary>
	public void FillGrid ()
	{
		Camera cam = Camera.main;
		//place 1 marker in center of screen
		//
		// 2 5 8
		// 1 4 7
		// 0 3 6

		for (int k = 0; k < GRID_SIZE; k++) {
			int pointIndexGrid = m_pointCloud.FindClosestPoint (cam, GridPosition [k], 10);
			if (pointIndexGrid > -1) {
				Vector3 recent_point_Grid = m_pointCloud.m_points [pointIndexGrid];
				points [k] = recent_point_Grid;
				DotMarkerInvisible [k] = showDots (DotMarker2, "marker_invisible", points [k]);
			}
		}
	}

	private void GridCalculations ()
	{
		int loop = (int)Math.Sqrt (GRID_SIZE);
		float screenDiv = (float)(1 / ((float)loop + 1));
		int k = 0;
		for (int i = 0; i <= loop - 1; i++) {
			for (int j = 0; j <= loop - 1; j++) {
				GridPosition [k] = new Vector2 (Screen.width * screenDiv + Screen.width * i * screenDiv, Screen.height * screenDiv + Screen.height * j * screenDiv);
				k++;
			}
		}
	}

}

