using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Threading.Tasks;

using Firebase;
//using Firebase.Unity.Editor;

public class FirebaseRemoteConfig : MonoBehaviour {

    public static FirebaseRemoteConfig staticInstance;

    Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;
    protected bool isFirebaseInitialized = false;
    // Use this for initialization

    private void Awake()
    {
        if (staticInstance == null)
        {
            staticInstance = this;
        }
    }

    void Start () {
		
	}


    public void RunInitializeFirebaseRemoteConfig()
    {
        Invoke("CallInitializeFirebase", 1f);
    }

    //private void CallInitializeFirebase()
    //{
    //    Debug.Log("FirebaseRemoteConfig start");
    //    Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
    //        dependencyStatus = task.Result;
    //        if (dependencyStatus == Firebase.DependencyStatus.Available)
    //        {
    //            InitializeFirebaseRemoteConfig();

    //            FetchData();
    //        }
    //        else
    //        {
    //            Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
    //        }
    //    });
    //}

    // Update is called once per frame
    void Update () {
		
	}

	// Initialize remote config, and set the default values.
	void InitializeFirebaseRemoteConfig() {
		System.Collections.Generic.Dictionary<string, object> defaults =
			new System.Collections.Generic.Dictionary<string, object>();

		// These are the values that are used if we haven't fetched data from the
		// server
		// yet, or if we ask for values that the server doesn't have:

        defaults.Add("show_ads_after_time", 30); //show ads after minimum time

        defaults.Add("show_ads_after_puzzle", 5); //show ads after clear first 5 level in puzzle
        defaults.Add("show_ads_after_arcade", 4); //show ads after clear first 4 level in arcade
        defaults.Add("show_ads_after_challenge", 2); //show ads after clear first 3 time in challenge

	    defaults.Add("show_ads_next_puzzle", 3); //show ads after finish puzzle
		defaults.Add ("show_ads_next_arcade", 3); //show ads when finish arcade
		defaults.Add ("show_ads_next_challenge", 2);//show ads when challenge 3 times - 1

        defaults.Add("show_reward_after_time", 2); //show reward ads after time
        defaults.Add("rate_show_reward_win", 20); //show reward ads when score over best 0 to 100
        defaults.Add("real_user_challenge", 2);
        defaults.Add("real_user_puzzle", 5); 
        defaults.Add("real_user_arcade", 5); 
        
        //Firebase.RemoteConfig.FirebaseRemoteConfig.SetDefaults(defaults);

        Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults);

