namespace Triangle.Core.Items;

/// <summary>
/// 장비 한 점: 창고나 멤버의 부위에 있는 실제 물건 (2026-10-16). 같은 아이템도 한 점씩 따로 저장한다.
/// 재료는 한 점씩 두지 않고 창고에 개수로 둔다(<see cref="Progress.Company.Materials"/>).
/// - <see cref="Uid"/>: 회사 안에서 겹치지 않는 번호. 회사가 새 장비를 만들 때 붙인다(<see cref="Progress.Company.NextItemUid"/>).
/// - <see cref="Level"/>: 아이템 레벨 (1 이상). 원정에서 떨어진 장비는 그 지역의 깊이(난이도), 상점·제작·모집 장비는 티어다.
///   지금은 기록만 하고, 반복 파밍 계획의 무작위 옵션(2단위)이 옵션 값을 이 레벨에 비례하게 굴린다.
/// </summary>
public sealed record ItemInstance(int Uid, string ItemId, int Level);
