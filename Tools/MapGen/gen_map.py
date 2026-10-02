"""
Builds the 40x40 world of Age of Sakura into Assets/Resources/Definitions/game_definitions.json (map section only).

The starting valley (x 0..15, z 0..19) keeps the hand-made rows of the original 20x20 map, so existing saves and tests still fit.
Everything east and south of it is generated here from a fixed seed: a wide river that bends south-east into a lake, an east bank,
a southern meadow, forests, cherry groves and rim forest. Run again to regenerate; the result is deterministic.

Legend: . grass  , dirt  ~ water  = bridge  T tree  C cherry  B bamboo  R rock  f/s/g/r grass with flowers/shrub/tuft/small rock
"""
import json, math, random, os

W = H = 40
PATH = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'Definitions', 'game_definitions.json')

with open(PATH, encoding='utf-8') as f:
    data = json.load(f)
old = data['map']['rows'][:20] if len(data['map']['rows']) == 20 else None
if old is None:
    # regenerate from the stored starting valley (first 20 rows, first 16 columns)
    old = [r[:20] for r in data['map']['rows'][:20]]
valley = [r[:16] for r in old]

rng = random.Random(2024)
g = [['.'] * W for _ in range(H)]

# ---- value noise for forest clusters
def noise_grid(cell, seed):
    r = random.Random(seed)
    n = (W * 2 // cell) + 4
    return [[r.random() for _ in range(n)] for _ in range(n)]
NG = noise_grid(3, 11)
def noise(x, z, cell=5):
    gx, gz = x / cell, z / cell
    x0, z0 = int(gx), int(gz)
    fx, fz = gx - x0, gz - z0
    fx = fx * fx * (3 - 2 * fx); fz = fz * fz * (3 - 2 * fz)
    a = NG[z0][x0]; b = NG[z0][x0 + 1]; c = NG[z0 + 1][x0]; d = NG[z0 + 1][x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fz) + (c * (1 - fx) + d * fx) * fz

# ---- water: river from the north edge, bending south-east into a lake
river = {}  # z -> (xmin, xmax)
def span(z0, z1, a0, b0, a1, b1):
    for z in range(z0, z1 + 1):
        t = (z - z0) / max(1, z1 - z0)
        river[z] = (round(a0 + (a1 - a0) * t), round(b0 + (b1 - b0) * t))
span(0, 2, 17, 20, 17, 20)
river[3] = (16, 20)
river[4] = (15, 19)
river[5] = (15, 19)
span(6, 9, 16, 19, 16, 19)
span(10, 15, 16, 20, 17, 21)
span(16, 19, 17, 21, 20, 24)
span(20, 24, 20, 24, 22, 27)

LAKE = (29.0, 30.5, 9.5, 6.5)  # centre x, centre z, radius x, radius z
water = set()
for z, (a, b) in river.items():
    for x in range(a, b + 1): water.add((x, z))
for z in range(H):
    for x in range(W):
        dx = (x + 0.5 - LAKE[0]) / LAKE[2]; dz = (z + 0.5 - LAKE[1]) / LAKE[3]
        wobble = 0.12 * math.sin(x * 0.9) + 0.1 * math.cos(z * 1.1)
        if dx * dx + dz * dz < 1.0 + wobble: water.add((x, z))
# join the river's last rows to the lake
for z in range(24, 27):
    for x in range(22, 31): water.add((x, z))

def near_water(x, z, r=1):
    return any((x + dx, z + dz) in water for dx in range(-r, r + 1) for dz in range(-r, r + 1))

# ---- land: forest clusters, open meadows near the play areas
def play_area(x, z):
    if 0 <= x <= 19 and 0 <= z <= 19: return True            # starting valley (hand made)
    if 4 <= x <= 17 and 16 <= z <= 29: return True            # southern meadow (town hall level 2)
    if 20 <= x <= 34 and 4 <= z <= 22: return True            # east bank (town hall level 3)
    return False

for z in range(H):
    for x in range(W):
        if (x, z) in water:
            g[z][x] = '~'; continue
        edge = min(x, z, W - 1 - x, H - 1 - z)
        n = noise(x, z) * 0.7 + noise(x * 1.7 + 9, z * 1.7 + 4, 3) * 0.3
        play = play_area(x, z)
        p_tree = 0.80 if edge < 3 else (0.18 if play else 0.42)
        p_tree *= (0.35 + 1.5 * n) if play else (0.5 + n)
        if near_water(x, z) and not near_water(x, z, 0): p_tree *= 0.4
        r = rng.random()
        if r < p_tree:
            g[z][x] = 'T' if rng.random() < 0.82 else ('B' if rng.random() < 0.6 else 'R')
        else:
            q = rng.random()
            g[z][x] = 'f' if q < 0.05 else 's' if q < 0.12 else 'g' if q < 0.22 else '.' if q < 0.97 else 'r'

# ---- cherry groves and bamboo thickets
def grove(cx, cz, n, ch):
    for _ in range(n):
        x = int(cx + rng.gauss(0, 1.4)); z = int(cz + rng.gauss(0, 1.4))
        if 0 <= x < W and 0 <= z < H and (x, z) not in water and not (x <= 15 and z <= 19): g[z][x] = ch
grove(25, 13, 7, 'C'); grove(11, 24, 6, 'C'); grove(31, 8, 5, 'C'); grove(7, 33, 5, 'C')
grove(23, 19, 6, 'B'); grove(14, 28, 5, 'B'); grove(34, 17, 5, 'B')

# ---- dirt paths: from the bridge across the east bank, and a lane through the southern meadow
def dirt(cells):
    for x, z in cells:
        if (x, z) not in water and g[z][x] not in 'T': g[z][x] = ','
for x in range(20, 32): dirt([(x, 9), (x, 10)] if x < 27 else [(x, 9 - (x - 26) // 2 * 1)])
for z in range(16, 29): dirt([(9, z), (10, z)])
for x in range(10, 18): dirt([(x, 22)])

# ---- bridges: the old one, extended across the wide river, and a second one in the south
for x in range(16, 20): g[9][x] = '='
for x in range(20, 21): g[9][x] = ','
for x in range(river[21][0], river[21][1] + 1): g[21][x] = '='
dirt([(river[21][0] - 1, 21), (river[21][0] - 2, 21), (river[21][1] + 1, 21), (river[21][1] + 2, 21)])
# keep the bridge approaches clear
for (bx, bz) in [(15, 9), (14, 9), (20, 9), (21, 9), (river[21][0] - 1, 21), (river[21][1] + 1, 21)]:
    if (bx, bz) not in water and not (bx <= 15 and bz <= 19): g[bz][bx] = ','

# ---- stamp the hand-made valley last (columns 0..15) and its river columns; the original had water in x 15..19 which the river now owns
for z in range(20):
    for x in range(16):
        g[z][x] = valley[z][x]
for z in range(20):
    for x in range(16, 20):
        g[z][x] = '~' if (x, z) in water else g[z][x]
for x in range(16, 20): g[9][x] = '='
# a mountain massif at the north edge of the valley: a Mine stands against row 3
for z, x0, x1 in [(1, 9, 10), (2, 8, 11), (3, 7, 12)]:
    for x in range(x0, x1 + 1): g[z][x] = 'M'
# a second landing for a dock: two bamboo cells on the bank are cleared
g[10][15] = '.'; g[11][15] = '.'

rows = [''.join(r) for r in g]
assert all(len(r) == W for r in rows) and len(rows) == H

m = data['map']
m['width'] = W; m['height'] = H
m['rows'] = rows
m['unlocked'] = {'x': 4, 'z': 3, 'width': 14, 'height': 13}   # x 4..17: the first two river columns are included so a dock fits
m['expansions'] = [
    {'level': 2, 'x': 4, 'z': 16, 'width': 14, 'height': 14},   # southern meadow
    {'level': 3, 'x': 20, 'z': 4, 'width': 14, 'height': 19},   # east bank beyond the bridge
]
data['camera']['defaultFocusX'] = 10
data['camera']['defaultFocusZ'] = 9.5
with open(PATH, 'w', encoding='utf-8') as f:
    json.dump(data, f, indent=2, ensure_ascii=False)
    f.write('\n')
print('x    ' + ''.join(str(x % 10) for x in range(W)))
for z, r in enumerate(rows): print(f'{z:2d}   {r}')
