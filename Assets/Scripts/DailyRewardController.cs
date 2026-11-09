using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;


public class DailyRewardController : MonoBehaviour
{
    public HomeController scriptHomeController;
    public GameObject btnClose;
    public GameObject btnClaim;
    public GameObject btnClaimX2;

    public Transform[] ListDayItem;

    public Transform Day7;
    public Transform Day10;
    public Transform Day15;
    public Transform Day22;
    public Transform Day30;


    public RectTransform TimelinePercent;

    private int rewardDay = 0;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void FillAndShowForDate(int afterDay)
    {
        int[] listRewardOfDay = { GameMaster.RewardDay1, GameMaster.RewardDay2 , GameMaster.RewardDay3 , GameMaster.RewardDay4 , GameMaster.RewardDay5 , GameMaster.RewardDay6 , GameMaster.RewardDay7 , GameMaster.RewardDay10 , GameMaster.RewardDay15 , GameMaster.RewardDay22 , GameMaster.RewardDay30 };
        rewardDay = 0;
        for(int i = 1; i < 8; i++)
        {
            if(afterDay >= i)
            {
                //current
                if (listRewardOfDay[i - 1] == 0)
                {
                    //ready for claim reward
                    if(afterDay == i){
                        ListDayItem[i - 1].GetChild(1).gameObject.SetActive(false); //Pass
                        ListDayItem[i - 1].GetChild(2).gameObject.SetActive(false); //Checked
                        ListDayItem[i - 1].GetChild(3).gameObject.SetActive(false); //Next
                        ListDayItem[i - 1].GetChild(4).gameObject.SetActive(true); //Spray
                        rewardDay = i;
                    }
                    else
                    {
                        //miss reward
                        ListDayItem[i - 1].GetChild(1).gameObject.SetActive(true); //Pass
                        ListDayItem[i - 1].GetChild(2).gameObject.SetActive(false); //Checked
                        ListDayItem[i - 1].GetChild(3).gameObject.SetActive(false); //Next
                        ListDayItem[i - 1].GetChild(4).gameObject.SetActive(false); //Spray
                    }
                  
                }
                else
                {
                    //claimed
                    ListDayItem[i - 1].GetChild(1).gameObject.SetActive(true); //Pass
                    ListDayItem[i - 1].GetChild(2).gameObject.SetActive(true); //Checked
                    ListDayItem[i - 1].GetChild(3).gameObject.SetActive(false); //Next
                    ListDayItem[i - 1].GetChild(4).gameObject.SetActive(false); //Spray
                }

            }else
            {
                //not run
                ListDayItem[i - 1].GetChild(1).gameObject.SetActive(false); //Pass
                ListDayItem[i - 1].GetChild(2).gameObject.SetActive(false); //Checked
                ListDayItem[i - 1].GetChild(3).gameObject.SetActive(true); //Next
                ListDayItem[i - 1].GetChild(4).gameObject.SetActive(false); //Spray
             
            }
        }


        //check for Day10
        if (afterDay >= 7)
        {
            if (GameMaster.RewardDay7 == 0)
            {
                //ready claim
                Day7.GetChild(0).gameObject.SetActive(true);
                Day7.GetChild(1).gameObject.SetActive(false);

            }
            else
            {
                //claimed
                Day7.GetChild(0).gameObject.SetActive(false);
                Day7.GetChild(1).gameObject.SetActive(true);
            }
        }
        else
        {
            //not run
            Day7.GetChild(0).gameObject.SetActive(true);
            Day7.GetChild(1).gameObject.SetActive(false);
        }

        if (afterDay >= 10)
        {
            if (GameMaster.RewardDay10 == 0)
            {
                //ready claim
                Day10.GetChild(0).gameObject.SetActive(true);
                Day10.GetChild(1).gameObject.SetActive(false);
                Day10.GetChild(2).gameObject.SetActive(true);
                Day10.GetChild(3).gameObject.SetActive(true);
                Day10.GetChild(4).gameObject.SetActive(false);
                rewardDay = 10;
            }
            else
            {
                //claimed
                Day10.GetChild(0).gameObject.SetActive(false);
                Day10.GetChild(1).gameObject.SetActive(true);
                Day10.GetChild(2).gameObject.SetActive(false);
                Day10.GetChild(3).gameObject.SetActive(false);
                Day10.GetChild(4).gameObject.SetActive(true);
            }
        }
        else
        {
            //not run
            Day10.GetChild(0).gameObject.SetActive(true);
            Day10.GetChild(1).gameObject.SetActive(false);
            Day10.GetChild(2).gameObject.SetActive(false);
            Day10.GetChild(3).gameObject.SetActive(true);
            Day10.GetChild(4).gameObject.SetActive(false);
        }

        //check for Day15
        if (afterDay >= 15)
        {
            if (GameMaster.RewardDay15 == 0)
            {
                //ready claim
                Day15.GetChild(0).gameObject.SetActive(true);
                Day15.GetChild(1).gameObject.SetActive(false);
                Day15.GetChild(2).gameObject.SetActive(true);
                Day15.GetChild(3).gameObject.SetActive(true);
                Day15.GetChild(4).gameObject.SetActive(false);

                rewardDay = 15;

            }
            else
            {
                //claimed
                Day15.GetChild(0).gameObject.SetActive(false);
                Day15.GetChild(1).gameObject.SetActive(true);
                Day15.GetChild(2).gameObject.SetActive(false);
                Day15.GetChild(3).gameObject.SetActive(false);
                Day15.GetChild(4).gameObject.SetActive(true);
            }
        }
        else
        {
            //not run
            Day15.GetChild(0).gameObject.SetActive(true);
            Day15.GetChild(1).gameObject.SetActive(false);
            Day15.GetChild(2).gameObject.SetActive(false);
            Day15.GetChild(3).gameObject.SetActive(true);
            Day15.GetChild(4).gameObject.SetActive(false);
        }

        //check for Day22
        if (afterDay >= 22)
        {
            if (GameMaster.RewardDay22 == 0)
            {
                //ready claim
                Day22.GetChild(0).gameObject.SetActive(true);
                Day22.GetChild(1).gameObject.SetActive(false);
                Day22.GetChild(2).gameObject.SetActive(true);
                Day22.GetChild(3).gameObject.SetActive(true);
                Day22.GetChild(4).gameObject.SetActive(false);
                rewardDay = 22;

            }
            else
            {
                //claimed
                Day22.GetChild(0).gameObject.SetActive(false);
                Day22.GetChild(1).gameObject.SetActive(true);
                Day22.GetChild(2).gameObject.SetActive(false);
                Day22.GetChild(3).gameObject.SetActive(false);
                Day22.GetChild(4).gameObject.SetActive(true);
            }
        }
        else
        {
            //not run
            Day22.GetChild(0).gameObject.SetActive(true);
            Day22.GetChild(1).gameObject.SetActive(false);
            Day22.GetChild(2).gameObject.SetActive(false);
            Day22.GetChild(3).gameObject.SetActive(true);
            Day22.GetChild(4).gameObject.SetActive(false);
        }

        //check for Day30
       if (afterDay >= 30)
        {
            if (GameMaster.RewardDay30 == 0)
            {
                //ready claim
                Day30.GetChild(0).gameObject.SetActive(true);
                Day30.GetChild(1).gameObject.SetActive(false);
                Day30.GetChild(2).gameObject.SetActive(true);
                Day30.GetChild(3).gameObject.SetActive(true);
                Day30.GetChild(4).gameObject.SetActive(false);
                rewardDay = 30;

            }
            else
            {
                //claimed
                Day30.GetChild(0).gameObject.SetActive(false);
                Day30.GetChild(1).gameObject.SetActive(true);
                Day30.GetChild(2).gameObject.SetActive(false);
                Day30.GetChild(3).gameObject.SetActive(false);
                Day30.GetChild(4).gameObject.SetActive(true);
            }
        }
        else
        {
            //not run
            Day30.GetChild(0).gameObject.SetActive(true);
            Day30.GetChild(1).gameObject.SetActive(false);
            Day30.GetChild(2).gameObject.SetActive(false);
            Day30.GetChild(3).gameObject.SetActive(true);
            Day30.GetChild(4).gameObject.SetActive(false);
        }

        //show for time line
        int day = afterDay;

        if(day < 1)
        {
            day = 1;
        }
        if (day <= 7)
        {
            TimelinePercent.sizeDelta = new Vector2(0.0f, 37f);
        }else if (day <= 10)
        {
            TimelinePercent.sizeDelta = new Vector2( (float)(day - 7) * 134.0f / 3.0f, 37.0f);
        }else if(day <= 15)
        {
            TimelinePercent.sizeDelta = new Vector2( 134.0f +  (float)(day - 10) * (274.0f - 134.0f) / 5.0f, 37.0f);
        }
        else if (day <= 22)
        {
           TimelinePercent.sizeDelta = new Vector2( 274.0f +  (float)(day - 15) * (414.0f - 274.0f) / 7.0f, 37.0f);
        }
        else if (day <= 30)
        {
            TimelinePercent.sizeDelta = new Vector2( 414.0f +  (float)(day - 22) * (570.0f - 414.0f) / 8.0f, 37.0f);
        }
        else
        {
            TimelinePercent.sizeDelta = new Vector2(570.0f, 37.0f);
        }

        if(rewardDay > 0)
        {
            btnClose.SetActive(false);
            btnClaim.SetActive(true);
            btnClaimX2.SetActive(true);
        }
        else
        {
            btnClose.SetActive(true);
            btnClaim.SetActive(false);
            btnClaimX2.SetActive(false);
        }
        
    }

