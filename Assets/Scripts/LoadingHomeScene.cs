using UnityEngine;
using System.Collections;
using UnityEngine.UI;

using TMPro;


using UnityEngine.SceneManagement;

public class LoadingHomeScene : MonoBehaviour {
	public TextMeshProUGUI textLoading;
	public RectTransform imageProgressBar;
	AsyncOperation asynLoadLevel = null;
	float realLoading = 0f;
	float loadingComplete = 0f;
	float smoothLoading = 0f;

	private void OnEnable()
	{
		Localize.LanguageChanged += RefreshLoadingText;
		RefreshLoadingText();
	}

	private void OnDisable() => Localize.LanguageChanged -= RefreshLoadingText;

	private void RefreshLoadingText()
	{
		if (textLoading != null)
			textLoading.text = Localize.Format("ĐANG TẢI {0}%", "LOADING {0}%", Mathf.RoundToInt(loadingComplete * 100f));
	}



	// Use this for initialization
	void Start () {
		Application.targetFrameRate = 60;

		realLoading = 0f;
		loadingComplete = 0f;
		smoothLoading = 0f;

		this.StartCoroutine(IELoadHomeScene());
	}

	// Update is called once per frame
	void Update () {
		smoothLoading += 1.0f * Time.deltaTime;
		if (asynLoadLevel != null) {
			realLoading = asynLoadLevel.progress;// * 1.0f/ 0.9f;
			//////Debug.Log ("realLoading " + realLoading);
		} 
		if (realLoading < 0.9f) {
			loadingComplete = Mathf.Min (realLoading, smoothLoading);
		} else {
			loadingComplete = smoothLoading;
		}

		smoothLoading = loadingComplete;

		if (loadingComplete >= 1.0f) {
			loadingComplete = 1.0f;
		}

		//////Debug.Log ("loadingComplete " + loadingComplete);
		imageProgressBar.sizeDelta = new Vector2(Mathf.Max(32f, loadingComplete * 270f), 30f) ;

		RefreshLoadingText();


	
		if (loadingComplete >= 1f) {
			SwitchScene ();
		}
	}

	IEnumerator IELoadHomeScene()
	{
		//asynLoadLevel = Application.LoadLevelAsync (1);
        asynLoadLevel = SceneManager.LoadSceneAsync(1);
		asynLoadLevel.allowSceneActivation = false;
		yield return asynLoadLevel;
		realLoading = 1f;
	}

	private void SwitchScene()
	{
		if (asynLoadLevel != null) {
			asynLoadLevel.allowSceneActivation = true;
			asynLoadLevel = null;
			//Destroy (this.gameObject);
		}
	}

}
