"""生成一键息屏用的 app.ico（多尺寸，32bpp DIB 条目，无需第三方库）。"""
import struct, os

BG = (0x26, 0x2C, 0x38)      # 深蓝灰
FG = (0xF0, 0xF4, 0xFA)      # 近白


def crescent(S):
    """返回 S*S 的 RGBA 像素（行优先，自上而下）。"""
    cx, cy = S * 0.5, S * 0.5
    R = S * 0.46
    # 挖去的圆（右上偏移），形成月牙
    ox, oy = cx + S * 0.20, cy - S * 0.13
    R2 = R * 0.82

    px = []
    for y in range(S):
        row = []
        for x in range(S):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            in_main = dx * dx + dy * dy <= R * R
            dx2, dy2 = x + 0.5 - ox, y + 0.5 - oy
            in_cut = dx2 * dx2 + dy2 * dy2 <= R2 * R2
            if in_main and not in_cut:
                row.append((BG[0], BG[1], BG[2], 255))
            else:
                row.append((0, 0, 0, 0))
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
    out = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'app.ico')
    size, n = build(out)
    print('written:', out, size, 'bytes,', n, 'sizes')
