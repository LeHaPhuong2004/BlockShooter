using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using TMPro;

using System.Globalization;
using System.Reflection;

#if UNITY_IPHONE
using UnityEngine.iOS;
#elif UNITY_ANDROID
     
#else

#endif
public class HomeController : MonoBehaviour {

	public static HomeController staticScript;
	// Use this for initialization
	public GameObject PanelLoading;

	public GameObject PanelWaitPurchaseUI;

	public GameObject PanelDailyReward;

	public GameObject PanelShop;


	public GameObject PanelItem;

	public GameObject PanelConfirmPlay;

	public GameObject PanelNotEnoughGold;
	public GameObject PanelRewardNotLoaded;

	//public GameObject BtnRemoveAds;
	//public TextMeshProUGUI  txtRemoveAdsPrice;

	public GameObject PanelSetting;

	public TMP_Text priceRemoveAds;
	public TMP_Text pricePack1;
    public TMP_Text pricePack2;
    public TMP_Text pricePack3;
    public TMP_Text pricePack4;
    public TMP_Text pricePack5;
    public TMP_Text pricePack6;

	public Button claimButton1;
    public Button claimButton2;
    public Button claimButton3;
    public Button claimButton4;
    public Button claimButton5;
    public GameObject checkIcon1;
    public GameObject checkIcon2;
    public GameObject checkIcon3;
    public GameObject checkIcon4;
    public GameObject checkIcon5;
    //public GameObject SettingSoundOn;
    //public GameObject SettingSoundOff;

    //public GameObject SettingMusicOn;
    //public GameObject SettingMusicOff;

    //public GameObject SettingJoystickOn;
    //public GameObject SettingJoystickOff;

    //public TextMeshProUGUI txtUserGold;

    //public TextMeshProUGUI  txtBestScore;
    //public TextMeshProUGUI  txtPuzzleLevelCleared;
    //public TextMeshProUGUI  txtArcadeLevelCleared;

    //public Animator settingAnimator;


    //public RectTransform MainCanvas;
    //   public GameObject prefabCoinMove;

    //   public RectTransform rectIconCoin;

    ////For shop
    //public TextMeshProUGUI txtPricePack1;
    //public TextMeshProUGUI txtPricePack2;
    //public TextMeshProUGUI txtPricePack3;
    //public TextMeshProUGUI txtPricePack4;
    //public TextMeshProUGUI txtPricePack5;
    //public TextMeshProUGUI txtPricePack6;

    //public TextMeshProUGUI txtTimeCoundownAds;
    //public Button btnWatchAds;
    //public GameObject btnWatchAdsEffect;
    //public GameObject btnWatchAdsText;

    //public TextMeshProUGUI txtItem1Number;
    //public TextMeshProUGUI txtItem2Number;
    //public TextMeshProUGUI txtItem3Number;
    //public TextMeshProUGUI txtItem4Number;

    //private int remainTimeAds = 900;

    void Awake()
	{
		staticScript = this;
		float ratio = PortraitDisplay.ContentAspect;

		if (ratio < 0.5f) {
			//only for iPhoneX
			this.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
		}
		else
		{
			this.GetComponent<CanvasScaler>().matchWidthOrHeight = 1.0f;
		}
	}
	void Start () {
		Application.targetFrameRate = 60;
		Time.timeScale = 1;
		
	
		GameMaster.staticScript.PlayBackgroundSound();

		GameMaster.staticScript.PreloadRewardAd();

	

		if(GameMaster.IsFirstOpen == 0)
        {
            GameMaster.IsFirstOpen = 1;

            GameMaster.LogEventAction("A_FIRST_OPEN");
        }
		
		//GameMaster.ShowSmallBanner();

	}

	private string FormatScore(int score)
	{
		NumberFormatInfo dotSeparatorFormat = new NumberFormatInfo
		{
			NumberGroupSeparator = "."
		};
		
		if (score > 9999)
        {
 
            return score.ToString("#,##0", dotSeparatorFormat);
        }
        
        return score.ToString();
	}

