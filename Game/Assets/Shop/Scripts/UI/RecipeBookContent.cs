namespace DdukDduk.Shop
{
    /// <summary>Editable story text for the four recipe-book spreads. Gameplay prices live in ShopModel.</summary>
    public sealed class RecipeBookEntry
    {
        public int ProductIndex;
        public string Subtitle, Opening, Ingredients, Experiments, Conclusion, Letter, ResearchDescription;
    }

    public static class RecipeBookContent
    {
        public static readonly RecipeBookEntry[] Entries =
        {
            new RecipeBookEntry
            {
                ProductIndex = 5, Subtitle = "봄을 담은 떡",
                Opening = "봄에 나는 어린 쑥으로 만든 떡.\n은은하게 퍼지는 향이\n입안 가득 봄을 데려오는 것 같지.",
                ResearchDescription = "봄의 향을 담은, 향긋한 쑥떡.\n은은한 쑥 향과 부드러운 팥앙금이\n어우러진 가문의 대표 떡이다.",
                Ingredients = "어린 쑥  ·  쌀가루  ·  소금\n쑥은 연한 잎을 골라 곱게 다듬는다.",
                Experiments = "첫 배합 / 향이 조금 약하다.\n두 번째 / 향과 쌀맛이 어울린다.\n세 번째 / 쓴맛이 앞선다.",
                Conclusion = "쌀맛을 남기고, 쑥 향은 은은하게.",
                Letter = "우리 딸아,\n봄이면 네가 먼저 찾던 떡이란다.\n이 향을 맡으면 우리 집 마당도\n함께 떠올려주렴.\n                         — 엄마가"
            },
            new RecipeBookEntry
            {
                ProductIndex = 6, Subtitle = "한입에 숨겨 둔 달콤함",
                Opening = "동그란 떡에 숨긴 작은 단맛.\n한입 베어 물면 단맛이 퍼져\n어느새 하나 더 집게 되는 떡.",
                ResearchDescription = "한입에 숨겨 둔 달콤한 꿀떡.\n말랑한 떡과 촉촉한 꿀이\n어우러져 작은 기쁨을 전한다.",
                Ingredients = "쌀가루  ·  꿀  ·  설탕\n속은 흘러나오지 않을 만큼만 채운다.",
                Experiments = "첫 배합 / 속이 많아 떡이 터졌다.\n두 번째 / 단맛이 오래 남는다.\n세 번째 / 한입에 좋은 달콤함.",
                Conclusion = "작게 빚고, 달콤함은 한입만큼.",
                Letter = "우리 딸아,\n지친 날에는 작은 단맛 하나가\n마음을 풀어주기도 한단다.\n오늘 누군가에게 그런 한입을\n건넬 수 있으면 좋겠구나.\n                         — 엄마가"
            },
            new RecipeBookEntry
            {
                ProductIndex = 7, Subtitle = "오래 남는 구수한 맛",
                Opening = "부드러운 떡에 고소한 콩.\n천천히 씹을수록 구수해지는\n자꾸 생각나는 소박한 떡.",
                ResearchDescription = "부드러운 떡에 콩을 담았다.\n씹을수록 구수한 맛이 퍼지는\n소박하고 든든한 우리 집 떡.",
                Ingredients = "쌀가루  ·  삶은 콩  ·  볶은 콩가루\n콩의 씹는 맛을 조금 남겨 둔다.",
                Experiments = "첫 배합 / 콩과 떡이 따로 논다.\n두 번째 / 씹는 재미가 없다.\n세 번째 / 부드러운 콩맛이 남는다.",
                Conclusion = "떡과 콩, 어느 쪽도 서두르지 말기.",
                Letter = "우리 딸아,\n자꾸 생각나는 맛이 있지.\n소박해도 오래 기억되는 맛.\n이 떡이 누군가에게\n그런 떡이 되었으면 좋겠구나.\n                         — 엄마가"
            },
            new RecipeBookEntry
            {
                ProductIndex = 8, Subtitle = "귀한 날을 위한 마지막 레시피",
                Opening = "소중한 사람을 대접하듯이.\n여러 재료의 맛을 한데 모아\n정성껏 빚는 마지막 레시피.",
                ResearchDescription = "귀한 날을 위해 정성껏 빚은 떡.\n대추와 밤, 잣의 풍미가 어울려\n소중한 사람을 대접하기 좋다.",
                Ingredients = "쌀가루  ·  대추  ·  밤  ·  잣\n재료마다 가진 맛을 소중히 다룬다.",
                Experiments = "첫 배합 / 대추 맛이 앞선다.\n두 번째 / 밤과 잣이 어울린다.\n세 번째 / 여러 맛이 조화롭다.",
                Conclusion = "귀한 재료보다, 귀하게 대하는 마음.",
                Letter = "우리 딸아,\n이 책의 마지막 장은\n이제 네 손으로 완성해주겠니?\n네가 빚는 떡에 담길 이야기를\n엄마도 오래 기다렸단다.\n                         — 엄마가"
            }
        };
    }
}
