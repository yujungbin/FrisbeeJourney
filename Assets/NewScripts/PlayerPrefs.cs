using System;
using UnityEngine;

public class DiscProgressionStore : MonoBehaviour
{
    private const string CoinsKey = "DiscGame.Coins";
    private const string InitialThrustLevelKey = "DiscGame.Level.InitialThrust";
    private const string DurabilityLevelKey = "DiscGame.Level.Durability";
    private const string LiftLevelKey = "DiscGame.Level.Lift";

    [SerializeField] private DiscStatsConfig statsConfig;
    [SerializeField] private int defaultCoins = 0;

    public event Action OnChanged;

    public int Coins { get; private set; }
    public int InitialThrustLevel { get; private set; }
    public int DurabilityLevel { get; private set; }
    public int LiftLevel { get; private set; }

    public DiscStatsConfig StatsConfig => statsConfig;

    private void Awake()
    {
        Load();
    }

    public void Load()
    {
        Coins = PlayerPrefs.GetInt(CoinsKey, defaultCoins);
        InitialThrustLevel = PlayerPrefs.GetInt(InitialThrustLevelKey, 0);
        DurabilityLevel = PlayerPrefs.GetInt(DurabilityLevelKey, 0);
        LiftLevel = PlayerPrefs.GetInt(LiftLevelKey, 0);

        OnChanged?.Invoke();
    }

    public void Save()
    {
        PlayerPrefs.SetInt(CoinsKey, Coins);
        PlayerPrefs.SetInt(InitialThrustLevelKey, InitialThrustLevel);
        PlayerPrefs.SetInt(DurabilityLevelKey, DurabilityLevel);
        PlayerPrefs.SetInt(LiftLevelKey, LiftLevel);
        PlayerPrefs.Save();
    }

    public DiscRuntimeStats BuildRuntimeStats()
    {
        if (statsConfig == null)
        {
            Debug.LogError("DiscStatsConfig가 연결되어 있지 않습니다.");
            return new DiscRuntimeStats(18f, 100f, 0.65f);
        }

        return statsConfig.BuildRuntimeStats(
            InitialThrustLevel,
            DurabilityLevel,
            LiftLevel
        );
    }

    public int GetLevel(DiscStatType type)
    {
        switch (type)
        {
            case DiscStatType.InitialThrust:
                return InitialThrustLevel;

            case DiscStatType.Durability:
                return DurabilityLevel;

            case DiscStatType.Lift:
                return LiftLevel;

            default:
                return 0;
        }
    }

    public bool TryUpgrade(DiscStatType type)
    {
        if (statsConfig == null)
        {
            Debug.LogError("DiscStatsConfig가 연결되어 있지 않습니다.");
            return false;
        }

        DiscStatDefinition definition = statsConfig.GetDefinition(type);
        int currentLevel = GetLevel(type);
        int cost = definition.GetUpgradeCost(currentLevel);

        if (cost < 0)
        {
            Debug.Log($"{definition.displayName}은 이미 최대 레벨입니다.");
            return false;
        }

        if (Coins < cost)
        {
            Debug.Log($"코인이 부족합니다. 필요 코인: {cost}, 보유 코인: {Coins}");
            return false;
        }

        Coins -= cost;
        SetLevel(type, currentLevel + 1);

        Save();
        OnChanged?.Invoke();

        return true;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        Coins += amount;

        Save();
        OnChanged?.Invoke();
    }

    private void SetLevel(DiscStatType type, int level)
    {
        switch (type)
        {
            case DiscStatType.InitialThrust:
                InitialThrustLevel = level;
                break;

            case DiscStatType.Durability:
                DurabilityLevel = level;
                break;

            case DiscStatType.Lift:
                LiftLevel = level;
                break;
        }
    }
}