	private void OnRectTransformDimensionsChange()
	{
		Vector2 currentScreenSize = PortraitDisplay.ContentPixelSize;

		float ratio = PortraitDisplay.ContentAspect;

		if (ratio < 0.5f) {
			//only for iPhoneX
			this.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
		}
		else
		{
			this.GetComponent<CanvasScaler>().matchWidthOrHeight = 1.0f;
		}

		if (GameMaster.staticScript != null && currentScreenSize != GameMaster.staticScript.LastScreenSize)
		{
			//GameMaster.staticScript.LastScreenSize = currentScreenSize;
		}
	}
	

	// Update is called once per frame
	void Update () {

	}

	public void PlayGame(){
		if (!HeartManager.CanPlay)
		{
			foreach (GameObject root in gameObject.scene.GetRootGameObjects())
			{
				HomeExperienceUI home = root.GetComponentInChildren<HomeExperienceUI>(true);
				if (home == null) continue;
				home.ShowOutOfHearts();
				break;
			}
			return;
		}
		if (AudioManager.Instance != null && AudioManager.Instance.buttonClick != null)
			AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
		else
			GameMaster.PlaySoundClick();

		SceneManager.LoadScene (2);
		
	}



	public void OnClickMoreApp(){
		//Open store tizen
		GameMaster.PlaySoundClick ();

#if UNITY_IPHONE
		string linkApp = "https://apps.apple.com/us/developer/mobisoft-technology-company-limited/id963051892";
#elif UNITY_ANDROID
		string linkApp = "https://play.google.com/store/apps/dev?id=4770807614687012751";
#else
		string linkApp = "https://apps.apple.com/us/developer/mobisoft-technology-company-limited/id963051892";
#endif
		GameMaster.LogEventAction("Click_More_App");
		Application.OpenURL (linkApp);
	}

	public void OnClickRateApp()
	{
		//Open store tizen
		GameMaster.PlaySoundClick();

		GameMaster.HaveShowReviewRate = 1;
#if UNITY_IPHONE
		string linkApp = "---";
#elif UNITY_ANDROID
		string linkApp = "---";
#else
		string linkApp = "---";
#endif
		GameMaster.LogEventAction("Click_Rate_App");
		Application.OpenURL(linkApp);
	}


	public void OnClickRanking(){
		GameMaster.PlaySoundClick ();
		GameMaster.LogEventAction("Click_Ranking");
		GameMaster.ShowLeaderBoard ();
	}

	private void CheckShowRateApp()
	{
#if UNITY_IPHONE
		//show rate app
		
		GameMaster.HaveShowReviewRate = 1;
		//RabaGames.RateInsideAppiOS.DisplayReviewDialog();

		bool popupShown = Device.RequestStoreReview();

		if (popupShown)
		{
			// The review popup was presented to the user, set "reviewRequested" to "true" to reflect that
			// Note: there's no way to check if the user actually gave a review for the app or cancelled the popup.
			//GameMaster.HaveShowReviewRate = 1;
		}
		else
		{
			// The review popup wasn't presented. Log a message and reset "reviewRequested" so you can revisit this in the future.
			Debug.Log("iOS version is too low or StoreKit framework was not linked.");
			//GameMaster.HaveShowReviewRate = 1;

		}

#elif UNITY_ANDROID
     
#else

#endif
	}

	public void OnClickRemoveAds()
	{
		GameMaster.PlaySoundClick();

		GameMaster.LogEventAction("Click_Remove_App");

		IAPManager.BuyNoADS();
	}

	public void OnClickWatchAdsFree()
    {
        GameMaster.PlaySoundClick();

		bool res = GameMaster.staticScript.ShowRewardedAd(4);

		if(res == false)
		{
			PanelRewardNotLoaded.SetActive(true);
		}
       
    }

	public void OnHandleWatchAdsGetFreeCoin()
	{

	}

	public void OnClickBuyPack1()
    {
        //GameMaster.PlaySoundClick();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        IAPManager.BuyCoinPack1Gold();
        GameMaster.LogEventAction("INAPP_BUY_PACK_1");
    }

