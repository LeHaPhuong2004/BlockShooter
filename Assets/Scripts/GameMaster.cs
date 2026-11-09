using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

//using GoogleMobileAds;
using GoogleMobileAds.Api;

using Facebook.Unity;

#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

//Note: After import firebase then have error FirebaseCppApp-6_15_0.bundle not open. Fix by open Security & Pravacy in MacOS and accept this file.
using Firebase;
using Firebase.Extensions;

#if UNITY_IOS
using UnityEngine.SocialPlatforms;
using UnityEngine.SocialPlatforms.GameCenter;
#elif UNITY_ANDROID
using GooglePlayGames;
#else

#endif

using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;

public class GameMaster : MonoBehaviour {
	public static GameMaster staticScript;
	private int bestScore;

	private int userGold;
	private int userCoinTmp = 0;
	private int soundOnOff;
	private int musicOnOff;
	//private int joystickOnOff;
	private int levelCleared;
	private int currLevel;
	private Dictionary<int, int> ListLevelStar;

	private int levelArcadeCleared;

	private int currLevelArcade;

	private Dictionary<int, int> ListLevelStarArcade;
	[SerializeField] private AudioSource soundSfxSource;
	[SerializeField] private AudioSource soundMusicSource;
	private int countRetryAd = 0;

	private int enableDailyReward;
	private long startTimePlayGame = 0;
    private long beginTimePlayGame = 0;

	private long lastTimeCheckDailyReward = 0;
    private int rewardDay1 = 0; //0 = not run, 1 = claim, 2 = miss
    private int rewardDay2 = 0;
    private int rewardDay3 = 0;
    private int rewardDay4 = 0;
    private int rewardDay5 = 0;
    private int rewardDay6 = 0;
    private int rewardDay7 = 0;
    private int rewardDay10 = 0;
    private int rewardDay15 = 0;
    private int rewardDay22 = 0;
    private int rewardDay30 = 0;

	private bool HasDedicatedSfxSource()
	{
		return soundSfxSource != null &&
			soundSfxSource != soundMusicSource &&
			(AudioManager.Instance == null || soundSfxSource != AudioManager.Instance.musicSource);
	}

	#if UNITY_IPHONE
	private string leaderboardID = "---";
#elif UNITY_ANDROID
	private string leaderboardID = "---";
#else
	private string leaderboardID = "--";
#endif

    
#if UNITY_IPHONE
	//private readonly string AdmobAppId = "---";
	private readonly string AdmobAppId = "---";
#elif UNITY_ANDROID
    //private readonly string AdmobAppId = "---";
	 private readonly string AdmobAppId = "---";
#else
    private readonly string AdmobAppId = "---";
#endif

#if UNITY_EDITOR
	string adUnitId = "unused";
	//string adBannerId = "unused";

	string adRewardId = "unused";

#elif UNITY_ANDROID
		//for production
		string adUnitId = "---";
		//string adBannerId = "---";
		string adRewardId = "---";

		//for test
		//string adUnitId = "ca-app-pub-3940256099942544/1033173712"; //only for test
		//string adBannerId = "ca-app-pub-3940256099942544/6300978111"; //only for test
		//string adRewardId = "ca-app-pub-3940256099942544/5224354917"; //only for test

#elif UNITY_IPHONE
		//for production
		string adUnitId = "---";
		//string adBannerId = "---";
		string adRewardId = "---";

		//for test
		//string adUnitId = "ca-app-pub-3940256099942544/4411468910"; //only for test
		//string adBannerId = "ca-app-pub-3940256099942544/2934735716"; //only for test
		//string adRewardId = "ca-app-pub-3940256099942544/1712485313"; //only for test
#else
		string adUnitId = "unexpected_platform";
		//string adBannerId = "unexpected_platform";
		string adRewardId = "unexpected_platform";
#endif

	private InterstitialAd interstitial;

	//private BannerView bannerView;
	private RewardedAd rewardedAd;
	public bool HasConfiguredRewardedAdUnit
	{
		get
		{
			const string prefix = "ca-app-pub-";
			if (string.IsNullOrWhiteSpace(adRewardId) ||
				!adRewardId.StartsWith(prefix, StringComparison.Ordinal)) return false;
			int separator = adRewardId.IndexOf('/');
			if (separator <= prefix.Length || separator >= adRewardId.Length - 1) return false;
			for (int index = prefix.Length; index < adRewardId.Length; index++)
			{
				if (index == separator) continue;
				if (adRewardId[index] < '0' || adRewardId[index] > '9') return false;
			}
			return true;
		}
	}
	private Action rewardedAdOpenedHandler;
	private Action rewardedAdClosedHandler;
	private Action<AdError> rewardedAdFailedHandler;
	private RewardedAdRequest rewardedAdRequest;
	private bool rewardedAdLoading;
	private int rewardedAdLoadVersion;

	private sealed class RewardedAdRequest
	{
		public RewardedAd Ad;
		public Action OnRewarded;
		public Action OnClosedWithoutReward;
		public bool UsesCallbacks;
		public bool RewardEarned;
		public bool Completed;
		public float PreviousTimeScale;
		public bool PreviousAudioPaused;
	}

 	private bool adRewardIsLoaded = false;
	 private int tryLoadAdRewardTime = 0;
	public static bool AdRewardIsLoaded
    {
        get { return staticScript.adRewardIsLoaded; }
        set
        {
            staticScript.adRewardIsLoaded = value;
        }
    }
 	private int currRewardType = 0;

	private int timeToShowAd;

	private int puzzleCounterCheckShowAds;
	private int arcadeCounterCheckShowAds;
	private int challengeCounterCheckShowAds;
	private int challengePlayCounter;

	private int rewardRevivalCounter = 0;

	private float lastAdShownTime = -30f; 

	private bool admobIsRequested = false;
	private int numberCheckReviewRate = 0;
	private int haveShowReviewRate = 0;

	private int haveNoAds = 0;
	private string iapPriceRemoveAds = "$0.99";
	private string iapPricePack1 = "";
    private string iapPricePack2 = "";
    private string iapPricePack3 = "";

	private string iapPricePack4 = "";
    private string iapPricePack5 = "";
    private string iapPricePack6 = "";

 	private int isFirstOpen = 0;



	public AudioClip soundClick;
	public AudioClip soundBuyItem;
	public AudioClip soundEggBrock;

	public AudioClip soundEmptyGold;

	public AudioClip soundGetGold;

	private Dictionary<int, int> listItemPriceGold   = new Dictionary<int, int> { { 1, 100}, {2, 150}, {3, 200}, {4, 300}};

	private Dictionary<int, int> listItemPriceAds = new Dictionary<int, int> { { 1, 2}, {2, 3}, {3, 4}, {4, 5} };

	public static Dictionary<int, int> ListItemPriceGold
    {
        get { return staticScript.listItemPriceGold; }
    }
	public static Dictionary<int, int> ListItemPriceAds
    {
        get { return staticScript.listItemPriceAds; }
    }

	//for Item
	private Dictionary<int, int> DicUserItem;
    public void LoadUserItem()
    {
        DicUserItem = new Dictionary<int, int>();

        string strList = EncryptedPlayerPrefs.GetString("LIST_USER_ITEM", "1:4;2:3;3:2;4:2");

        if (!string.IsNullOrEmpty(strList))
        {
            string[] arr = strList.Split(';');

            for (int i = 0; i < arr.Length; i++)
            {
                string[] arr2 = arr[i].Split(':');

                if (arr2.Length == 2)
                {
                    DicUserItem.Add(int.Parse(arr2[0]), int.Parse(arr2[1]));
                }
            }
        }
    }

