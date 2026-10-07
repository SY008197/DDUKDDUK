using System;

namespace DdukDduk.Shop
{
    // Arrays of public fields remain compatible with Unity JsonUtility; runtime sets and dictionaries are private.
    [Serializable]
    public sealed class CommonDataSnapshot
    {
        public string[] completedResearchIds = new string[0];
        public MarketingWeekSnapshot[] usedThisWeek = new MarketingWeekSnapshot[0];
        public string[] firstUsed = new string[0];
        public MarketingApplyDaySnapshot[] applyDay = new MarketingApplyDaySnapshot[0];
        public AffectionSnapshot[] affection = new AffectionSnapshot[0];
        public string[] completedEventIds = new string[0];
        public Route currentRoute;
    }

    [Serializable]
    public sealed class MarketingWeekSnapshot
    {
        public string marketingId;
        public int week;
    }

    [Serializable]
    public sealed class MarketingApplyDaySnapshot
    {
        public string marketingId;
        public int day;
    }

    [Serializable]
    public sealed class AffectionSnapshot
    {
        public CharacterId id;
        public int amount;
    }
}
