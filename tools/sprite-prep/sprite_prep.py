"""AI가 만든 도트풍 이미지를 게임에 넣을 256×256 픽셀아트 스프라이트로 정리한다.

명령:
  process  원본 이미지를 정리한다 (배경 제거 → 도트 격자 맞추기 → 크기 맞추기 → 색 줄이기 → 캔버스 배치)
  palette  정리한 스프라이트들에서 공용 팔레트를 만든다
  recolor  팔레트 색을 다른 색으로 바꾼다 (적 세력 색 등)
  sheet    여러 스프라이트를 한 줄로 늘어놓아 크기와 발 위치를 비교한다

자세한 사용법은 같은 폴더의 README.md를 본다.
"""

from __future__ import annotations

import argparse
import json
import sys
import warnings
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

CANVAS = 256
MARGIN = 8  # 캔버스 위·좌·우 여백
BASELINE = CANVAS - 8  # 발이 닿는 줄 (이 줄 바로 위까지 그린다)


# ---------------------------------------------------------------- 배경 제거


def remove_background(img: Image.Image, tolerance: int, holes: bool) -> Image.Image:
    """투명 배경이면 그대로 두고, 아니면 네 모서리에서 이어진 비슷한 색을 지운다.

    holes면 이어지지 않은 곳(팔과 몸 사이 등)의 같은 색도 지운다.
    """
    rgba = np.array(img.convert("RGBA"))
    alpha = rgba[:, :, 3]
    if (alpha < 255).mean() > 0.05:
        return Image.fromarray(rgba)

    h, w = alpha.shape
    rgb = rgba[:, :, :3].astype(np.int32)
    corners = [rgb[0, 0], rgb[0, w - 1], rgb[h - 1, 0], rgb[h - 1, w - 1]]
    background = np.zeros((h, w), dtype=bool)
    seen = np.zeros((h, w), dtype=bool)
    queue = deque((y, x) for y, x in [(0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)])
    for y, x in queue:
        seen[y, x] = True

    def close(color: np.ndarray) -> bool:
        return any(np.abs(color - c).max() <= tolerance for c in corners)

    while queue:
        y, x = queue.popleft()
        if not close(rgb[y, x]):
            continue
        background[y, x] = True
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and not seen[ny, nx]:
                seen[ny, nx] = True
                queue.append((ny, nx))

    if holes:
        diff = np.stack([np.abs(rgb - c).max(axis=2) for c in corners]).min(axis=0)
        background |= diff <= tolerance
    rgba[background, 3] = 0
    return Image.fromarray(rgba)


# ---------------------------------------------------------------- 도트 격자


def _edges(rgba: np.ndarray, min_diff: int) -> tuple[np.ndarray, np.ndarray]:
    """색이 크게 바뀌는 경계의 x 위치(가로 방향)와 y 위치(세로 방향)를 돌려준다.

    압축으로 번진 경계는 이웃한 두세 줄에 걸치므로, 변화량이 가장 큰 줄 하나만 경계로 친다.
    """
    rgb = rgba[:, :, :3].astype(np.int32)
    alpha = np.where(rgba[:, :, 3] >= 128, 255, 0)

    def peaks(diff: np.ndarray) -> np.ndarray:
        # diff의 마지막 축을 따라 이웃보다 크거나 같은 지점(왼쪽과 같으면 앞쪽 하나만)만 남긴다.
        left = np.concatenate([np.zeros_like(diff[..., :1]), diff[..., :-1]], axis=-1)
        right = np.concatenate([diff[..., 1:], np.zeros_like(diff[..., :1])], axis=-1)
        return (diff >= min_diff) & (diff > left) & (diff >= right)

    dx = np.maximum(np.abs(rgb[:, 1:] - rgb[:, :-1]).max(axis=2), np.abs(alpha[:, 1:] - alpha[:, :-1]))
    dy = np.maximum(np.abs(rgb[1:, :] - rgb[:-1, :]).max(axis=2), np.abs(alpha[1:, :] - alpha[:-1, :]))
    xs = np.nonzero(peaks(dx))[1] + 1
    ys = np.nonzero(peaks(dy.T))[1] + 1
    return xs, ys


def _grid_score(xs: np.ndarray, ys: np.ndarray, k: int) -> tuple[float, int, int]:
    """경계가 k 간격의 같은 줄에 몰린 비율과, 그 줄의 위치(ox, oy)."""
    bx = np.bincount(xs % k, minlength=k)
    by = np.bincount(ys % k, minlength=k)
    return (bx.max() + by.max()) / (len(xs) + len(ys)), int(bx.argmax()), int(by.argmax())