    public static void AddToUserItem(int itemId, int num)
    {
        if (!staticScript.DicUserItem.ContainsKey(itemId))
        {
            staticScript.DicUserItem.Add(itemId, num);
		}
		else
		{
			staticScript.DicUserItem[itemId] += num;
		}

		//update to DB
		string strList = "";
		int k = 0;
		foreach (int tmp in staticScript.DicUserItem.Keys)
		{
			if (k > 0)
			{
				strList += ";" + tmp.ToString() + ":" + staticScript.DicUserItem[tmp];
			}
			else
			{
				strList = tmp.ToString() + ":" + staticScript.DicUserItem[tmp];
			}
			k++;
		}

		EncryptedPlayerPrefs.SetString("LIST_USER_ITEM", strList);
        
    }

	public static int CheckItemHave(int itemId)
    {
        if (staticScript.DicUserItem.ContainsKey(itemId))
        {
            return staticScript.DicUserItem[itemId];
        }
        else
        {
            return 0;
        }
    }

	public static void UseItem(int itemId)
    {
        if (staticScript.DicUserItem.ContainsKey(itemId) && staticScript.DicUserItem[itemId] > 0)
        {
			
			staticScript.DicUserItem[itemId] -= 1;
		
			//update to DB
			string strList = "";
			int k = 0;
			foreach (int tmp in staticScript.DicUserItem.Keys)
			{
				if (k > 0)
				{
					strList += ";" + tmp.ToString() + ":" + staticScript.DicUserItem[tmp];
				}
				else
				{
					strList = tmp.ToString() + ":" + staticScript.DicUserItem[tmp];
				}
				k++;
			}

			EncryptedPlayerPrefs.SetString("LIST_USER_ITEM", strList);
		}
    }


//for Item Watched Ads
	 private Dictionary<int, int> DicUserItemWatchAds;
    public void LoadUserItemWatchAds()
    {
        DicUserItemWatchAds = new Dictionary<int, int>();

        string strList = EncryptedPlayerPrefs.GetString("LIST_USER_ITEM_WATCH_ADS", "1:0");

        if (!string.IsNullOrEmpty(strList))
        {
            string[] arr = strList.Split(';');

            for (int i = 0; i < arr.Length; i++)
            {
                string[] arr2 = arr[i].Split(':');

                if (arr2.Length == 2)
                {
                    DicUserItemWatchAds.Add(int.Parse(arr2[0]), int.Parse(arr2[1]));
                }
            }
        }
    }

    public static void AddToUserItemWatchAds(int itemId)
    {
        if (!staticScript.DicUserItemWatchAds.ContainsKey(itemId))
        {
            staticScript.DicUserItemWatchAds.Add(itemId, 1);
		}
		else
		{
			staticScript.DicUserItemWatchAds[itemId] += 1;
		}

		//update to DB
		string strList = "";
		int k = 0;
		foreach (int tmp in staticScript.DicUserItemWatchAds.Keys)
		{
			if (k > 0)
			{
				strList += ";" + tmp.ToString() + ":" + staticScript.DicUserItemWatchAds[tmp];
			}
			else
			{
				strList = tmp.ToString() + ":" + staticScript.DicUserItemWatchAds[tmp];
			}
			k++;
		}

		EncryptedPlayerPrefs.SetString("LIST_USER_ITEM_WATCH_ADS", strList);
        
    }

	public static int CheckItemWatchAdsHave(int itemId)
    {
        if (staticScript.DicUserItemWatchAds.ContainsKey(itemId))
        {
            return staticScript.DicUserItemWatchAds[itemId];
        }
        else
        {
            return 0;
        }
    }


	public static void ResetItemWatchAdsHave(int itemId)
    {
        if (staticScript.DicUserItemWatchAds.ContainsKey(itemId))
        {
           staticScript.DicUserItemWatchAds[itemId] = 0;
        }
        else
        {
             staticScript.DicUserItemWatchAds.Add(itemId, 0);
        }
    }



    public static int IsFirstOpen
    {
        get { return staticScript.isFirstOpen; }
        set
        {
            staticScript.isFirstOpen = value;
			EncryptedPlayerPrefs.SetInt("is_first_open", value);
        }
    }


	private int isSentRealUser = 0;

	public static int IsSentRealUser
    {
        get { return staticScript.isSentRealUser; }
        set
        {
            staticScript.isSentRealUser = value;
			EncryptedPlayerPrefs.SetInt("is_sent_real_user", value);
        }
    }

	private int challengeHitScore_10 = 0;

	public static int ChallengeHitScore_10
    {
        get { return staticScript.challengeHitScore_10; }
        set
        {
            staticScript.challengeHitScore_10 = value;
			EncryptedPlayerPrefs.SetInt("challenge_hit_score_10", value);
        }
    }

	private int challengeHitScore_20 = 0;

	public static int ChallengeHitScore_20
    {
        get { return staticScript.challengeHitScore_20; }
        set
        {
            staticScript.challengeHitScore_20 = value;
			EncryptedPlayerPrefs.SetInt("challenge_hit_score_20", value);
        }
    }

	private int challengeHitScore_50 = 0;

	public static int ChallengeHitScore_50
    {
        get { return staticScript.challengeHitScore_50; }
        set
        {
            staticScript.challengeHitScore_50 = value;
			EncryptedPlayerPrefs.SetInt("challenge_hit_score_50", value);
        }
    }

	private int challengeHitScore_80 = 0;

	public static int ChallengeHitScore_80
    {
        get { return staticScript.challengeHitScore_80; }
        set
        {
            staticScript.challengeHitScore_80 = value;
			EncryptedPlayerPrefs.SetInt("challenge_hit_score_80", value);
        }
    }

	private int challengeHitScore_100 = 0;

	public static int ChallengeHitScore_100
    {
        get { return staticScript.challengeHitScore_100; }
        set
        {
            staticScript.challengeHitScore_100 = value;
			EncryptedPlayerPrefs.SetInt("challenge_hit_score_100", value);
        }
    }

	private int challengeHitScore_150 = 0;

	public static int ChallengeHitScore_150
    {
        get { return staticScript.challengeHitScore_150; }
        set
        {
            staticScript.challengeHitScore_150 = value;
			EncryptedPlayerPrefs.SetInt("challenge_hit_score_150", value);
        }
    }

	private int challengeHitScore_200 = 0;

	public static int ChallengeHitScore_200
    {
        get { return staticScript.challengeHitScore_200; }
        set
        {
            staticScript.challengeHitScore_200 = value;
			EncryptedPlayerPrefs.SetInt("challenge_hit_score_200", value);
        }

	}

	private int challengeHitScore_200_over = 0;

	public static int ChallengeHitScore_200_Over
    {
        get { return staticScript.challengeHitScore_200_over; }
        set
        {
            staticScript.challengeHitScore_200_over = value;
			EncryptedPlayerPrefs.SetInt("challenge_hit_score_200_over", value);
        }

	}


	public GameObject LoadingHomeControl;

	private bool isFirebaseInitialized = false;

	private Vector2 lastScreenSize; 

	public  Vector2 LastScreenSize
    {
        get { return lastScreenSize; }
        set
        {
            lastScreenSize = value;
        }
    }

  public static long StartTimePlayGame
    {
        get { return staticScript.startTimePlayGame; }
        set
        {
            staticScript.startTimePlayGame = value;
            EncryptedPlayerPrefs.SetString("start_time_play_game", staticScript.startTimePlayGame.ToString());
        }
    }

    public static long BeginTimePlayGame
    {
        get { return staticScript.beginTimePlayGame; }
        set
        {
            staticScript.beginTimePlayGame = value;
            EncryptedPlayerPrefs.SetString("start_begin_play_game", staticScript.beginTimePlayGame.ToString());
        }
    }

