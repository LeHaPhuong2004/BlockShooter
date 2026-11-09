using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

using System.Collections.Generic;
using UnityEngine.UI;

using TMPro;

using System.Globalization;



public class GameController : MonoBehaviour {
	public static GameController staticScript;

	

	private static readonly NumberFormatInfo dotSeparatorFormat = new NumberFormatInfo
    {
        NumberGroupSeparator = "."
    };

	void Awake(){
		staticScript = this;
		
	}

	private void OnRectTransformDimensionsChange()
	{
		Vector2 currentScreenSize = PortraitDisplay.ContentPixelSize;

		PortraitDisplay.ConfigureCanvas(GetComponent<Canvas>());

		if (GameMaster.staticScript != null && currentScreenSize != GameMaster.staticScript.LastScreenSize)
		{
			GameMaster.staticScript.LastScreenSize = currentScreenSize;

		}
	}



	// Use this for initialization
	void Start () {
		Application.targetFrameRate = 60;
		Time.timeScale = 1;

		if (GameMaster.HaveNoAds == 0)
		{
			GameMaster.TryRequestAdmob();
		}

		GameMaster.staticScript.PreloadRewardAd();

		GameMaster.LogEventAction("Play_Challenge_Mode");
	}

	// Update is called once per frame
	void Update () {
		
	}


	
	private void OnDestroy()
	{
		staticScript = null;
	}
}

