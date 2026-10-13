# sprite-prep

AI가 만든 도트풍 이미지를 게임에 넣을 **256×256 픽셀아트 스프라이트**로 정리하는 도구다. 기준은 [docs/art-spec.md](../../docs/art-spec.md), 생성 지시문은 [docs/art-prompts.md](../../docs/art-prompts.md)에 있다.

## 설치

Python 3.10 이상.

```bash
pip install -r tools/sprite-prep/requirements.txt
```

## 하는 일

AI가 만드는 "픽셀아트"는 보통 1024px 이미지에 가짜 도트(원본 픽셀 여러 개가 모인 칸)를 그린 것이다. `process`는 이것을 다음 순서로 정리한다.

1. **배경 제거**: 이미 투명하면 그대로 둔다. 아니면 네 모서리에서 이어진 비슷한 색을 지운다. 그래서 단색 배경(마젠타 등)으로 생성하는 것이 좋다.
2. **도트 격자 맞추기**: 가짜 도트 한 칸의 크기를 추정해서(`--grid auto`) 칸 하나를 진짜 픽셀 하나로 줄인다. 칸 안의 색은 중앙값을 쓰므로 압축 잡음과 번진 경계에 강하다.
3. **크기 맞추기**: 캔버스(여백 8px)를 넘으면 가장 가까운 픽셀 방식으로 줄인다. 작으면 그대로 둔다. `--scale`로 배율을 직접 줄 수 있다.
4. **색 줄이기**: 디더링 없이 줄인다. `--palette`를 주면 그 팔레트의 색만 쓴다. 없으면 이미지마다 `--colors`개로 줄이고, 거의 같은 색은 합친다.
5. **외톨이 픽셀 제거**, **캔버스 배치**: 발을 아래 기준선(위에서 248px)에 맞추고 좌우 가운데에 둔다.

## 명령

```bash
T="python tools/sprite-prep/sprite_prep.py"

# 한 장 정리
$T process raw/class_sword_1h.png sprites/class_sword_1h.png

# 도트가 너무 작게 나오면 정수 배율로 키운다 (2배)
$T process raw/class_sword_1h.png sprites/class_sword_1h.png --scale 2

# 격자 추정이 틀리면 칸 크기를 직접 준다 (원본 4px이 도트 한 칸)
$T process raw/class_sword_1h.png sprites/class_sword_1h.png --grid 4

# 마음에 드는 스프라이트들로 공용 팔레트를 만든다
$T palette palette.png sprites/class_sword_1h.png sprites/class_shortbow.png --colors 32

# 공용 팔레트로 다시 정리한다 (모든 스프라이트의 색이 같은 팔레트를 쓴다)
$T process raw/class_sword_1h.png sprites/class_sword_1h.png --palette palette.png

# 적 세력 색으로 바꾼다. map.json: {"#3a5a8c": "#8c3a3a", ...}
$T recolor sprites/class_sword_1h.png sprites/enemy_bandit.png --map map.json

# 여러 장을 나란히 놓고 크기와 발 위치를 비교한다
$T sheet sheet.png sprites/*.png --zoom 2
```

`sheet` 이미지에는 파란 선(발 기준선)과 빨간 선(첫 스프라이트의 머리 높이)이 그려진다. 4등신 캐릭터들의 키가 이 선에 비슷하게 맞아야 한다. 맞지 않으면 그 이미지만 `--scale`로 다시 정리한다.

## 한계

- 격자 추정은 도트 칸 크기가 일정할 때 맞는다. AI가 칸 크기를 들쭉날쭉하게 그리면 `--grid`를 직접 주거나, 다시 생성하는 편이 낫다.
- 결과 해상도는 AI가 그린 도트의 밀도로 정해진다. 1024px 이미지에 4px 도트면 대상이 약 256px 이하로 나온다. 도트가 굵으면(8px 이상) 결과가 작게 나오므로 `--scale 2`로 키운다.
- 배경 제거는 기본으로 모서리와 이어진 영역만 지운다. 팔과 몸 사이처럼 갇힌 배경이 남으면 `--holes`를 준다. 이때 캐릭터 안의 같은 색도 지워지므로, 배경색은 캐릭터에 없는 색(마젠타)으로 생성한다.