 	public static long LastTimeCheckDailyReward
    {
        get { return staticScript.lastTimeCheckDailyReward; }
        set
        {
            staticScript.lastTimeCheckDailyReward = value;
            EncryptedPlayerPrefs.SetString("last_check_daily_reward", staticScript.lastTimeCheckDailyReward.ToString());
        }
    }

	 public static int EnableDailyReward
    {
        get { return staticScript.enableDailyReward; }
        set
        {
            staticScript.enableDailyReward = value;
            EncryptedPlayerPrefs.SetInt("enable_daily_reward", staticScript.enableDailyReward);
        }
    }

    public static int RewardDay1
    {
        get { return staticScript.rewardDay1; }
        set
        {
            staticScript.rewardDay1 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_1", staticScript.rewardDay1);
        }
    }

    public static int RewardDay2
    {
        get { return staticScript.rewardDay2; }
        set
        {
            staticScript.rewardDay2 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_2", staticScript.rewardDay2);
        }
    }

    public static int RewardDay3
    {
        get { return staticScript.rewardDay3; }
        set
        {
            staticScript.rewardDay3 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_3", staticScript.rewardDay3);
        }
    }

    public static int RewardDay4
    {
        get { return staticScript.rewardDay4; }
        set
        {
            staticScript.rewardDay4 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_4", staticScript.rewardDay4);
        }
    }

    public static int RewardDay5
    {
        get { return staticScript.rewardDay5; }
        set
        {
            staticScript.rewardDay5 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_5", staticScript.rewardDay5);
        }
    }

    public static int RewardDay6
    {
        get { return staticScript.rewardDay6; }
        set
        {
            staticScript.rewardDay6 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_6", staticScript.rewardDay6);
        }
    }

    public static int RewardDay7
    {
        get { return staticScript.rewardDay7; }
        set
        {
            staticScript.rewardDay7 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_7", staticScript.rewardDay7);
        }
    }

    public static int RewardDay10
    {
        get { return staticScript.rewardDay10; }
        set
        {
            staticScript.rewardDay10 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_10", staticScript.rewardDay10);
        }
    }

    public static int RewardDay15
    {
        get { return staticScript.rewardDay15; }
        set
        {
            staticScript.rewardDay15 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_15", staticScript.rewardDay15);
        }
    }

    public static int RewardDay22
    {
        get { return staticScript.rewardDay22; }
        set
        {
            staticScript.rewardDay22 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_22", staticScript.rewardDay22);
        }
    }

    public static int RewardDay30
    {
        get { return staticScript.rewardDay30; }
        set
        {
            staticScript.rewardDay30 = value;
            EncryptedPlayerPrefs.SetInt("reward_day_30", staticScript.rewardDay30);
        }
    }


	void Awake(){
		//PlayerPrefs.DeleteAll();
		GameObject.DontDestroyOnLoad (gameObject);

		if (FindObjectsOfType(GetType()).Length > 1)
		{
			DestroyImmediate(gameObject);
			return;
		}

		staticScript = this;

		EncryptedPlayerPrefs.keys = new string[]
		{
			"hqys#1j*71nMkHg01h%s",
			"abK7G%1kn&hAvdpueb$@",
			"ll916G1^dhNbaj*h76Bdj",
			"1hB$jdbKT1dfb&2b@bldJK"
		};

		PortraitDisplay.LockPortraitOrientation();
		LoadUserData();
	}

