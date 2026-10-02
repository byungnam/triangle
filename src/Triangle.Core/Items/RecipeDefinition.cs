namespace Triangle.Core.Items;

/// <summary>
/// 제작법 (<c>recipes.json</c>): 재료 아이템과 골드를 내고 장비 하나를 만든다.
/// 결과 아이템마다 제작법은 하나다(결과 아이템 ID로 찾는다).
/// </summary>
public sealed record RecipeDefinition
{
    /// <summary>만들어지는 장비의 아이템 ID.</summary>
    public required string Result { get; init; }

    public required IReadOnlyList<RecipeMaterial> Materials { get; init; }

    public int Gold { get; init; }
}

/// <param name="ItemId">재료 아이템 (부위 <see cref="EquipmentSlot.Material"/>).</param>
/// <param name="Count">필요한 개수 (1 이상).</param>
public sealed record RecipeMaterial(string ItemId, int Count);
