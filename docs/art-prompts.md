# AI 생성 지시문 (초안)

[아트 명세서](art-spec.md)의 스프라이트를 AI로 생성할 때 쓰는 지시문이다. 생성한 이미지는 [sprite-prep](../tools/sprite-prep/README.md)으로 정리해서 256×256 픽셀아트로 만든다.

**상태: 초안.** 첫 시험 생성 결과를 보고 함께 고친다. AI 도구가 정해지면 그 도구에 맞게 다듬는다.

## 작업 순서

1. **기준 그림 만들기**: 한손검(`class_sword_1h`)을 먼저 여러 번 생성해서 화풍이 마음에 드는 한 장을 고른다.
2. **기준 고정**: 고른 그림을 이후 모든 생성에 **스타일 참조 이미지**로 넣는다. 도구에 참조 기능이 없으면 같은 시드와 같은 공통 지시문을 쓴다.
3. **나머지 생성**: 공통 지시문 + 스프라이트별 지시문으로 생성한다.
4. **정리**: `sprite_prep.py process`로 정리하고, `sheet`로 나란히 놓아 키와 발 위치를 비교한다.
5. **팔레트 통일**: 마음에 드는 스프라이트들로 `palette`를 만들고, 모두 `--palette`로 다시 정리한다.

## 생성 설정