        isFirebaseInitialized = true;
        ////Debug.Log("RemoteConfig configured and ready!");
    }

    public int GetShowAdsAfterTime(){
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_show_ads_after_time", 30);
        }
        int res = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue ("show_ads_after_time").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_show_ads_after_time", res);
        return res;
    }

	public int GetShowAdsAfterPuzzle(){
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_show_ads_after_puzzle", 5);
        }
        int res = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue ("show_ads_after_puzzle").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_show_ads_after_puzzle", res);
        return res;
    }

    public int GetShowAdsAfterArcade()
    {
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_show_ads_after_arcade", 4);
        }
        int res = int.Parse(Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("show_ads_after_arcade").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_show_ads_after_arcade", res);
        return res;
    }


    public int GetShowAdsAfterChallenge()
    {
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_show_ads_after_challenge", 2);
        }
        int res = int.Parse(Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("show_ads_after_challenge").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_show_ads_after_challenge", res);
        return res;
    }

    public int GetShowAdsNextPuzzle(){
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_show_ads_next_puzzle", 3);
        }
        int res = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue ("show_ads_next_puzzle").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_show_ads_next_puzzle", res);
        return res;
    }

    public int GetShowAdsNextArcade()
    {
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_show_ads_next_arcade", 3);
        }
        int res = int.Parse(Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("show_ads_next_arcade").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_show_ads_next_arcade", res);
        return res;
    }


    public int GetShowAdsNextChallenge()
    {
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_show_ads_next_challenge", 2);
        }
        int res = int.Parse(Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue("show_ads_next_challenge").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_show_ads_next_challenge", res);
        return res;
    }

    public int GetShowRewardAfterTime(){
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_show_reward_after_time", 2);
        }
        int res = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue ("show_reward_after_time").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_show_reward_after_time", res);
        return res;
    }

    public int GetRateShowRewardWin(){
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_rate_show_reward_win", 20);
        }
        int res = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue ("rate_show_reward_win").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_rate_show_reward_win", res);
        return res;
    }
    
    public int GetRealUserChallenge(){
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_real_user_challenge", 2);
        }
        int res = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue ("real_user_challenge").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_real_user_challenge", res);
        return res;
    }

    public int GetRealUserArcade(){
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_real_user_arcade", 5);
        }
        int res = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue ("real_user_arcade").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_real_user_arcade", res);
        return res;
    }

    public int GetRealUserPuzzle(){
        if (!isFirebaseInitialized)
        {
            return EncryptedPlayerPrefs.GetInt("remote_real_user_puzzle", 5);
        }
        int res = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue ("real_user_puzzle").StringValue);
        EncryptedPlayerPrefs.SetInt("remote_real_user_puzzle", res);
        return res;
    }
    // Start a fetch request.
 //   public void FetchData() {
 //       if (!isFirebaseInitialized) return;

	//	Debug.Log("Remote Fetching data...");
	//	// FetchAsync only fetches new data if the current data is older than the provided
	//	// timespan.  Otherwise it assumes the data is "recent enough", and does nothing.
	//	// By default the timespan is 12 hours, and for production apps, this is a good
	//	// number.  For this example though, it's set to a timespan of zero, so that
	//	// changes in the console will always show up immediately.
	//	System.Threading.Tasks.Task fetchTask = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.FetchAsync(
	//		TimeSpan.FromHours(2));
	//	fetchTask.ContinueWith(FetchComplete);
	//}

	//void FetchComplete(Task fetchTask) {
	//	if (fetchTask.IsCanceled) {
	//		////Debug.Log("Fetch canceled.");
	//	} else if (fetchTask.IsFaulted) {
	//		////Debug.Log("Fetch encountered an error.");
	//	} else if (fetchTask.IsCompleted) {
	//		////Debug.Log("Fetch completed successfully!");
	//	}

	//	var info = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.Info;
	//	switch (info.LastFetchStatus) {
	//	case Firebase.RemoteConfig.LastFetchStatus.Success:
	//		//Firebase.RemoteConfig.FirebaseRemoteConfig.ActivateFetched ();
 //           Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.ActivateAsync();
 //               //Debug.Log (string.Format ("Remote data loaded and ready (last fetch time {0}).", info.FetchTime));

 //               //after loaded then reload lang
 //               /*
 //               if (GameMaster.SettingLanguage == 0) {
 //                   int lan = int.Parse (Firebase.RemoteConfig.FirebaseRemoteConfig.GetValue ("lang").StringValue);
 //                   Localize.loadLanguage (lan);

 //                   if (lan != 0) {
 //                       GameMaster.SettingLanguage = lan;
 //                   }
 //               }
 //               */
 //               break;
	//	case Firebase.RemoteConfig.LastFetchStatus.Failure:
	//		switch (info.LastFetchFailureReason) {
	//		case Firebase.RemoteConfig.FetchFailureReason.Error:
	//			////Debug.Log("Fetch failed for unknown reason");
	//			break;
	//		case Firebase.RemoteConfig.FetchFailureReason.Throttled:
	//			////Debug.Log("Fetch throttled until " + info.ThrottledEndTime);
	//			break;
	//		}
	//		break;
	//	case Firebase.RemoteConfig.LastFetchStatus.Pending:
	//		////Debug.Log("Latest Fetch call still pending.");
	//		break;
	//	}
	//}
}