def detect_grid(img: Image.Image, max_k: int = 16, threshold: float = 0.7, min_diff: int = 40) -> int:
    """가짜 도트 한 칸의 크기(원본 픽셀 수)를 추정한다. 못 찾으면 1.

    색이 크게 바뀌는 경계가 k 간격의 같은 줄에 몰려 있으면 k는 도트 칸 크기(또는 그 약수)다.
    그런 k 중 가장 큰 것을 고른다. 넓은 단색 면과 자잘한 잡음은 경계로 치지 않는다.
    """
    xs, ys = _edges(np.array(img.convert("RGBA")), min_diff)
    if len(np.unique(xs)) + len(np.unique(ys)) < 24:
        # 경계 줄이 너무 적으면 우연히 간격이 맞을 수 있어 믿을 수 없다.
        return 1
    best = 1
    for k in range(2, max_k + 1):
        if _grid_score(xs, ys, k)[0] >= threshold:
            best = k
    return best


def snap_to_grid(img: Image.Image, k: int, min_diff: int = 40) -> Image.Image:
    """가짜 도트 k×k 칸을 진짜 픽셀 하나로 줄인다.

    칸의 절반 이상이 불투명하면 불투명 픽셀들의 중앙값 색을 쓴다. 잡음과 경계의 번짐에 강하다.
    """
    if k <= 1:
        return img
    rgba = np.array(img.convert("RGBA"))
    xs, ys = _edges(rgba, min_diff)
    _, ox, oy = _grid_score(xs, ys, k) if len(xs) + len(ys) else (0, 0, 0)

    # 경계 줄이 칸의 왼쪽·위쪽 끝이 되도록 앞을 비운다.
    pad_y, pad_x = (k - oy) % k, (k - ox) % k
    h, w = rgba.shape[:2]
    rows, cols = -(-(h + pad_y) // k), -(-(w + pad_x) // k)
    padded = np.zeros((rows * k, cols * k, 4), dtype=np.uint8)
    padded[pad_y : pad_y + h, pad_x : pad_x + w] = rgba

    blocks = padded.reshape(rows, k, cols, k, 4).transpose(0, 2, 1, 3, 4).reshape(rows, cols, k * k, 4)
    opaque = blocks[..., 3] >= 128
    keep = opaque.mean(axis=2) >= 0.5
    rgb = np.where(opaque[..., None], blocks[..., :3].astype(np.float32), np.nan)
    with warnings.catch_warnings():
        # 완전히 투명한 칸은 중앙값이 없어 경고가 난다. 그 칸은 어차피 버린다.
        warnings.simplefilter("ignore", category=RuntimeWarning)
        median = np.nanmedian(rgb, axis=2)

    out = np.zeros((rows, cols, 4), dtype=np.uint8)
    out[..., :3] = np.where(keep[..., None], np.nan_to_num(median), 0).round().astype(np.uint8)
    out[..., 3] = np.where(keep, 255, 0)
    return Image.fromarray(out)


# ---------------------------------------------------------------- 크기와 배치


def crop_to_subject(img: Image.Image) -> Image.Image:
    box = img.getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox()
    if box is None:
        raise SystemExit("이미지에 남은 대상이 없다. 배경 제거 허용치(--tolerance)를 낮춰 본다.")
    return img.crop(box)


def resize_subject(img: Image.Image, scale: float | None) -> Image.Image:
    """캔버스 여백 안에 들어가도록 줄인다. scale을 주면 그 배율을 쓴다."""
    w, h = img.size
    if scale is None:
        scale = min((CANVAS - 2 * MARGIN) / w, (BASELINE - MARGIN) / h, 1.0)
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    if (nw, nh) == (w, h):
        return img
    if nw > CANVAS - 2 * MARGIN or nh > BASELINE - MARGIN:
        raise SystemExit(f"배율 {scale}이면 {nw}×{nh}px가 되어 캔버스를 넘는다.")
    # 도트를 섞지 않도록 가장 가까운 픽셀로 줄인다.
    return img.resize((nw, nh), Image.NEAREST)


def place_on_canvas(img: Image.Image, dx: int) -> Image.Image:
    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    w, h = img.size
    x = (CANVAS - w) // 2 + dx
    y = BASELINE - h
    canvas.paste(img, (x, y), img)
    return canvas


# ---------------------------------------------------------------- 색


def load_palette(path: Path) -> list[tuple[int, int, int]]:
    rgba = np.array(Image.open(path).convert("RGBA")).reshape(-1, 4)
    colors: list[tuple[int, int, int]] = []
    for r, g, b, a in rgba.tolist():
        if a >= 128 and (r, g, b) not in colors:
            colors.append((r, g, b))
    if not colors:
        raise SystemExit(f"{path}: 팔레트에 색이 없다.")
    return colors


def quantize(img: Image.Image, palette: list[tuple[int, int, int]] | None, colors: int) -> Image.Image:
    """디더링 없이 색을 줄인다. 반투명은 기준(128)으로 완전 투명 또는 불투명으로 나눈다."""
    rgba = np.array(img.convert("RGBA"))
    opaque = rgba[:, :, 3] >= 128
    rgb = Image.fromarray(rgba[:, :, :3])

    if palette is None:
        q = rgb.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    else:
        pal_img = Image.new("P", (1, 1))
        flat = [c for color in palette for c in color]
        pal_img.putpalette(flat + flat[:3] * (256 - len(palette)))
        q = rgb.quantize(palette=pal_img, dither=Image.Dither.NONE)

    out = np.array(q.convert("RGBA"))
    out[:, :, 3] = np.where(opaque, 255, 0)
    return Image.fromarray(out)


def merge_close_colors(img: Image.Image, distance: int) -> Image.Image:
    """거의 같은 색들을 많이 쓰인 색 하나로 합친다. 잡음 때문에 생긴 비슷한 색을 정리한다."""
    if distance <= 0:
        return img
    rgba = np.array(img)
    opaque = rgba[:, :, 3] > 0
    colors, counts = np.unique(rgba[opaque][:, :3], axis=0, return_counts=True)
    order = np.argsort(-counts)
    kept: list[np.ndarray] = []
    target = {}
    for i in order:
        c = colors[i].astype(np.int32)
        for k in kept:
            if np.abs(c - k).max() <= distance:
                target[tuple(c)] = k
                break
        else:
            kept.append(c)
    for src, dst in target.items():
        hit = opaque & (rgba[:, :, 0] == src[0]) & (rgba[:, :, 1] == src[1]) & (rgba[:, :, 2] == src[2])
        rgba[hit, :3] = dst
    return Image.fromarray(rgba)


def remove_specks(img: Image.Image) -> Image.Image:
    """상하좌우에 불투명 이웃이 하나도 없는 외톨이 픽셀을 지운다."""
    rgba = np.array(img)
    a = rgba[:, :, 3] > 0
    p = np.pad(a, 1)
    neighbours = p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    rgba[a & ~neighbours, 3] = 0
    return Image.fromarray(rgba)


def used_colors(img: Image.Image) -> int:
    rgba = np.array(img)
    opaque = rgba[rgba[:, :, 3] > 0][:, :3]
    return len({tuple(c) for c in opaque})


# ---------------------------------------------------------------- 명령


def cmd_process(args: argparse.Namespace) -> None:
    palette = load_palette(args.palette) if args.palette else None
    img = Image.open(args.input)
    img = remove_background(img, args.tolerance, args.holes)
    img = crop_to_subject(img)

    if args.grid == "auto":
        k = detect_grid(img)
    else:
        k = int(args.grid)
    img = snap_to_grid(img, k)
    img = crop_to_subject(img)
    img = resize_subject(img, args.scale)
    img = quantize(img, palette, args.colors)
    if palette is None:
        img = merge_close_colors(img, args.merge)
    img = remove_specks(img)
    img = place_on_canvas(img, args.dx)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    img.save(args.output)
    box = img.getbbox()
    print(
        f"{args.output}: 도트 칸 {k}px, 대상 {box[2] - box[0]}×{box[3] - box[1]}px, "
        f"색 {used_colors(img)}개"
    )


def cmd_palette(args: argparse.Namespace) -> None:
    pixels = []
    for path in args.inputs:
        rgba = np.array(Image.open(path).convert("RGBA"))
        pixels.append(rgba[rgba[:, :, 3] >= 128][:, :3])
    if not pixels or sum(len(p) for p in pixels) == 0:
        raise SystemExit("색을 뽑을 픽셀이 없다.")
    allpx = np.concatenate(pixels)[None, :, :]
    q = Image.fromarray(allpx.astype(np.uint8)).quantize(
        colors=args.colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE
    )
    flat = q.getpalette()[: args.colors * 3]
    colors = sorted({tuple(flat[i : i + 3]) for i in range(0, len(flat), 3)}, key=_luma)

    strip = Image.new("RGBA", (len(colors), 1))
    strip.putdata([(*c, 255) for c in colors])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    strip.save(args.output)
    print(f"{args.output}: {len(colors)}색")
    for c in colors:
        print(f"  #{c[0]:02x}{c[1]:02x}{c[2]:02x}")


def _luma(c: tuple[int, int, int]) -> float:
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def cmd_recolor(args: argparse.Namespace) -> None:
    mapping_raw = json.loads(args.map.read_text(encoding="utf-8"))
    mapping = {_hex(k): _hex(v) for k, v in mapping_raw.items()}
    rgba = np.array(Image.open(args.input).convert("RGBA"))
    out = rgba.copy()
    for src, dst in mapping.items():
        hit = (rgba[:, :, 0] == src[0]) & (rgba[:, :, 1] == src[1]) & (rgba[:, :, 2] == src[2]) & (rgba[:, :, 3] > 0)
        out[hit, :3] = dst
    args.output.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out).save(args.output)
    print(f"{args.output}: {len(mapping)}색 바꿈")


