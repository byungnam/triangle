"""game-icons.net 아이콘을 게임용 흰색 PNG로 만든다 (게임에서 색을 입힌다).

준비: npm pack @iconify-json/game-icons && tar xzf iconify-json-game-icons-*.tgz
      pip install cairosvg
실행: python3 tools/icons/make_icons.py <package/icons.json 경로>

아이콘은 game-icons.net (CC BY 3.0, 저자 Lorc, Delapouite 외) 것이다. 출처는 Icons/CREDITS.md에 있다.
게임 이름 → game-icons 이름 표는 아래 ICONS에 있다. 바꾸면 다시 실행한다.
"""
import json
import sys
from pathlib import Path

import cairosvg

SIZE = 128
OUT = Path(__file__).resolve().parents[2] / "src" / "Triangle.Desktop" / "Icons"

ICONS = {
    # 무기·보조 종류 (게임의 ItemIcons가 아이템 type을 이 이름으로 바꾼다)
    "weapon.sword": "broadsword",
    "weapon.greatsword": "relic-blade",
    "weapon.spear": "barbed-spear",
    "weapon.halberd": "halberd",
    "weapon.hatchet": "hatchet",
    "weapon.greataxe": "battle-axe",
    "weapon.mace": "flanged-mace",
    "weapon.warhammer": "warhammer",
    "weapon.shortbow": "pocket-bow",
    "weapon.longbow": "bow-arrow",
    "weapon.crossbow": "crossbow",
    "weapon.arbalest": "crossbow",
    "weapon.sling": "sling",
    "weapon.musket": "musket",
    "weapon.handcannon": "pistol-gun",
    "weapon.wand": "crystal-wand",
    "weapon.staff": "wizard-staff",
    "weapon.holywand": "orb-wand",
    "offhand.shield": "round-shield",
    "offhand.tome": "spell-book",
    "offhand.relic": "holy-symbol",
    # 방어구 (부위 · 재질)
    "Head.plate": "visored-helm",
    "Head.leather": "robin-hood-hat",
    "Head.cloth": "hood",
    "Body.plate": "breastplate",
    "Body.leather": "leather-armor",
    "Body.cloth": "robe",
    "Feet.plate": "metal-boot",
    "Feet.leather": "leather-boot",
    "Feet.cloth": "boots",
    # 초상 (주무기 계열로 고른다)
    "portrait.melee": "black-knight-helm",
    "portrait.ranged": "bowman",
    "portrait.mage": "wizard-face",
    "portrait.healer": "sun-priest",
    "portrait.none": "hooded-figure",
    "figure": "person",
    # 스탯
    "stat.str": "biceps",
    "stat.dex": "feather",
    "stat.vital": "heart-plus",
    "stat.intel": "brain",
    "stat.speed": "run",
    "hp": "hearts",
    "mp": "crystal-ball",
    "delay": "hourglass",
    "defense": "checked-shield",
    "magic-defense": "magic-shield",
    "power": "sword-brandish",
    # 행동
    "action.melee": "sword-brandish",
    "action.sweep": "crossed-swords",
    "action.ranged": "arrow-flights",
    "action.shot": "bullseye",
    "action.fire": "fire-ray",
    "action.frost": "frozen-orb",
    "action.lightning": "heavy-lightning",
    "action.magic": "magic-swirl",
    "action.heal": "health-increase",
    "action.shield": "magic-shield",
    "action.mana": "crystal-ball",
    "action.summon": "ghost",
    "action.buff": "upgrade",
    "action.debuff": "broken-shield",
    # 기타
    "tactics": "scroll-unfurled",
    "equipment": "knapsack",
    "stats": "muscle-up",
    "mastery": "anvil",
    "lock": "padlock",
}


def main(icons_json: str) -> None:
    data = json.loads(Path(icons_json).read_text())
    width = data.get("width", 512)
    height = data.get("height", 512)
    OUT.mkdir(parents=True, exist_ok=True)
    for name, source in ICONS.items():
        body = data["icons"][source]["body"].replace("currentColor", "#ffffff")
        svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">{body}</svg>'
        cairosvg.svg2png(bytestring=svg.encode(), write_to=str(OUT / f"{name}.png"), output_width=SIZE, output_height=SIZE)

    credits = ["# 아이콘 출처", "", "모든 아이콘은 [game-icons.net](https://game-icons.net)의 것이며 "
               "[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) 라이선스다. "
               "흰색 PNG로 바꿔 게임에서 색을 입힌다 (`tools/icons/make_icons.py`).", "",
               "| 파일 | game-icons 이름 |", "|---|---|"]
    credits += [f"| `{name}.png` | {source} |" for name, source in ICONS.items()]
    (OUT / "CREDITS.md").write_text("\n".join(credits) + "\n")
    print(f"{len(ICONS)} icons -> {OUT}")


if __name__ == "__main__":
    main(sys.argv[1])
