
# ==== 04d : distant backdrop (mountains up to 6 km) : no void visible from the air ====
# Placed after the fill (its huge bounding box must not block prop placement).
# Starts under the rampart plateau (square 410 m, 50 m high) and rises to
# 100-550 m peaks, then a far ridge ; fog hides the outer edge. Name contains
# "Backdrop" : no collider in Unity, no cast shadow.
NA, NR, RMAX = 200, 56, 6000.0
verts = []
for k in range(NR + 1):
    t = k / NR
    for i in range(NA):
        a = 2 * math.pi * i / NA
        c, s = math.cos(a), math.sin(a)
        r0 = 410.0 / max(abs(c), abs(s))
        r = r0 + (RMAX - r0) * t * t
        x, y = r * c, r * s
        e = smooth((r - r0) / 500.0)
        n = noise.fractal(Vector((x * 0.0009, y * 0.0009, 11.0)), 1.0, 2.0, 5)
        mt = min(max(0.5 + 0.6 * n, 0.0), 1.0)
        h = 50.0 + e * (60.0 + 450.0 * mt ** 1.5) + 300.0 * smooth((r - 3500.0) / 2000.0)
        verts.append((x, y, h))
faces = []
for k in range(NR):
    for i in range(NA):
        j = (i + 1) % NA
        a0, a1 = k * NA + i, k * NA + j
        faces.append((a0, a1, a1 + NA, a0 + NA))
bd = make_obj("Backdrop_Mountains", verts, faces, "Rampart", "04_Backdrop")
bm = bmesh.new(); bm.from_mesh(bd.data)
bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY')
bm.normal_update()
if sum(1 for f in bm.faces if f.normal.z > 0) < len(bm.faces) / 2:   # open surface : faces must point up
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
bm.to_mesh(bd.data); bm.free()
for p in bd.data.polygons: p.use_smooth = True
world_uv(bd, scale=40.0)
