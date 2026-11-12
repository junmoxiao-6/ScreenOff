"""生成一键息屏用的 app.ico（多尺寸，32bpp DIB 条目，无需第三方库）。

月牙用 8x8 超采样算覆盖率做抗锯齿——小尺寸（16/20/24）不加抗锯齿会糊成一团。
"""
import struct, os, math

BG = (0x26, 0x2C, 0x38)      # 深蓝灰
FG = (0xF0, 0xF4, 0xFA)      # 近白

SS = 8                        # 每像素超采样边数
EDGE = 1.5                    # 距边界小于该距离才做超采样


def _hit(px, py, cx, cy, R, ox, oy, R2):
    dx, dy = px - cx, py - cy
    if dx * dx + dy * dy > R * R:
        return False
    dx2, dy2 = px - ox, py - oy
    return dx2 * dx2 + dy2 * dy2 > R2 * R2


def crescent(S):
    """返回 S*S 的 RGBA 像素（行优先，自上而下），带覆盖率抗锯齿。

    月牙 = 大圆减去一个更大的偏移圆。挖空圆必须比大圆大，否则底部会留一圈
    厚环，看起来像「C」而不是月牙。
    """
    cx, cy = S * 0.5, S * 0.5
    R = S * 0.47

    # 小尺寸把月牙画粗一点，否则缩到 16px 只剩一条毛边
    k = 1.0
    if S <= 20:
        k = 1.14
    elif S <= 24:
        k = 1.08
    elif S <= 32:
        k = 1.04

    R2 = R * 1.25 / k
    ox = cx + R * 0.52
    oy = cy - R * 0.22

    px = []
    inv = 1.0 / (SS * SS)
    for y in range(S):
        row = []
        for x in range(S):
            mx, my = x + 0.5, y + 0.5
            d1 = math.hypot(mx - cx, my - cy)
            d2 = math.hypot(mx - ox, my - oy)

            if abs(d1 - R) > EDGE and abs(d2 - R2) > EDGE:
                # 远离边界，直接判定，省掉超采样
                a = 255 if _hit(mx, my, cx, cy, R, ox, oy, R2) else 0
            else:
                hit = 0
                for sy in range(SS):
                    py_ = y + (sy + 0.5) / SS
                    for sx in range(SS):
                        px_ = x + (sx + 0.5) / SS
                        if _hit(px_, py_, cx, cy, R, ox, oy, R2):
                            hit += 1
                a = int(round(255 * hit * inv))

            row.append((BG[0], BG[1], BG[2], a) if a else (0, 0, 0, 0))
        px.append(row)
    return px


def dib(S):
    """构造 ICO 里的单张图：BITMAPINFOHEADER + BGRA(bottom-up) + AND mask。"""
    px = crescent(S)
    header = struct.pack('<IiiHHIIiiII',
                         40, S, S * 2, 1, 32, 0, S * S * 4, 0, 0, 0, 0)

    xor = bytearray()
    for y in range(S - 1, -1, -1):          # bottom-up
        for x in range(S):
            r, g, b, a = px[y][x]
            xor += struct.pack('<BBBB', b, g, r, a)

    # AND mask：1 表示透明，每行按 4 字节对齐
    row_bytes = ((S + 31) // 32) * 4
    mask = bytearray()
    for y in range(S - 1, -1, -1):
        bits = bytearray(row_bytes)
        for x in range(S):
            if px[y][x][3] == 0:
                bits[x >> 3] |= (0x80 >> (x & 7))
        mask += bits

    return header + bytes(xor) + bytes(mask)


def build(path, sizes=(16, 20, 24, 32, 40, 48, 64, 128, 256)):
    images = [dib(s) for s in sizes]
    head = struct.pack('<HHH', 0, 1, len(sizes))
    offset = 6 + 16 * len(sizes)
    entries, blob = b'', b''
    for s, img in zip(sizes, images):
        w = 0 if s >= 256 else s
        entries += struct.pack('<BBBBHHII', w, w, 0, 0, 1, 32, len(img), offset)
        offset += len(img)
        blob += img
    with open(path, 'wb') as f:
        f.write(head + entries + blob)
    return os.path.getsize(path), len(sizes)


if __name__ == '__main__':
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, 'app.ico')
    size, n = build(out)
    print('written:', out, size, 'bytes,', n, 'sizes')