def _hex(s: str) -> tuple[int, int, int]:
    s = s.lstrip("#")
    if len(s) != 6:
        raise SystemExit(f"색은 #rrggbb로 쓴다: {s}")
    return int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16)


def cmd_sheet(args: argparse.Namespace) -> None:
    sprites = [Image.open(p).convert("RGBA") for p in args.inputs]
    for p, s in zip(args.inputs, sprites):
        if s.size != (CANVAS, CANVAS):
            raise SystemExit(f"{p}: {CANVAS}×{CANVAS}이 아니다. process로 먼저 정리한다.")
    zoom = args.zoom
    sheet = Image.new("RGBA", (CANVAS * len(sprites) * zoom, CANVAS * zoom), (20, 22, 28, 255))
    draw = ImageDraw.Draw(sheet)
    for i, s in enumerate(sprites):
        big = s.resize((CANVAS * zoom, CANVAS * zoom), Image.NEAREST)
        sheet.paste(big, (i * CANVAS * zoom, 0), big)
        draw.rectangle([i * CANVAS * zoom, 0, (i + 1) * CANVAS * zoom - 1, CANVAS * zoom - 1], outline=(48, 54, 66, 255))
    # 발 기준선과, 첫 스프라이트의 머리 높이선
    draw.line([0, BASELINE * zoom, sheet.width, BASELINE * zoom], fill=(111, 168, 255, 255))
    top = sprites[0].getbbox()
    if top:
        draw.line([0, top[1] * zoom, sheet.width, top[1] * zoom], fill=(255, 122, 107, 255))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.output)
    print(f"{args.output}: {len(sprites)}개")


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(prog="sprite_prep", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("process", help="원본 이미지를 256×256 스프라이트로 정리한다")
    p.add_argument("input", type=Path)
    p.add_argument("output", type=Path)
    p.add_argument("--palette", type=Path, help="공용 팔레트 PNG. 없으면 이미지마다 --colors개로 줄인다")
    p.add_argument("--colors", type=int, default=32, help="팔레트가 없을 때 쓸 색 수 (기본 32)")
    p.add_argument("--merge", type=int, default=16, help="팔레트가 없을 때, 채널 차이가 이 값 이하인 색을 합친다 (기본 16, 0이면 안 합침)")
    p.add_argument("--grid", default="auto", help="가짜 도트 한 칸의 크기(원본 px). auto면 추정한다 (기본 auto)")
    p.add_argument("--scale", type=float, help="격자를 맞춘 뒤 곱할 배율. 없으면 캔버스에 맞게 줄인다")
    p.add_argument("--dx", type=int, default=0, help="가운데에서 좌우로 옮길 픽셀 수")
    p.add_argument("--tolerance", type=int, default=24, help="배경색으로 볼 색 차이 (기본 24)")
    p.add_argument("--holes", action="store_true", help="모서리와 이어지지 않은 곳의 배경색도 지운다")
    p.set_defaults(func=cmd_process)

    p = sub.add_parser("palette", help="정리한 스프라이트들에서 공용 팔레트를 만든다")
    p.add_argument("output", type=Path)
    p.add_argument("inputs", type=Path, nargs="+")
    p.add_argument("--colors", type=int, default=32)
    p.set_defaults(func=cmd_palette)

    p = sub.add_parser("recolor", help="팔레트 색을 바꾼다")
    p.add_argument("input", type=Path)
    p.add_argument("output", type=Path)
    p.add_argument("--map", type=Path, required=True, help='{"#원래색": "#바꿀색"} 형식의 JSON')
    p.set_defaults(func=cmd_recolor)

    p = sub.add_parser("sheet", help="스프라이트를 한 줄로 늘어놓아 비교한다")
    p.add_argument("output", type=Path)
    p.add_argument("inputs", type=Path, nargs="+")
    p.add_argument("--zoom", type=int, default=1)
    p.set_defaults(func=cmd_sheet)

    args = parser.parse_args(argv)
    args.func(args)


if __name__ == "__main__":
    main(sys.argv[1:])