    public void OnClickBuyPack2()
    {
        //GameMaster.PlaySoundClick();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        IAPManager.BuyCoinPack2Gold();
        GameMaster.LogEventAction("INAPP_BUY_PACK_2");
    }

    public void OnClickBuyPack3()
    {
        //GameMaster.PlaySoundClick();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        IAPManager.BuyCoinPack3Gold();
        GameMaster.LogEventAction("INAPP_BUY_PACK_3");
    }

	 public void OnClickBuyPack4()
    {
        //GameMaster.PlaySoundClick();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        IAPManager.BuyCoinPack4Gold();
        GameMaster.LogEventAction("INAPP_BUY_PACK_4");
    }

    public void OnClickBuyPack5()
    {
        //GameMaster.PlaySoundClick();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        IAPManager.BuyCoinPack5Gold();
        GameMaster.LogEventAction("INAPP_BUY_PACK_5");
    }

    public void OnClickBuyPack6()
    {
        //GameMaster.PlaySoundClick();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        IAPManager.BuyCoinPack6Gold();
        GameMaster.LogEventAction("INAPP_BUY_PACK_6");
    }

	public void OnClickRestorePurchase()
	{
		//GameMaster.PlaySoundClick();
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        IAPManager.AutoRestorePurchase();
	}

	public void OnPurchaseRemoveAdsSuccess()
    {
      

    }

    public void OnPurchasePack1Success()
    {
		AudioManager.Instance.PlaySFX(AudioManager.Instance.buySound);
		//GameMaster.staticScript.PlaySoundGetGold();
        ShowCoinMoveAnimtion(500, 1);
    }