    public void OnClickClose()
    {
        GameMaster.PlaySoundClick();

        this.gameObject.SetActive(false);
    }

    public void OnClickClaimX2()
    {
        GameMaster.PlaySoundClick();

       scriptHomeController.DailyRewardClaimX2();

    }

    public void ProcessClaimX2()
    {
        btnClaim.SetActive(false);
        btnClaimX2.SetActive(false);
        
        int  bonusGold = ClaimRewardOfDay(rewardDay, 3);

        if(bonusGold > 0)
        {
            //show animation gold
            GameMaster.staticScript.PlaySoundGetGold();
            scriptHomeController.ShowCoinMoveAnimtion(bonusGold, 1);
        }

        Invoke(nameof(HideDailyReward), 1f);
    }

    public void OnClickClaim()
    {
        GameMaster.PlaySoundClick();

        btnClaim.SetActive(false);
        btnClaimX2.SetActive(false);

        int  bonusGold = ClaimRewardOfDay(rewardDay, 1);

        if(bonusGold > 0)
        {
            //show animation gold
            GameMaster.staticScript.PlaySoundGetGold();
            scriptHomeController.ShowCoinMoveAnimtion(bonusGold, 1);
        }

        Invoke(nameof(HideDailyReward), 1f);
    }
    
    private void HideDailyReward()
    {
        this.gameObject.SetActive(false);
    }

