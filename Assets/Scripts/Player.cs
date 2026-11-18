using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{

    [SerializeField] 
    TMP_Text moneyDisplay;

    [Serializable]
    public struct RangeLevel
    {
        public int cost;
        [Tooltip("Inner spot angle (degrees) after buying this level")]
        public float innerSpotAngle;
        [Tooltip("Outer spot angle (degrees) after buying this level")]
        public float outerSpotAngle;
    }

    public int money = 0;

    [Tooltip("Reaching this much money (or less) loses the level")]
    [SerializeField] int loseMoney = -500;

    [SerializeField] BoatSpawner boatSpawner;

    [SerializeField] LightBeam lightBeam;

    [SerializeField] Button rangeUpgrade;
    [SerializeField] Button beamSpeedUpgrade;
    [SerializeField] Button extraBeamUpgrade;
    [SerializeField] Button litBoatSpeedUpgrade;

    [Header("Range upgrade")]
    [Tooltip("One entry per purchasable level, in order")]
    [SerializeField] RangeLevel[] rangeLevels =
    {
        new RangeLevel { cost = 50,  innerSpotAngle = 20f, outerSpotAngle = 35f },
        new RangeLevel { cost = 120, innerSpotAngle = 28f, outerSpotAngle = 45f },
        new RangeLevel { cost = 250, innerSpotAngle = 36f, outerSpotAngle = 60f },
    };
    int rangeLevel = 0; // number of range levels bought

    void Start()
    {
        rangeUpgrade.onClick.AddListener(UpgradeRange);
        RefreshUpgradeButtons();
    }

    public void IncreaseMoney(int amount)
    {
        money += amount;
        RefreshUpgradeButtons();

        if (money <= loseMoney)
            boatSpawner.EndLevel(false);
    }

    bool TrySpend(int cost)
    {
        if (money < cost)
            return false;
        money -= cost;
        return true;
    }

    void RefreshUpgradeButtons()
    {
        bool rangeMaxed = rangeLevel >= rangeLevels.Length;
        rangeUpgrade.interactable = !rangeMaxed && money >= rangeLevels[rangeLevel].cost;
        moneyDisplay.text = money + "$";
        var texts = rangeUpgrade.GetComponentsInChildren<TMP_Text>();
        foreach (var text in texts)
        {
            if (text.gameObject.name == "Cost")
            {
                text.text = rangeMaxed ? "MAX" : rangeLevels[rangeLevel].cost + "$";
            }
        }
    
    }

    void UpgradeRange()
    {
        if (rangeLevel >= rangeLevels.Length)
            return;

        RangeLevel next = rangeLevels[rangeLevel];
        if (!TrySpend(next.cost))
            return;

        lightBeam.SetSpotAngles(next.innerSpotAngle, next.outerSpotAngle);
        rangeLevel++;
        RefreshUpgradeButtons();
    }

    void UpgradeBeamSpeed()
    {

    }

    void UpgradeExtraBeam()
    {

    }

    void UpgradeLitBoatSpeed()
    {

    }
}