    public void OnPurchasePack2Success()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buySound);
        //GameMaster.staticScript.PlaySoundGetGold();
        ShowCoinMoveAnimtion(1200, 1);
    }

    public void OnPurchasePack3Success()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buySound);
        //GameMaster.staticScript.PlaySoundGetGold();
        ShowCoinMoveAnimtion(2500, 1);
    }

	public void OnPurchasePack4Success()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buySound);
        //GameMaster.staticScript.PlaySoundGetGold();
        ShowCoinMoveAnimtion(6500, 1);
    }

	public void OnPurchasePack5Success()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buySound);
        //GameMaster.staticScript.PlaySoundGetGold();
        ShowCoinMoveAnimtion(14000, 1);
    }

	public void OnPurchasePack6Success()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buySound);
        //GameMaster.staticScript.PlaySoundGetGold();
        ShowCoinMoveAnimtion(30000, 1);
    }

	public void OnClickHideWaitPurchase()
	{

		GameMaster.PlaySoundClick();
		PanelWaitPurchaseUI.SetActive(false);
	}

    public static void HideButtonRemoveAds()
    {

    }

	public void OnClickShowSetting()
    {
		
	}

	public void OnClickHideSetting()
    {
		
    }
	public void UpdateIAPPrices(string removeAds, string pack1, string pack2, string pack3, string pack4, string pack5, string pack6)
	{
		if (priceRemoveAds != null) priceRemoveAds.text = removeAds;
		if (pricePack1 != null) pricePack1.text = pack1;
        if (pricePack2 != null) pricePack2.text = pack2;
        if (pricePack3 != null) pricePack3.text = pack3;
        if (pricePack4 != null) pricePack4.text = pack4;
        if (pricePack5 != null) pricePack5.text = pack5;
        if (pricePack6 != null) pricePack6.text = pack6;
    }
    public void CollectMissonReward1()
    {
        AddMissionGold(50);
        claimButton1.gameObject.SetActive(false);
        checkIcon1.gameObject.SetActive(false);
        AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollect);
    }

    public void CollectMissonReward2()
    {
        AddMissionGold(50);
        claimButton2.gameObject.SetActive(false);
        checkIcon2.gameObject.SetActive(false);
        AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollect);
    }

    public void CollectMissonReward3()
    {
        AddMissionGold(50);
        claimButton3.gameObject.SetActive(false);
        checkIcon3.gameObject.SetActive(false);
        AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollect);
    }

    public void CollectMissonReward4()
    {
        AddMissionGold(50);
        claimButton4.gameObject.SetActive(false);
        checkIcon4.gameObject.SetActive(false);
        AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollect);
    }

    public void CollectMissonReward5()
    {
        AddMissionGold(50);
        claimButton5.gameObject.SetActive(false);
        checkIcon5.gameObject.SetActive(false);
        AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollect);
    }

    //public void OnClickChangeToSoundOn()
    //   {
    //	GameMaster.PlaySoundClick();
    //	GameMaster.SoundOnOff = 1;
    //	SettingSoundOn.SetActive(true);
    //	SettingSoundOff.SetActive(false);

    //}

    //public void OnClickChangeToSoundOff()
    //{
    //	GameMaster.PlaySoundClick();
    //	GameMaster.SoundOnOff = 0;
    //	SettingSoundOn.SetActive(false);
    //	SettingSoundOff.SetActive(true);

    //}

    //public void OnClickChangeToMusicOn()
    //   {
    //	GameMaster.PlaySoundClick();
    //	GameMaster.MusicOnOff = 1;
    //	SettingMusicOn.SetActive(true);
    //	SettingMusicOff.SetActive(false);

    //}
    //public void OnClickChangeToMusicOff()
    //{
    //	GameMaster.PlaySoundClick();
    //	GameMaster.MusicOnOff = 0;
    //	SettingMusicOn.SetActive(false);
    //	SettingMusicOff.SetActive(true);

    //}

    //public void OnClickShowShop()
    //{
    //	GameMaster.PlaySoundClick();

    //	txtPricePack1.text = GameMaster.IapPricePack1;
    //	txtPricePack2.text = GameMaster.IapPricePack2;
    //	txtPricePack3.text = GameMaster.IapPricePack3;

    //	txtPricePack4.text = GameMaster.IapPricePack4;
    //	txtPricePack5.text = GameMaster.IapPricePack5;
    //	txtPricePack6.text = GameMaster.IapPricePack6;

    //	UpdateRemainAdsTime();
    //	ShowRemainAdsTime();
    //   	InvokeRepeating(nameof(ShowRemainAdsTime), 1f, 1f);

    //	PanelShop.SetActive(true);
    //}

    //public void OnClickShowShopNotEnough()
    //{
    //	GameMaster.PlaySoundClick();

    //	txtPricePack1.text = GameMaster.IapPricePack1;
    //	txtPricePack2.text = GameMaster.IapPricePack2;
    //	txtPricePack3.text = GameMaster.IapPricePack3;

    //	txtPricePack4.text = GameMaster.IapPricePack4;
    //	txtPricePack5.text = GameMaster.IapPricePack5;
    //	txtPricePack6.text = GameMaster.IapPricePack6;

    //	PanelNotEnoughGold.SetActive(false);
    //}

    //public void OnClickHideShop()
    //{
    //	GameMaster.PlaySoundClick();

    //	CancelInvoke(nameof(ShowRemainAdsTime));
    //	PanelShop.SetActive(false);
    //}



    public void OnClickShowItem()
	{
		GameMaster.PlaySoundClick();
		PanelItem.SetActive(true);
	}

	public void OnClickHideItem()
	{
		GameMaster.PlaySoundClick();
		PanelItem.SetActive(false);
	}

	 private void CheckShowDailyReward()
    {
        long fistTimeOfDay = (System.DateTime.Now.Date.Ticks / 10000000);

        if (fistTimeOfDay <= GameMaster.LastTimeCheckDailyReward + 100)
        {
            //checked in today
			Debug.Log("CheckShowDailyReward checked in today");
            return;
        }

        long fisrtTimeOfBeginDay = new System.DateTime(GameMaster.BeginTimePlayGame * 10000000).Date.Ticks / 10000000;

        long numberDay = (fistTimeOfDay - fisrtTimeOfBeginDay) / 86400; //day from 0 , 1, 2, 3, 4, 5, 6 
        numberDay += 1;

		Debug.Log("CheckShowDailyReward numberDay = " + numberDay);
        //numberDay from 1, 2, 3, 4, 5, 6, 7 ... 28
        if (numberDay > 30 || numberDay < 1)
        {
            //out of range to check
			Debug.Log("CheckShowDailyReward out of range to check");
            return;
        }


        if (numberDay == 1 && GameMaster.EnableDailyReward == 0)
        {
            //only show daily reward if user played in game
			Debug.Log("CheckShowDailyReward player not run");
            return;
        }

        GameMaster.LastTimeCheckDailyReward = fistTimeOfDay;

        PanelDailyReward.GetComponent<DailyRewardController>().FillAndShowForDate((int)numberDay);

        PanelDailyReward.SetActive(true);

    }

	public void DailyRewardClaimX2()
	{
		bool res = GameMaster.staticScript.ShowRewardedAd(3);

		if(res == false)
		{
			PanelRewardNotLoaded.SetActive(true);
		}
	}

	public void OnClickCloseRewardNotLoaded()
	{
		GameMaster.PlaySoundClick();
		PanelRewardNotLoaded.SetActive(false);
	}

	public void OnHandleDailyRewardClaimX2()
	{
		Time.timeScale = 1f;
		PanelDailyReward.GetComponent<DailyRewardController>().ProcessClaimX2();
	}

	//public void UpdateUserGold()
 //   {
 //       txtUserGold.text = FormatScore(GameMaster.UserGold);
 //   }

	 public void ShowCoinMoveAnimtion(int CoinNumber, int type)
    {
        //Debug.Log(rectWatchAdsInShop.localToWorldMatrix * rectWatchAdsInShop.anchoredPosition);

        GameMaster.UserCoinTmp = GameMaster.UserGold;

        CurrencyManager.AwardMoney(CoinNumber);

        //if(type == 2)
        //{
        //    UpdateUserGold();

        //}

        //if (CoinNumber <= 100)
        //      {
        //          int step = CoinNumber;

        //          StartCoroutine(DrawMoveCoin(step, 1, type));
        //      }
        //      else if (CoinNumber <= 2000)
        //      {
        //          int step = CoinNumber / 10;

        //          StartCoroutine(DrawMoveCoin(step, 10, type));
        //      }
        //      else if (CoinNumber <= 5000)
        //      {
        //          int step = CoinNumber / 20;

        //          StartCoroutine(DrawMoveCoin(step, 20, type));
        //      }
        //      else if (CoinNumber <= 10000)
        //      {
        //          int step = CoinNumber / 50;

        //          StartCoroutine(DrawMoveCoin(step, 50, type));
        //      }
        //      else
        //      {
        //          int step = CoinNumber / 100;

        //          StartCoroutine(DrawMoveCoin(step, 100, type));
        //      }

    }

	//private IEnumerator DrawMoveCoin(int number, int CoinValue, int type)
 //   {
 //       Vector2 start, end;

 //       float ratio = 1920f / Screen.height;  //1920f / Screen.width;

 //       float ratioH = (float)Screen.height / (float)Screen.width;

 //       float offsetTop = 0f;

 //       if(ratioH > 2.0f) {
 //           offsetTop = -50f;
 //       }


 //       //start = new Vector2(Screen.width * ratio / 2f, Screen.height * ratio / 2);

 //       //end = new Vector2(Screen.width * ratio - 330f, Screen.height * ratio - 120f + offsetTop);
	//	end = GetScreenCoordinates(rectIconCoin);
 //       Vector2 velocity;

 //       while (number > 0)
 //       {
 //           GameObject Coin = Instantiate(prefabCoinMove, Vector3.zero, Quaternion.identity, MainCanvas) as GameObject;
            
 //           start = new Vector2(Random.Range(Screen.width * ratio / 4f, 3f * Screen.width * ratio / 4f), Random.Range(Screen.height * ratio / 4f, 2f * Screen.height * ratio / 3f));
 //           velocity = new Vector2(Random.Range(-1000f, 1000f), Random.Range(-500f, -1000f));
 //           Coin.GetComponent<MoveCoinOnUI>().StartMove(this.gameObject, start, end, CoinValue, velocity, Random.Range(1.5f, 4.0f), type);
           
 //           Coin.SetActive(true);
 //           number--;

 //           yield return new WaitForEndOfFrame();
 //       }


 //   }

	//private Vector2 GetScreenCoordinates(RectTransform targetImage)
 //   {
	//	if (targetImage == null) return Vector2.zero;
		

	//	Vector3 localPos = MainCanvas.InverseTransformPoint(targetImage.position);

	//	float x = localPos.x + (MainCanvas.rect.width * MainCanvas.pivot.x);
	//	float y = localPos.y + (MainCanvas.rect.height * MainCanvas.pivot.y);

 //   	return new Vector2(x, y);

 //   }

	// public void IncreaseUserGoldEff(int Coin)
 //   {
 //       GameMaster.UserCoinTmp += Coin;
 //     	txtUserGold.text = FormatScore(GameMaster.UserCoinTmp);

 //       StartCoroutine(ZoomCoin());
 //   }

	//private IEnumerator ZoomCoin()
 //   {
 //       rectIconCoin.localScale = new Vector3(1.15f, 1.15f, 1.15f);
 //       yield return new WaitForSeconds(0.03f);

 //       rectIconCoin.localScale = new Vector3(1.2f, 1.2f, 1.2f);
 //       yield return new WaitForSeconds(0.03f);

 //       rectIconCoin.localScale = new Vector3(1.25f, 1.25f, 1.25f);
 //       yield return new WaitForSeconds(0.03f);


 //       rectIconCoin.localScale = new Vector3(1.1f, 1.1f, 1.1f);
 //       yield return new WaitForSeconds(0.03f);

 //       rectIconCoin.localScale = new Vector3(1.0f, 1.0f, 1.0f);
 //       yield return new WaitForSeconds(0.03f);
 //   }

	

	public void ItemUnlockByAds()
	{
		bool res = GameMaster.staticScript.ShowRewardedAd(8);

		if(res == false)
		{
			PanelRewardNotLoaded.SetActive(true);
		}
	}

	public void OnHandleItemUnlockByAds()
	{
		Time.timeScale = 1f;

	}


	public void ShowNotEnoughGold()
	{
		GameMaster.staticScript.PlaySoundEmptyGold();
		PanelNotEnoughGold.SetActive(true);
	}

	public void OnClickHideNotEnoughGold()
	{
		GameMaster.PlaySoundClick();
		PanelNotEnoughGold.SetActive(false);
	}
    private void AddMissionGold(int amount)
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddMoney(amount);
        else
            GameMaster.AddGold(amount);
    }
    //private void UpdateRemainAdsTime()
    //{
    //	remainTimeAds = GameMaster.GetRemainSecondsWatchAds();
    //}
    //private void ShowRemainAdsTime()
    //{
    //	remainTimeAds -= 1;
    //	if (remainTimeAds <= 0)
    //	{
    //		btnWatchAds.interactable = true;
    //		btnWatchAdsEffect.SetActive(true);
    //		btnWatchAdsText.SetActive(true);
    //		txtTimeCoundownAds.gameObject.SetActive(false);
    //		CancelInvoke(nameof(ShowRemainAdsTime));
    //		return;
    //	}

    //	btnWatchAdsText.SetActive(false);
    //	btnWatchAds.interactable = false;
    //	btnWatchAdsEffect.SetActive(false);

    //	int minute = (int)(remainTimeAds / 60);
    //	int second = (int)(remainTimeAds % 60);

    //	txtTimeCoundownAds.text = $"{minute:D2}:{second:D2}";
    //	txtTimeCoundownAds.gameObject.SetActive(true);
    //}

    private void OnDestroy()
	{
		staticScript = null;
	}
}