    private int ClaimRewardOfDay(int day, int multi)
    {
        GameMaster.PlaySoundClick();

        int bonusGold = 0;
        if (day == 0) return bonusGold;

        if(day <= 7)
        {
            switch (day)
            {
                case 1:
                    //collect 100 gold
                 
                    bonusGold = 100 * multi;
                    GameMaster.RewardDay1 = 1;

                    ListDayItem[0].GetChild(1).gameObject.SetActive(false); //Pass
                    ListDayItem[0].GetChild(2).gameObject.SetActive(true); //Checked
                    ListDayItem[0].GetChild(3).gameObject.SetActive(false); //Next
                    ListDayItem[0].GetChild(4).gameObject.SetActive(false); //Spray
                    break;
                case 2:

                    bonusGold = 500 * multi;

                    GameMaster.RewardDay2 = 1;

                    ListDayItem[1].GetChild(1).gameObject.SetActive(false); //Pass
                    ListDayItem[1].GetChild(2).gameObject.SetActive(true); //Checked
                    ListDayItem[1].GetChild(3).gameObject.SetActive(false); //Next
                    ListDayItem[1].GetChild(4).gameObject.SetActive(false); //Spray
                 
                    break;
                case 3:

                    bonusGold = 0;
                    GameMaster.AddToUserItem(2, 3 * multi);

                    GameMaster.RewardDay3 = 1;

                    ListDayItem[2].GetChild(1).gameObject.SetActive(false); //Pass
                    ListDayItem[2].GetChild(2).gameObject.SetActive(true); //Checked
                    ListDayItem[2].GetChild(3).gameObject.SetActive(false); //Next
                    ListDayItem[2].GetChild(4).gameObject.SetActive(false); //Spray
                    break;
                case 4:
                    bonusGold = 1000 * multi;
                    GameMaster.RewardDay4 = 1;

                    ListDayItem[3].GetChild(1).gameObject.SetActive(false); //Pass
                    ListDayItem[3].GetChild(2).gameObject.SetActive(true); //Checked
                    ListDayItem[3].GetChild(3).gameObject.SetActive(false); //Next
                    ListDayItem[3].GetChild(4).gameObject.SetActive(false); //Spray
                    break;
                case 5:
                    bonusGold = 0;
                    GameMaster.AddToUserItem(3, 4 * multi);
                    GameMaster.RewardDay5 = 1;

                    ListDayItem[4].GetChild(1).gameObject.SetActive(false); //Pass
                    ListDayItem[4].GetChild(2).gameObject.SetActive(true); //Checked
                    ListDayItem[4].GetChild(3).gameObject.SetActive(false); //Next
                    ListDayItem[4].GetChild(4).gameObject.SetActive(false); //Spray

                    break;
                case 6:
                    bonusGold = 1500 * multi;
                    GameMaster.RewardDay6 = 1;

                    ListDayItem[5].GetChild(1).gameObject.SetActive(false); //Pass
                    ListDayItem[5].GetChild(2).gameObject.SetActive(true); //Checked
                    ListDayItem[5].GetChild(3).gameObject.SetActive(false); //Next
                    ListDayItem[5].GetChild(4).gameObject.SetActive(false); //Spray

                    break;
                case 7:
                    bonusGold = 5000 * multi;
                    GameMaster.AddToUserItem(4, 5 * multi);

                    GameMaster.RewardDay7 = 1;

                    //add item 1 and item 2
                    ListDayItem[6].GetChild(1).gameObject.SetActive(false); //Pass
                    ListDayItem[6].GetChild(2).gameObject.SetActive(true); //Checked
                    ListDayItem[6].GetChild(3).gameObject.SetActive(false); //Next
                    ListDayItem[6].GetChild(4).gameObject.SetActive(false); //Spray

                    break;
            }
        }
        else
        {
            //extra

            switch (day)
            {
                case 10:
                    bonusGold = 2000 * multi;
                    GameMaster.RewardDay10 = 1;

                    //ready claim
                    Day10.GetChild(0).gameObject.SetActive(false);
                    Day10.GetChild(1).gameObject.SetActive(true);
                    Day10.GetChild(2).gameObject.SetActive(false);
                    Day10.GetChild(3).gameObject.SetActive(false);
                    Day10.GetChild(4).gameObject.SetActive(true);
                    break;
                case 15:
                    bonusGold = 2000 * multi;
                    GameMaster.RewardDay15 = 1;

                    Day15.GetChild(0).gameObject.SetActive(false);
                    Day15.GetChild(1).gameObject.SetActive(true);
                    Day15.GetChild(2).gameObject.SetActive(false);
                    Day15.GetChild(3).gameObject.SetActive(false);
                    Day15.GetChild(4).gameObject.SetActive(true);
                    break;
                case 22:
                    bonusGold = 2000 * multi;
                    GameMaster.RewardDay22 = 1;

                    Day22.GetChild(0).gameObject.SetActive(false);
                    Day22.GetChild(1).gameObject.SetActive(true);
                    Day22.GetChild(2).gameObject.SetActive(false);
                    Day22.GetChild(3).gameObject.SetActive(false);
                    Day22.GetChild(4).gameObject.SetActive(true);
                    break;
                case 30:
                    bonusGold = 2000 * multi;
                    GameMaster.RewardDay30 = 1;

                    Day30.GetChild(0).gameObject.SetActive(false);
                    Day30.GetChild(1).gameObject.SetActive(true);
                    Day30.GetChild(2).gameObject.SetActive(false);
                    Day30.GetChild(3).gameObject.SetActive(false);
                    Day30.GetChild(4).gameObject.SetActive(true);
                    break;
            }
        }

        rewardDay = 0;

        return bonusGold;
    }
}