- 크기: 1024×1024 정사각형
- 배경: 단색 마젠타(#FF00FF). 정리 도구가 배경을 지우기 쉽고, 캐릭터에 잘 쓰이지 않는 색이다
- 한 장에 한 캐릭터, 전신, 무기 끝까지 프레임 안에

## 공통 지시문

모든 스프라이트 앞에 붙인다. 영어로 쓰는 편이 대부분의 도구에서 결과가 좋다.

```
pixel art game sprite, single character, full body, 4 heads tall proportions,
strict side view facing right, standing on an invisible flat ground line,
90s Japanese tactical RPG battle sprite style, medieval fantasy mercenary,
crisp pixels, clean dark outline, limited palette of about 32 colors,
no anti-aliasing, no gradients, no blur,
whole body and the entire weapon inside the frame with some margin,
solid flat magenta #FF00FF background, no ground shadow, no text, no border
```

참고 게임 이름(랑그릿사)은 지시문에 넣지 않는다. 특정 작품의 그림을 따라 그리게 될 수 있어서다. 대신 "90s Japanese tactical RPG battle sprite style"로 분위기만 전한다.

## 피해야 할 것

도구가 제외 지시문(negative prompt)을 받으면 넣는다.

```
3d render, realistic, painterly, smooth shading, gradient, blurry, anti-aliased,
front view, three-quarter view, multiple characters, cropped weapon, cropped feet,
background scenery, ground shadow, text, watermark, frame
```

## 스프라이트별 지시문

[명세서의 자세](art-spec.md#병종-스프라이트-25종)를 지시문으로 옮긴 것이다. 공통 지시문 뒤에 붙인다.

### 병종 (25종)

| 파일 | 무기 종류 | 지시문 |
|---|---|---|
| `class_sword_1h` | 한손검 | a disciplined infantryman holding a round shield in front and a one-handed sword, body half-turned, sword point aimed past the shield edge, calm and skillful stance |
| `class_sword_2h` | 양손검 | an executioner-like warrior holding a long two-handed greatsword angled down or resting on the shoulder, legs wide apart, heavy and cold demeanor |
| `class_spear_1h` | 한손창 | a nimble skirmisher holding a small round shield in front and a short spear raised over the shoulder as if about to throw it, weight on the front foot, light gear |
| `class_spear_2h` | 양손창 | a phalanx soldier bracing a long pike low and forward with both hands, back foot planted firmly, unyielding defensive stance |
| `class_axe_1h` | 한손도끼 | a raider with a battered wooden shield, leaning forward, a hand axe hanging low, glaring at the enemy like a hunter smelling blood |
| `class_axe_2h` | 양손도끼 | a berserker raising a huge two-handed axe overhead, teeth bared, eager to charge into battle right now, minimal armor |
| `class_mace_1h` | 한손둔기 | a stocky sturdy fighter covering the body with a shield, holding a flanged mace at shoulder height ready to smash down |
| `class_mace_2h` | 양손둔기 | a huge brute leaning on the handle of an enormous warhammer resting on the ground, relaxed show of raw strength |
| `class_shortbow` | 단궁 | an agile scout crouching low while nocking an arrow on a short bow, sense of quick movement |
| `class_longbow` | 장궁 | a sniper standing perfectly upright, drawing a tall longbow to full draw and aiming far away, quiet focus |
| `class_crossbow` | 석궁 | a crossbowman holding a crossbow braced against the shoulder and aiming, soldierly discipline |
| `class_arbalest` | 쇠뇌 | a siege soldier with a heavy arbalest resting on a wooden stand, turning its winding crank, slow but powerful |
| `class_sling` | 슬링 | a lightly dressed wanderer whirling a sling overhead, playful mischievous attitude |
| `class_musket` | 머스킷 | a line infantryman aiming a musket with a fixed bayonet forward, disciplined posture |
| `class_handcannon` | 핸드캐논 | a gunner holding a short thick hand cannon under the arm, lighting the fuse, smoke, rough and reckless |
| `class_fire_wand` | 화염 마법봉 | a hot-tempered fire mage swinging a short wand forward, sparks of fire flying, aggressive |
| `class_fire_staff` | 화염 지팡이 | a fire mage raising a long staff high to summon a pillar of fire, cloak blown by the heat, destructive |
| `class_frost_wand` | 냉기 마법봉 | a cold precise frost mage pointing a short wand straight ahead, frost forming at its tip |
| `class_frost_staff` | 냉기 지팡이 | a frost mage standing still with a long staff planted on the ground, a small blizzard swirling around, imposing |
| `class_lightning_wand` | 전기 마법봉 | a lightning mage thrusting a short wand forward, hair standing on end, excited energy |
| `class_lightning_staff` | 전기 지팡이 | a lightning mage raising a long staff to the sky, lightning striking its tip |
| `class_healing_rod` | 치유봉 | a caring healer holding a short holy rod close to the chest with both hands, soft light |
| `class_regen_rod` | 재생봉 | a calm cleric holding a holy rod low, gentle light spreading like ripples at the feet |
| `class_barrier_rod` | 보호막봉 | a guardian cleric thrusting a holy rod forward to raise a wall of light, legs firmly planted, defensive stance |
| `class_blessing_rod` | 축복봉 | an uplifted cleric raising a holy rod high, mouth open as if singing a hymn |
| `class_none` | 무기 없음 | an unarmed apprentice in simple clothes, fists clenched, standing a bit awkwardly |

### 적 전용 (4종)

| 파일 | 대상 | 지시문 |
|---|---|---|
| `unit_skeleton_sword_1h` | 망자 보병 | an undead skeleton soldier in rusted armor holding a round shield in front and a notched one-handed sword, faint glow in the eye sockets |
| `unit_skeleton_shortbow` | 망자 궁수 | an undead skeleton archer in rotten leather crouching and nocking an arrow on a short bow, faint glow in the eye sockets |
| `unit_dog` | 들개 | a starving feral dog, mangy fur, baring its teeth, side view facing right |
| `unit_wraith_rider` | 망령 기수 | a ghostly wraith knight riding a spectral horse, translucent pale body, holding a sword, side view facing right |

들개와 망령 기수는 4등신 사람이 아니므로 공통 지시문의 `4 heads tall proportions`를 빼고 쓴다. 망령 기수는 말까지 들어가야 하므로 다른 스프라이트보다 크게 나올 수 있다. 크기는 `sheet`로 비교해서 정한다.

## 프로토타입 범위 (미결정)

| 묶음 | 스프라이트 | 쓰이는 곳 |
|---|---|---|
| 필수 (5장) | `class_sword_1h`, `class_shortbow`, `class_fire_staff`, `class_healing_rod`, `unit_dog` | 신입 대원 4명 전원, 근교 숲의 적 전부(들개, 도적, 밀렵꾼) |
| 추가 (3장) | `unit_skeleton_sword_1h`, `unit_skeleton_shortbow`, `unit_wraith_rider` | 재의 왕도 |

신입 대원 4명의 시작 무기(한손검, 단궁, 화염 지팡이, 치유봉)가 적의 대표 그림 4종과 같아서, 필수 5장으로 처음 플레이하는 구간이 모두 채워진다.