	// Use this for initialization
	private IEnumerator Start () {
		
		//if (soundOnOff == 0)
		//{
		//	AudioListener.volume = 0f;
		//}

		//Only for test
		//levelCleared = 605;

		admobIsRequested = false;
		countRetryAd = 0;

		yield return new WaitForSeconds(0.2f);

#if UNITY_IOS && !UNITY_EDITOR
		//Request ATT
		//Debug.Log("Request ATT");
		if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
		{
			//Debug.Log("ATT NOT_DETERMINED");

			yield return new WaitForSeconds(0.3f);

			//Debug.Log("Send TT RequestAuthorizationTracking");
			ATTrackingStatusBinding.RequestAuthorizationTracking();

			
			
			//yield return new WaitUntil(() => ATTrackingStatusBinding.GetAuthorizationTrackingStatus() != ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED);

			float timeout = 15f; 
    		float startTime = Time.time;

			while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
			{
				if (Time.time - startTime > timeout)
				{
					break;
				}
				yield return new WaitForSeconds(0.1f);
			}
			

			//Debug.Log("Finish RequestAuthorizationTracking OR time out");
		}
#endif

		yield return new WaitForSeconds(0.2f);

		InitFB();

		yield return new WaitForSeconds(0.2f);

		LogInToGameCenter();

		yield return new WaitForSeconds(0.2f);

		lastScreenSize = new Vector2(Screen.width, Screen.height);

		MobileAds.RaiseAdEventsOnUnityMainThread = true;
		//MobileAds.Initialize(AdmobAppId);
		MobileAds.Initialize(initStatus => {
		    Debug.Log("Admob initialize done!");
			//LoadBannerAd();
        });

		yield return new WaitForSeconds(0.2f);


		Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
			var dependencyStatus = task.Result;
			if (dependencyStatus == Firebase.DependencyStatus.Available)
			{
				// Create and hold a reference to your FirebaseApp,
				// where app is a Firebase.FirebaseApp property of your application class.
				//app = Firebase.FirebaseApp.DefaultInstance;

				// Set a flag here to indicate whether Firebase is ready to use by your app.

				//TestText.text = "isFirebaseInitialized OK----";

				isFirebaseInitialized = true;

				this.gameObject.GetComponent<FirebaseRemoteConfig>().RunInitializeFirebaseRemoteConfig();

				/*
				var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();

				if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED)
				{
					Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);

					Debug.Log("Firebase Analytics AUTHORIZED");
				}
				else
				{
					Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(false);
					Debug.Log("Firebase Analytics NOT AUTHORIZED");
				}
				*/
				Debug.Log("---- Firebase initialize OK!");
			}
			else
			{
				UnityEngine.Debug.LogError(System.String.Format("Firebase Error: could not resolve all Firebase dependencies: {0}", dependencyStatus));
			}
		});

		yield return new WaitForSeconds(0.2f);

		LoadingHomeControl.SetActive(true);

		yield return null;

		//RequestInterstitial ();
	}

	// Update is called once per frame
	void Update () {
	
	}

	//for facebook

	private void InitFB()
	{
		if (!FB.IsInitialized)
		{
			Debug.Log("Call Initialize the Facebook SDK");
			FB.Init(InitCallback, OnHideUnity);
		}
		else
		{
			Debug.Log("Already initialized, signal an app activation App Event. Now is active app");
			FB.ActivateApp();
		}
	}

	private void InitCallback()
	{
		if (FB.IsInitialized)
		{
			// Signal an app activation App Event
			Debug.Log("Initialize the Facebook SDK success. Now  is active app");
			FB.ActivateApp();
			// Continue with Facebook SDK
			// ...
		}
		else
		{
			Debug.Log("Failed to Initialize the Facebook SDK. Please check resource");
		}
	}

	private void OnHideUnity(bool isGameShown)
	{
		if (!isGameShown)
		{
			// Pause the game - we will need to hide
			//Time.timeScale = 0;
		}
		else
		{
			// Resume the game - we're getting focus again
			//Time.timeScale = 1;
		}
	}

	public static void LogEventAction(string funcName)
	{
		Firebase.Analytics.FirebaseAnalytics.LogEvent(funcName);

		if (FB.IsInitialized){
			FB.LogAppEvent(funcName);
		}
	}

	public static void LogEventPurchase(decimal price, string currency)
    {
        if (FB.IsInitialized){
        
            FB.LogPurchase(price, currency);
        }
    }

	//public static void LogEventActionWithParam(string funcName, string paramName, int paramValue)
	//{
	//	Firebase.Analytics.FirebaseAnalytics.LogEvent(funcName, new Firebase.Analytics.Parameter(paramName, paramValue));
	//}

	//public static void LogEventActionWithParam(string funcName, string paramName, string paramValue)
	//{
	//	Firebase.Analytics.FirebaseAnalytics.LogEvent(funcName, new Firebase.Analytics.Parameter(paramName, paramValue));
	//}

	public void LoadUserData(){
		userGold = EncryptedPlayerPrefs.GetInt ("user_gold", 0);

		levelCleared = EncryptedPlayerPrefs.GetInt ("level_cleared", 1);

		currLevel  = EncryptedPlayerPrefs.GetInt ("curr_level", 1);

		levelArcadeCleared = EncryptedPlayerPrefs.GetInt ("level_arcade_cleared", 1);


		currLevelArcade  = EncryptedPlayerPrefs.GetInt ("curr_level_arcade", 1);


		bestScore = EncryptedPlayerPrefs.GetInt ("best_score", 0);
		soundOnOff = EncryptedPlayerPrefs.GetInt ("sound_on_off", 1);
		musicOnOff = EncryptedPlayerPrefs.GetInt ("music_on_off", 1);

		if (HasDedicatedSfxSource())
		{
			soundSfxSource.volume = soundOnOff == 1 ? 1.0f : 0.0f;
			if (soundOnOff == 0) soundSfxSource.Stop();
		}

        musicOnOff = EncryptedPlayerPrefs.GetInt("music_on_off", 1);

        if (musicOnOff == 1)
        {
            soundMusicSource.volume = 1.0f;
            if (!soundMusicSource.isPlaying)
                soundMusicSource.Play();
        }
        else
        {
            soundMusicSource.Stop();
            soundMusicSource.volume = 0.0f;
        }


        //joystickOnOff = EncryptedPlayerPrefs.GetInt("joystick_on_off", 0);
        timeToShowAd = EncryptedPlayerPrefs.GetInt ("time_to_show_ad", 3);

		challengePlayCounter = EncryptedPlayerPrefs.GetInt ("challenge_play_counter", 0);
		rewardRevivalCounter = EncryptedPlayerPrefs.GetInt ("reward_revival_counter", 0);

		puzzleCounterCheckShowAds = EncryptedPlayerPrefs.GetInt ("puzzle_counter_check_show_ads", 0);
		arcadeCounterCheckShowAds = EncryptedPlayerPrefs.GetInt ("arcade_counter_check_show_ads", 0);
		challengeCounterCheckShowAds = EncryptedPlayerPrefs.GetInt ("challenge_counter_check_show_ads", 0);

		numberCheckReviewRate = EncryptedPlayerPrefs.GetInt("number_check_review", 0);
		haveShowReviewRate = EncryptedPlayerPrefs.GetInt("have_check_review", 0);

        haveNoAds = EncryptedPlayerPrefs.GetInt("have_no_ads", 0);
		iapPriceRemoveAds = EncryptedPlayerPrefs.GetString("iap_price_remove_ads", "");

		iapPricePack1 = EncryptedPlayerPrefs.GetString("KEY_IAP_PRICE_PACK_1", "$0.99");
        iapPricePack2 = EncryptedPlayerPrefs.GetString("KEY_IAP_PRICE_PACK_2", "$1.99");
        iapPricePack3 = EncryptedPlayerPrefs.GetString("KEY_IAP_PRICE_PACK_3", "$3.99");

		iapPricePack4 = EncryptedPlayerPrefs.GetString("KEY_IAP_PRICE_PACK_4", "$7.99");
        iapPricePack5 = EncryptedPlayerPrefs.GetString("KEY_IAP_PRICE_PACK_5", "$14.99");
        iapPricePack6 = EncryptedPlayerPrefs.GetString("KEY_IAP_PRICE_PACK_6", "$29.99");


		isFirstOpen =  EncryptedPlayerPrefs.GetInt("is_first_open", 0);
		isSentRealUser =  EncryptedPlayerPrefs.GetInt("is_sent_real_user", 0);

		challengeHitScore_10 =  EncryptedPlayerPrefs.GetInt("challenge_hit_score_10", 0);
		challengeHitScore_20 =  EncryptedPlayerPrefs.GetInt("challenge_hit_score_20", 0);
		challengeHitScore_50 =  EncryptedPlayerPrefs.GetInt("challenge_hit_score_50", 0);
		challengeHitScore_80 =  EncryptedPlayerPrefs.GetInt("challenge_hit_score_80", 0);
		challengeHitScore_100 =  EncryptedPlayerPrefs.GetInt("challenge_hit_score_100", 0);
		challengeHitScore_150 =  EncryptedPlayerPrefs.GetInt("challenge_hit_score_150", 0);
		challengeHitScore_200 =  EncryptedPlayerPrefs.GetInt("challenge_hit_score_200", 0);
		challengeHitScore_200_over =  EncryptedPlayerPrefs.GetInt("challenge_hit_score_200_over", 0);
		
		
  		startTimePlayGame = long.Parse(EncryptedPlayerPrefs.GetString("start_time_play_game", "0"));

        if (startTimePlayGame == 0)
        {
            startTimePlayGame = (System.DateTime.Now.Ticks / 10000000);
            EncryptedPlayerPrefs.SetString("start_time_play_game", startTimePlayGame.ToString());
        }

        beginTimePlayGame = long.Parse(EncryptedPlayerPrefs.GetString("start_begin_play_game", "0"));
        if (beginTimePlayGame == 0)
        {
            beginTimePlayGame = (System.DateTime.Now.Ticks / 10000000);
            EncryptedPlayerPrefs.SetString("start_begin_play_game", beginTimePlayGame.ToString());
        }

		lastTimeCheckDailyReward = long.Parse(EncryptedPlayerPrefs.GetString("last_check_daily_reward", "0"));
       
	   	enableDailyReward = EncryptedPlayerPrefs.GetInt("enable_daily_reward", 0);
	    
        rewardDay1 = EncryptedPlayerPrefs.GetInt("reward_day_1", 0);
        rewardDay2 = EncryptedPlayerPrefs.GetInt("reward_day_2", 0);
        rewardDay3 = EncryptedPlayerPrefs.GetInt("reward_day_3", 0);
        rewardDay4 = EncryptedPlayerPrefs.GetInt("reward_day_4", 0);
        rewardDay5 = EncryptedPlayerPrefs.GetInt("reward_day_5", 0);
        rewardDay6 = EncryptedPlayerPrefs.GetInt("reward_day_6", 0);
        rewardDay7 = EncryptedPlayerPrefs.GetInt("reward_day_7", 0);
        rewardDay10 = EncryptedPlayerPrefs.GetInt("reward_day_10", 0);
        rewardDay15 = EncryptedPlayerPrefs.GetInt("reward_day_15", 0);
        rewardDay22 = EncryptedPlayerPrefs.GetInt("reward_day_22", 0);
        rewardDay30 = EncryptedPlayerPrefs.GetInt("reward_day_30", 0);

		LoadListLevelStar ();
		LoadListLevelStarArcade ();

		LoadUserItem();

		LoadUserItemWatchAds();

		//for test
		//haveNoAds = 1;
	}

 	public static int UserGold
    {
        get { return staticScript.userGold; }
        set
        {
            staticScript.userGold = value;
            EncryptedPlayerPrefs.SetInt("user_gold", staticScript.userGold);
        }
    }

	public static int UserCoinTmp
    {
        get { return staticScript.userCoinTmp; }
        set
        {
            staticScript.userCoinTmp = value;

        }
    }



	public static int LevelCleared {
		get{ return staticScript.levelCleared; }
		set {
			//if (staticScript.levelCleared >= 400)
			//	return;
			staticScript.levelCleared = value;
			EncryptedPlayerPrefs.SetInt ("level_cleared", staticScript.levelCleared);
		}
	}

	public static int LevelArcadeCleared {
		get{ return staticScript.levelArcadeCleared; }
		set {
			//if (staticScript.levelArcadeCleared >= 1000)
			//	return;
			staticScript.levelArcadeCleared = value;
			EncryptedPlayerPrefs.SetInt ("level_arcade_cleared", staticScript.levelArcadeCleared);
		}
	}

	public static int BestScore{
		get{ return staticScript.bestScore; }
		set{
			if (value > staticScript.bestScore) {
				staticScript.bestScore = value;
				//save to disk
				EncryptedPlayerPrefs.SetInt("best_score", value);
			}
		}
	}

	public static int SoundOnOff{
		get{ return staticScript.soundOnOff; }
		set{
			staticScript.soundOnOff = value;

			if (AudioManager.Instance != null)
				AudioManager.Instance.SetSFXVolume(value == 0 ? 0.0f : 1.0f);
			
			if (staticScript.HasDedicatedSfxSource()) {
				staticScript.soundSfxSource.volume = value == 0 ? 0.0f : 1.0f;
				if (value == 0) staticScript.soundSfxSource.Stop();
			}
			

			//save to disk
			EncryptedPlayerPrefs.SetInt("sound_on_off", value);
		}
	}

    public static int MusicOnOff
    {
        get { return staticScript.musicOnOff; }
        set
        {
            staticScript.musicOnOff = value;
            EncryptedPlayerPrefs.SetInt("music_on_off", value);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(value == 0 ? 0.0f : 1.0f);
            }

            if (staticScript.soundMusicSource == null)
            {
                return;
            }
            else
            {
               
            }

            if (value == 1)
            {
                
                staticScript.soundMusicSource.volume = 1.0f;

                if (!staticScript.soundMusicSource.isPlaying)
                {
                    staticScript.soundMusicSource.Play();
               
                }
                else
                {
                    Debug.Log(" Nhạc nền đang chạy  không gọi lại Play()");
                }
            }
            else
            {
                
                staticScript.soundMusicSource.Stop();
                staticScript.soundMusicSource.volume = 0.0f;
               
            }

        }
    }
    /*
	public static int JoystickOnOff
	{
		get { return staticScript.joystickOnOff; }
		set
		{
			staticScript.joystickOnOff = value;
			//save to disk
			EncryptedPlayerPrefs.SetInt("joystick_on_off", value);
		}
	}
	*/

    public static int NumberCheckReviewRate
	{
		get { return staticScript.numberCheckReviewRate; }
		set
		{
			staticScript.numberCheckReviewRate = value;
			//save to disk
			EncryptedPlayerPrefs.SetInt("number_check_review", value);
		}
	}

	public static int HaveShowReviewRate
	{
		get { return staticScript.haveShowReviewRate; }
		set
		{
			staticScript.haveShowReviewRate = value;
			//save to disk
			EncryptedPlayerPrefs.SetInt("have_check_review", value);
			
		}
	}


	public static int HaveNoAds
	{
		get { return staticScript.haveNoAds; }
		set
		{
			staticScript.haveNoAds = value;
			//save to disk
			EncryptedPlayerPrefs.SetInt("have_no_ads", value);

		}
	}

	public static string IapPriceRemoveAds
	{
		get { return staticScript.iapPriceRemoveAds; }
		set
		{
			staticScript.iapPriceRemoveAds = value;
			//save to disk
			EncryptedPlayerPrefs.SetString("iap_price_remove_ads", value);

		}
	}

	public static string IapPricePack1
    {
        get { return staticScript.iapPricePack1; }
        set
        {
            staticScript.iapPricePack1 = value;
            EncryptedPlayerPrefs.SetString("KEY_IAP_PRICE_PACK_1", value);
        }
    }

    public static string IapPricePack2
    {
        get { return staticScript.iapPricePack2; }
        set
        {
            staticScript.iapPricePack2 = value;
            EncryptedPlayerPrefs.SetString("KEY_IAP_PRICE_PACK_2", value);
        }
    }

    public static string IapPricePack3
    {
        get { return staticScript.iapPricePack3; }
        set
        {
            staticScript.iapPricePack3 = value;
            EncryptedPlayerPrefs.SetString("KEY_IAP_PRICE_PACK_3", value);
        }
    }

	public static string IapPricePack4
    {
        get { return staticScript.iapPricePack4; }
        set
        {
            staticScript.iapPricePack4 = value;
            EncryptedPlayerPrefs.SetString("KEY_IAP_PRICE_PACK_4", value);
        }
    }

    public static string IapPricePack5
    {
        get { return staticScript.iapPricePack5; }
        set
        {
            staticScript.iapPricePack5 = value;
            EncryptedPlayerPrefs.SetString("KEY_IAP_PRICE_PACK_5", value);
        }
    }

    public static string IapPricePack6
    {
        get { return staticScript.iapPricePack6; }
        set
        {
            staticScript.iapPricePack6 = value;
            EncryptedPlayerPrefs.SetString("KEY_IAP_PRICE_PACK_6", value);
        }
    }

	public static void PlaySoundClick(){
		if(SoundOnOff == 1)
		{
			staticScript.soundSfxSource.PlayOneShot(staticScript.soundClick);
		}
		
	}

	public void PlayBackgroundSound(){
		if (MusicOnOff == 1)
		{
			if(!soundMusicSource.isPlaying)
			{
				soundMusicSource.Play();
				
			}

			//soundMusicSource.volume = 1.0f;
			StartCoroutine(FadeVolume(soundMusicSource, 1.0f, 2.0f));
		}
	}

	public void PlayBackgroundSoundIngame(){
		if (MusicOnOff == 1)
		{
			if(!soundMusicSource.isPlaying)
			{
				soundMusicSource.Play();
				
			}
			//soundMusicSource.volume = 0.3f;
			StartCoroutine(FadeVolume(soundMusicSource, 0.3f, 1.5f));
		}
	}

	public IEnumerator FadeVolume(AudioSource source, float targetVolume, float duration)
	{
		float startVolume = source.volume;
		float t = 0;

		while (t < duration)
		{
			t += Time.deltaTime;
			source.volume = Mathf.Lerp(startVolume, targetVolume, t / duration);
			yield return null;
		}

		source.volume = targetVolume;
	}

	public void PlaySoundBuyItem()
	{
		if(SoundOnOff == 1)
		{
			soundSfxSource.PlayOneShot(soundBuyItem);
		}
		
	}

	public void PlaySoundEggBrock()
	{
		if(SoundOnOff == 1)
		{
			soundSfxSource.PlayOneShot(soundEggBrock);
		}
		
	}

	public void PlaySoundEmptyGold()
	{
		if(SoundOnOff == 1)
		{
			soundSfxSource.PlayOneShot(soundEmptyGold);	
		}
		
	}

	public void PlaySoundGetGold()
	{
		if(SoundOnOff == 1)
		{
			soundSfxSource.PlayOneShot(soundGetGold);
		}
		
	}

	//Load level star from file with format ListItemUser 101:1,102:2,103:1,104:3
	public void LoadListLevelStar(){
		ListLevelStar = new Dictionary<int, int> ();

		string strListLevelStar = EncryptedPlayerPrefs.GetString ("list_level_star", "");
		//strListLevelStar = "201:1";
		if (strListLevelStar != "") {
			string[] tmp = strListLevelStar.Split(',');
			for(int i = 0; i < tmp.Length; i++){
				string[] item = tmp[i].Split(':');
				//build list level star infor
				ListLevelStar.Add(int.Parse(item[0]), int.Parse(item[1]));
			}
		}

		//Debug.Log (ListLevelStar.Count);
	}

	//Save
	public  void SaveListLevelStar(){
		string strListLevelStar = "";
		int i = 0;
		int total = ListLevelStar.Count;
		foreach(KeyValuePair<int, int> entry in ListLevelStar)
		{
			string itemNode = entry.Key.ToString() + ":" + entry.Value.ToString();
			if(i < total - 1){
				strListLevelStar += itemNode + ",";
			}else{
				strListLevelStar += itemNode;
			}
			i++;
		}

		//Debug.Log (strListLevelStar);
		EncryptedPlayerPrefs.SetString ("list_level_star", strListLevelStar);
	}

	//Call when user buy item in shop
	public static void UpdateLevelStar(int level, int starNumber){
		if (staticScript.ListLevelStar.ContainsKey (level)) {
			if (staticScript.ListLevelStar [level] < starNumber) {
				staticScript.ListLevelStar [level] = starNumber;
			}
		} else {
			staticScript.ListLevelStar.Add(level, starNumber);
		}
		staticScript.SaveListLevelStar ();
	}

	public static int GetLevelStar(int level){
		if (staticScript.ListLevelStar.ContainsKey (level)) {
			return staticScript.ListLevelStar [level];
		} else {
			return 0;
		}
	}

	public void LoadListLevelStarArcade(){
		ListLevelStarArcade = new Dictionary<int, int> ();

		string strListLevelStarArcade = EncryptedPlayerPrefs.GetString ("list_level_star_arcade", "");
		//strListLevelStar = "201:1";
		if (strListLevelStarArcade != "") {
			string[] tmp = strListLevelStarArcade.Split(',');
			for(int i = 0; i < tmp.Length; i++){
				string[] item = tmp[i].Split(':');
				//build list level star infor
				ListLevelStarArcade.Add(int.Parse(item[0]), int.Parse(item[1]));
			}
		}

		//Debug.Log (ListLevelStar.Count);
	}

	//Save
	public  void SaveListLevelStarArcade(){
		string strListLevelStarArcade = "";
		int i = 0;
		int total = ListLevelStarArcade.Count;
		foreach(KeyValuePair<int, int> entry in ListLevelStarArcade)
		{
			string itemNode = entry.Key.ToString() + ":" + entry.Value.ToString();
			if(i < total - 1){
				strListLevelStarArcade += itemNode + ",";
			}else{
				strListLevelStarArcade += itemNode;
			}
			i++;
		}

		//Debug.Log (strListLevelStar);
		EncryptedPlayerPrefs.SetString ("list_level_star_arcade", strListLevelStarArcade);
	}

	public static void UpdateLevelStarArcade(int level, int starNumber){
		if (staticScript.ListLevelStarArcade.ContainsKey (level)) {
			if (staticScript.ListLevelStarArcade [level] < starNumber) {
				staticScript.ListLevelStarArcade [level] = starNumber;
			}
		} else {
			staticScript.ListLevelStarArcade.Add(level, starNumber);
		}
		staticScript.SaveListLevelStarArcade ();
	}

	public static int GetLevelStarArcade(int level){
		if (staticScript.ListLevelStarArcade.ContainsKey (level)) {
			return staticScript.ListLevelStarArcade [level];
		} else {
			return 0;
		}
	}

	public static int CurrLevel{
		get{ return staticScript.currLevel; }
		set{ 
			staticScript.currLevel = value; 
			EncryptedPlayerPrefs.SetInt ("curr_level", staticScript.currLevel);
			}
	}

	public static int CurrLevelArcade{
		get{ return staticScript.currLevelArcade; }
		set{ 
			staticScript.currLevelArcade = value; 
			EncryptedPlayerPrefs.SetInt ("curr_level_arcade", staticScript.currLevelArcade);
			}
	}

	public static int TimeToShowAd{
		get{return staticScript.timeToShowAd;}
		set{
			staticScript.timeToShowAd = value;
			EncryptedPlayerPrefs.SetInt ("time_to_show_ad", staticScript.timeToShowAd);
		}
	}


	public static int ChallengePlayCounter
	{
		get{return staticScript.challengePlayCounter;}
		set{
			staticScript.challengePlayCounter = value;
			EncryptedPlayerPrefs.SetInt ("challenge_play_counter", staticScript.challengePlayCounter);
		}
	}

	public static int RewardRevivalCounter
	{
		get{return staticScript.rewardRevivalCounter;}
		set{
			staticScript.rewardRevivalCounter = value;
			EncryptedPlayerPrefs.SetInt ("reward_revival_counter", staticScript.rewardRevivalCounter);
		}
	}

	

	public static int PuzzleCounterCheckShowAds
	{
		get{return staticScript.puzzleCounterCheckShowAds;}
		set{
			staticScript.puzzleCounterCheckShowAds = value;
			EncryptedPlayerPrefs.SetInt ("puzzle_counter_check_show_ads", staticScript.puzzleCounterCheckShowAds);
		}
	}

	public static int ArcadeCounterCheckShowAds
	{
		get{return staticScript.arcadeCounterCheckShowAds;}
		set{
			staticScript.arcadeCounterCheckShowAds = value;
			EncryptedPlayerPrefs.SetInt ("arcade_counter_check_show_ads", staticScript.arcadeCounterCheckShowAds);
		}
	}

	public static int ChallengeCounterCheckShowAds
	{
		get{return staticScript.challengeCounterCheckShowAds;}
		set{
			staticScript.challengeCounterCheckShowAds = value;
			EncryptedPlayerPrefs.SetInt ("challenge_counter_check_show_ads", staticScript.challengeCounterCheckShowAds);
		}
	}

	public static void UpdateBestScore(int new_score){
		Social.ReportScore (new_score, staticScript.leaderboardID, success => {
			Debug.Log(success ? "Reported score successfully" : "Failed to report score");
		});
	}


	public void LogInToGameCenter ()
	{
		Social.localUser.Authenticate ((bool success) =>
		{
			if (success) {
				Debug.Log ("Login Sucess");
			} else {
				Debug.Log ("Login failed");
			}
		});
	}



	public static void ShowLeaderBoard(){
		#if UNITY_IPHONE
		Social.ShowLeaderboardUI ();
		#elif UNITY_ANDROID
		((PlayGamesPlatform)Social.Active).ShowLeaderboardUI (staticScript.leaderboardID); // Show current (Active) leaderboard
		#else
		Social.ShowLeaderboardUI ();
		#endif
	}


	public static void ShowFullBanner(){
        if(HaveNoAds == 0) {
			//Debug.Log("Show Full banner");
			staticScript.ShowInterstitial ();
		}
	}

	public void HandleInterstialOnAdFailedToLoad () //object sender, EventArgs args
	{
		Debug.Log ("Load ad fail " + countRetryAd);
		countRetryAd++;

		//Only try 3 times
		if (countRetryAd >= 3)
		return;


		if (interstitial != null) {
			interstitial.Destroy ();
		}

		RequestInterstitial ();
	}

	public void HandleInterstialOnAdLoaded () //object sender, EventArgs args
	{
		countRetryAd = 0;
	}

	public void HandleInterstialOnAdOpening () //object sender, EventArgs args
	{
		Time.timeScale = 0f;
		MuteGameAudio(true);
        LogEventAction("AD_INTERSTITIAL_OPENED");
	}

	public void HandleInterstialOnAdClosed ()
	{
		Time.timeScale = 1f;

		MuteGameAudio(false);

        LogEventAction("AD_INTERSTITIAL_CLOSED");

		admobIsRequested = false;
		countRetryAd = 0;
		RequestInterstitial ();

	}

	public void HandleOnAdFullScreenContentFailed(AdError error) //object sender, EventArgs args
	{
		Time.timeScale = 1f;

		MuteGameAudio(false);

        LogEventAction("AD_INTERSTITIAL_FAILED");

		admobIsRequested = false;
		countRetryAd++;

		//Only try 3 times
		if (countRetryAd >= 3)
			return;

		RequestInterstitial();
	}


	private void RequestInterstitial()
	{
		admobIsRequested = true;

		if (interstitial != null)
		{
			interstitial.OnAdFullScreenContentClosed -= HandleInterstialOnAdClosed;
			interstitial.OnAdFullScreenContentOpened -= HandleInterstialOnAdOpening;
			interstitial.OnAdFullScreenContentFailed -= HandleOnAdFullScreenContentFailed;

            interstitial.Destroy();
			interstitial = null;
		}

#if UNITY_ANDROID
		//For Android
		//AdRequest request = new AdRequest.Builder().AddTestDevice("791692F2E51F69482F222C537731D9F7").Build();
		AdRequest request = new AdRequest();
#elif UNITY_IPHONE
		//AdRequest request = new AdRequest.Builder().AddTestDevice("1deefd6d9cd8e6c0821823586225c264").Build();
		AdRequest request = new AdRequest();
#else
		//AdRequest request = new AdRequest.Builder().Build();
		AdRequest request = new AdRequest();
#endif

		//AdRequest request = new AdRequest.Builder().Build();
		//interstitial.LoadAd(request);
		InterstitialAd.Load(adUnitId, request, (InterstitialAd ad, LoadAdError loadAdError) =>
		{
			if (loadAdError != null)
			{
				Debug.LogError("Interstitial ad failed to load an ad with error : " + loadAdError.GetMessage());
				return;
			}
			else
			if (ad == null)
			{
				Debug.LogError("Unexpected error: Interstitial load event fired with null ad and null error.");
				return;
			}
			Debug.Log("Interstitial ad loaded");
			interstitial = ad;

			//RegisterEventHandlers(ad);
			ad.OnAdFullScreenContentClosed += HandleInterstialOnAdClosed;
			ad.OnAdFullScreenContentOpened += HandleInterstialOnAdOpening;
			ad.OnAdFullScreenContentFailed += HandleOnAdFullScreenContentFailed;
		});
	}

	private void ShowInterstitial()
	{
		float adsCooldown = (float)FirebaseRemoteConfig.staticInstance.GetShowAdsAfterTime();

		if (Time.unscaledTime - lastAdShownTime > adsCooldown){
			if (interstitial != null && interstitial.CanShowAd())
			{
				interstitial.Show();
				LogEventAction("AD_INTERSTITIAL");
			}
			else
			{
				LogEventAction("AD_INTERSTITIAL_NOT_READY");
				Debug.Log("Interstitial is not ready yet.");
			}

			lastAdShownTime = Time.unscaledTime;
		}
	}

	public static void ReloadInterstitial(){
		/*
		if (!staticScript.interstitial.IsLoaded())
		{
			#if UNITY_ANDROID
			//For Android
			AdRequest request = new AdRequest.Builder().AddTestDevice("791692F2E51F69482F222C537731D9F7").Build();
			#elif UNITY_IPHONE
			AdRequest request = new AdRequest.Builder().AddTestDevice("1deefd6d9cd8e6c0821823586225c264").Build();
			#else
			AdRequest request = new AdRequest.Builder().Build();
			#endif

			staticScript.interstitial.LoadAd(request);
		}
		*/
	}

	public static void PreloadInterstitial(){
		staticScript.RequestInterstitial();
	}

	public static void TryRequestAdmob()
	{
		//Debug.Log("TryRequestAdmob");
		if (staticScript.interstitial == null || !staticScript.interstitial.CanShowAd())
		{
			//Debug.Log("interstitial admob is null with countRetryAd = " + staticScript.countRetryAd);
			
			staticScript.countRetryAd = 0;

			staticScript.RequestInterstitial();

			//Debug.Log("Send RequestInterstitial when ads null");
			
		}
		
	}

	/*
	public void LoadBannerAd()
    {
		if (haveNoAds == 1) return;

        if (bannerView != null)
        {
            bannerView.Destroy();
        }

		bannerView = new BannerView(adBannerId, AdSize.Banner, AdPosition.Bottom);
		//bannerView = new BannerView(adBannerId, adaptiveSize, AdPosition.Bottom);
		
        AdRequest adRequest = new AdRequest();

        bannerView.LoadAd(adRequest);
    }

	// Gọi hàm này để ẩn banner tạm thời
	public void HideBanner()
	{
		if (bannerView != null)
		{
			bannerView.Hide();
			Debug.Log("Banner đã ẩn.");
		}
	}

	// Gọi hàm này để hiện lại banner đã load
	public void ShowBanner()
	{
		if (bannerView != null)
		{
			bannerView.Show();
			Debug.Log("Banner đã hiện lại.");
		}
	}

	public static void ShowSmallBanner(){
        if(HaveNoAds == 0) {
			//Debug.Log("Show Full banner");
			staticScript.ShowBanner ();
		}
	}

	public static void HideSmallBanner(){
		//Debug.Log("Show Full banner");
		staticScript.HideBanner ();
	}
	*/

	public static void RemoveAllAds(){
		staticScript.OnRemoveAllAds ();
	}

	public void OnRemoveAllAds()
	{
		/*
		if (bannerView != null)
		{
			bannerView.Destroy();
            bannerView = null;
		}
		*/

		if (interstitial != null)
		{
			interstitial.OnAdFullScreenContentClosed -= HandleInterstialOnAdClosed;
			interstitial.OnAdFullScreenContentOpened -= HandleInterstialOnAdOpening;
			interstitial.OnAdFullScreenContentFailed -= HandleOnAdFullScreenContentFailed;

            interstitial.Destroy();
			interstitial = null;
		}

	}


    public void LoadRewardedAd()
    {
        if (!HasConfiguredRewardedAdUnit)
        {
            adRewardIsLoaded = false;
            return;
        }
        if (rewardedAdRequest != null || rewardedAdLoading) return;

        ReleaseRewardedAd();
        adRewardIsLoaded = false;
        rewardedAdLoading = true;
        int loadVersion = ++rewardedAdLoadVersion;

        var adRequest = new AdRequest();

        RewardedAd.Load(adRewardId, adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                if (this == null || loadVersion != rewardedAdLoadVersion)
                {
                    if (ad != null) ad.Destroy();
                    return;
                }

                rewardedAdLoading = false;
  
                if (error != null || ad == null)
                {
                    Debug.LogError("Error Rewarded Ad: " + error);
                    return;
                }

                Debug.Log("Rewarded Ad load success!");
                rewardedAd = ad;
                adRewardIsLoaded = true;
                // Đăng ký các sự kiện (Event Handlers) cho quảng cáo này
                RegisterEventHandlers(rewardedAd);
            });
    }

	private void TryReloadRewardedAd()
    {
        tryLoadAdRewardTime++;
        if(tryLoadAdRewardTime < 3)
        {
            LoadRewardedAd();
        }
    }

    public void PreloadRewardAd()
    {
        if(adRewardIsLoaded == false)
        {
            tryLoadAdRewardTime = 0;
            LoadRewardedAd();
        }
    }

    public bool ShowRewardedAd(int rewardType)
    {
		Debug.Log("ShowRewardedAd with ID  rewardType  = " + rewardType);
        return ShowRewardedAdRequest(rewardType, null, null, false);
    }

    /// <summary>
    /// Starts one rewarded ad. A false result invokes neither callback.
    /// Accepted requests invoke one callback after the ad closes or fails;
    /// onRewarded requires the SDK's earned-reward event before closure.
    /// </summary>
    public bool TryShowRewardedAd(Action onRewarded, Action onClosedWithoutReward)
    {
#if UNITY_EDITOR
        return false;
#else
        if (onRewarded == null) return false;
        return ShowRewardedAdRequest(0, onRewarded, onClosedWithoutReward, true);
#endif
    }

    private bool ShowRewardedAdRequest(int rewardType, Action onRewarded,
        Action onClosedWithoutReward, bool usesCallbacks)
    {
        if (rewardedAdRequest != null) return false;

        if (rewardedAd == null || !rewardedAd.CanShowAd())
        {
			 LogEventAction("AD_REWARD_NOT_READY");
            Debug.LogError("Rewarded Ad not ready to show.");
			return false;
        }

        var request = new RewardedAdRequest
        {
            Ad = rewardedAd,
            OnRewarded = onRewarded,
            OnClosedWithoutReward = onClosedWithoutReward,
            UsesCallbacks = usesCallbacks,
            PreviousTimeScale = Time.timeScale,
            PreviousAudioPaused = AudioListener.pause
        };
        rewardedAdRequest = request;
        currRewardType = rewardType;
        adRewardIsLoaded = false;

        try
        {
            request.Ad.Show((Reward reward) =>
            {
                if (!ReferenceEquals(rewardedAdRequest, request) || request.Completed) return;
                request.RewardEarned = true;
                Debug.Log(String.Format("Reward type: {0}, count: {1}", reward.Type, reward.Amount));
            });
        }
        catch (Exception exception)
        {
            // No callback for an unaccepted request; a synchronous SDK close may
            // already have completed it before Show threw.
            if (request.Completed) return true;
            FinishRewardedAdRequest(request, false, false);
            ReleaseRewardedAd();
            Debug.LogException(exception);
            return false;
        }

        LogEventAction("AD_REWARD_SHOW");
        return true;
    }


  	private void RegisterEventHandlers(RewardedAd ad)
    {
        // Raised when the ad is estimated to have earned money.
        ad.OnAdPaid += (AdValue adValue) =>
        {
            //Debug.Log(String.Format("Rewarded ad paid {0} {1}.", adValue.Value, adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Rewarded ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        ad.OnAdClicked += () =>
        {
            Debug.Log("Rewarded ad was clicked.");
        };
        // Raised when an ad opened full screen content.
        rewardedAdOpenedHandler = () =>
        {
            if (ReferenceEquals(rewardedAd, ad)) HandleRewardAdFullScreenContentOpened();
        };
        ad.OnAdFullScreenContentOpened += rewardedAdOpenedHandler;
        // Raised when the ad closed full screen content.
        rewardedAdClosedHandler = () =>
        {
            if (ReferenceEquals(rewardedAd, ad)) HandleRewardAdFullScreenContentClosed();
        };
        ad.OnAdFullScreenContentClosed += rewardedAdClosedHandler;
        // Raised when the ad failed to open full screen content.
        rewardedAdFailedHandler = error =>
        {
            if (ReferenceEquals(rewardedAd, ad)) HandleRewardAdFullScreenContentFailed(error);
        };
        ad.OnAdFullScreenContentFailed += rewardedAdFailedHandler;
    }

    private void ReleaseRewardedAd()
    {
        var ad = rewardedAd;
        rewardedAd = null;
        if (ad == null) return;

        ad.OnAdFullScreenContentOpened -= rewardedAdOpenedHandler;
        ad.OnAdFullScreenContentClosed -= rewardedAdClosedHandler;
        ad.OnAdFullScreenContentFailed -= rewardedAdFailedHandler;
        rewardedAdOpenedHandler = null;
        rewardedAdClosedHandler = null;
        rewardedAdFailedHandler = null;
        ad.Destroy();
    }

	private void HandleRewardAdFullScreenContentOpened()
	{
		if (rewardedAdRequest == null) return;
		Debug.Log("Rewarded ad full screen content opened.");
		Time.timeScale = 0f;
		MuteGameAudio(true);
		LogEventAction("AD_REWARD_OPENED");
	}

	private void HandleRewardAdFullScreenContentClosed()
	{
		var request = rewardedAdRequest;
		if (request == null || request.Completed) return;
		Debug.Log("Rewarded ad full screen content closed.");
		Debug.Log("HandleRewardedAdClose currRewardType = " + currRewardType);
		LogEventAction("AD_REWARD_CLOSED");
		FinishRewardedAdRequest(request, request.RewardEarned, true);
		ReloadAfterRewardedAd(request, false);
	}

	 private void HandleRewardAdFullScreenContentFailed(AdError error)
	{
		var request = rewardedAdRequest;
		if (request == null || request.Completed) return;
		Debug.LogError("Rewarded ad failed to open full screen content " + "with error : " + error);
		LogEventAction("AD_REWARD_FAILED");
		FinishRewardedAdRequest(request, false, true);
		ReloadAfterRewardedAd(request, true);
	}

    private void FinishRewardedAdRequest(RewardedAdRequest request, bool rewarded, bool notify)
    {
        if (!ReferenceEquals(rewardedAdRequest, request) || request.Completed) return;

        request.Completed = true;
        rewardedAdRequest = null;
        adRewardIsLoaded = false;
        currRewardType = 0;
        Time.timeScale = request.UsesCallbacks ? request.PreviousTimeScale : 1f;
        MuteGameAudio(request.UsesCallbacks && request.PreviousAudioPaused);

        Action callback = rewarded ? request.OnRewarded : request.OnClosedWithoutReward;
        request.OnRewarded = null;
        request.OnClosedWithoutReward = null;
        if (!notify || callback == null) return;

        try { callback(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private void ReloadAfterRewardedAd(RewardedAdRequest request, bool failed)
    {
#if UNITY_EDITOR
        if (request.UsesCallbacks) return;
#endif
        if (failed) TryReloadRewardedAd();
        else LoadRewardedAd();
    }

	private void MuteGameAudio(bool isMute)
    {
        AudioListener.pause = isMute;
    }
	private void OnDestroy()
    {
        //if (bannerView != null)
        //{
            //bannerView.Destroy();
        //}

 		if (interstitial != null)
		{

            interstitial.OnAdFullScreenContentClosed -= HandleInterstialOnAdClosed;
			interstitial.OnAdFullScreenContentOpened -= HandleInterstialOnAdOpening;
			interstitial.OnAdFullScreenContentFailed -= HandleOnAdFullScreenContentFailed;

            interstitial.Destroy();
			interstitial = null;
		}

		if (rewardedAdRequest != null)
            FinishRewardedAdRequest(rewardedAdRequest, false, false);
        ++rewardedAdLoadVersion;
        rewardedAdLoading = false;
        ReleaseRewardedAd();

		staticScript = null;
    }

	private bool IsTabletByPhysicalSize()
    {
		int screenWidth = Screen.width;
       	int screenHeight = Screen.height;

		int longSide = Mathf.Max(screenWidth, screenHeight);
        int shortSide = Mathf.Min(screenWidth, screenHeight);
	
		float ratio = 1.0f;

		if (shortSide > 0)
        {
            ratio = (float)longSide / (float)shortSide;
        }

		if(ratio <= 1.6f && shortSide >= 1080)
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	public static void StartCooldownAds()
	{
		long nextTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 900;
		EncryptedPlayerPrefs.SetString("NEXT_TIME_ADS", nextTime.ToString());
	}

	public static int GetRemainSecondsWatchAds()
    {
        long nextTime = long.Parse(PlayerPrefs.GetString("NEXT_TIME_ADS", "0"));
        long remain = nextTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return Mathf.Max(0, (int)remain);
    }
    public void OnMusicToggle(bool isOn)
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        MusicOnOff = isOn ? 1 : 0;
    }
    public static void AddGold(int amount)
    {
        if (amount <= 0 || staticScript == null) return;

        CurrencyManager.AwardMoney(amount);
        Debug.Log("Total Gold = " + UserGold);

        AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollect);


    }

   
}
