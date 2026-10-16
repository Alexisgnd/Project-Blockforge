"""BlockForge — Map_TestGround (terrain d'essai greybox 600 x 600 m).

Script source de verite : a lancer dans Blender 5.x (Scripting > Run Script, ou via MCP).
Reconstruit toute la scene de zero, ecrit les textures grille dans
Assets/_Project/Art/Textures/Map/TestGround, exporte Assets/_Project/Art/Models/Environment/Map_TestGround.fbx,
rend Preview_Overview.png et enregistre Map_TestGround.blend dans ce dossier.
Convention : Z vertical dans Blender (Y dans Unity), 1 unite = 1 metre, origine = centre du hub.
"""

# ==== BlockForge — Map_TestGround — 01 : setup, textures, helpers ====
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector, Matrix, noise

REPO = r"D:\unity projects\Project-Blockforge"
SRC_DIR = os.path.join(REPO, "Tools", "Maps", "TestGround")
TEX_DIR = os.path.join(REPO, "Assets", "_Project", "Art", "Textures", "Map", "TestGround")
FBX_PATH = os.path.join(REPO, "Assets", "_Project", "Art", "Models", "Environment", "Map_TestGround.fbx")
BLEND_PATH = os.path.join(SRC_DIR, "Map_TestGround.blend")
os.makedirs(SRC_DIR, exist_ok=True); os.makedirs(TEX_DIR, exist_ok=True)
random.seed(42)

# --- reset scene ---
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections): bpy.data.collections.remove(c)
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.curves, bpy.data.lights, bpy.data.cameras):
    for d in list(blk): blk.remove(d)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1.0

ROOT = bpy.data.collections.new("MAP_TestGround"); scene.collection.children.link(ROOT)
COLS = {}
def col(name):
    if name not in COLS:
        c = bpy.data.collections.new(name); ROOT.children.link(c); COLS[name] = c
    return COLS[name]

# --- grid textures : 1 tile = 10 m, minor line every 1 m, major line on the tile border ---
TEX = 1024
def grid_png(name, bg):
    bg = np.array(bg, dtype=np.float32) / 255.0
    img = np.ones((TEX, TEX, 4), dtype=np.float32); img[..., :3] = bg
    minor = bg * 0.80; major = bg * 0.55
    for i in range(1, 10):
        p = int(round(i * TEX / 10)); img[p-1:p+2, :, :3] = minor; img[:, p-1:p+2, :3] = minor
    img[5*TEX//10-2:5*TEX//10+3, :, :3] = bg * 0.70; img[:, 5*TEX//10-2:5*TEX//10+3, :3] = bg * 0.70  # 5 m line
    for sl in (slice(0, 4), slice(TEX-4, TEX)):
        img[sl, :, :3] = major; img[:, sl, :3] = major
    im = bpy.data.images.new("T_TG_" + name, TEX, TEX, alpha=False)
    im.pixels.foreach_set(img.ravel())
    path = os.path.join(TEX_DIR, "T_TG_%s.png" % name)
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im)
    return bpy.data.images.load(path, check_existing=True)

MATS = {}
def material(name, color=None, tex_bg=None, rough=0.85, emit=0.0):
    m = bpy.data.materials.new("M_TG_" + name); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = 0.0
    if tex_bg is not None:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = grid_png(name, tex_bg); t.location = (-400, 300)
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        m.diffuse_color = (*[ (c/255.0)**2.2 for c in tex_bg], 1)
    else:
        lin = [c ** 2.2 for c in color]
        bsdf.inputs["Base Color"].default_value = (*lin, 1); m.diffuse_color = (*lin, 1)
        if emit > 0:
            bsdf.inputs["Emission Color"].default_value = (*lin, 1); bsdf.inputs["Emission Strength"].default_value = emit
    MATS[name] = m
    return m

material("Ground",  tex_bg=(128, 130, 134))
material("Hub",     tex_bg=(188, 190, 194))
material("Rampart", tex_bg=(168, 163, 155))
material("Ramps",   tex_bg=(222, 128, 46))   # zone rampes : orange
material("Range",   tex_bg=(196, 58, 50))    # zone tir : rouge
material("Rough",   tex_bg=(170, 146, 110))  # zone accidentee : sable
material("Air",     tex_bg=(46, 166, 196))   # zone aerienne : cyan
material("Concrete",tex_bg=(150, 150, 150))
material("Marking", color=(0.93, 0.93, 0.90), rough=0.6)
material("Dark",    color=(0.12, 0.12, 0.13), rough=0.7)
material("Bounds",  color=(1.0, 0.0, 1.0), rough=1.0)
material("RedPaint", color=(0.85, 0.16, 0.12), rough=0.6)
material("Complex", tex_bg=(118, 132, 156))  # complexe a etages : ardoise
material("Yard",    tex_bg=(156, 84, 56))    # cour de combat : rouille
material("Steel",   tex_bg=(70, 104, 138))   # conteneurs bleus
material("Wood",    tex_bg=(150, 112, 70))   # caisses
material("TgtWhite",  color=(0.95, 0.95, 0.93), rough=0.6)
material("TgtBlack",  color=(0.10, 0.10, 0.11), rough=0.6)
material("TgtBlue",   color=(0.12, 0.45, 0.85), rough=0.6)
material("TgtRed",    color=(0.88, 0.15, 0.10), rough=0.6)
material("TgtYellow", color=(0.98, 0.80, 0.12), rough=0.6)

# --- helpers ---
def world_uv(ob, scale=10.0):
    me = ob.data; bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv.verify(); mw = ob.matrix_world; rot = mw.to_3x3()
    for f in bm.faces:
        n = rot @ f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = mw @ l.vert.co
            u, v = (p.x, p.y) if ax == 2 else ((p.y, p.z) if ax == 0 else (p.x, p.z))
            l[uvl].uv = (u / scale, v / scale)
    bm.to_mesh(me); bm.free()

def make_obj(name, verts, faces, mats, coll, origin=None, face_mats=None, recalc=False, uv=True):
    if isinstance(mats, str): mats = [mats]
    o = Vector(origin) if origin is not None else Vector((0, 0, 0))
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(Vector(v) - o) for v in verts], [], faces); me.update()
    ob = bpy.data.objects.new(name, me); ob.location = o; col(coll).objects.link(ob)
    for m in mats: me.materials.append(MATS[m])
    if face_mats is not None: me.polygons.foreach_set("material_index", face_mats)
    if recalc:
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    ob.matrix_world = Matrix.Translation(o)
    if uv: world_uv(ob)
    return ob

def box(name, center, size, mat, coll, rot_z=0.0):
    cx, cy, cz = center; a, b, c = [s / 2 for s in size]
    R = Matrix.Rotation(rot_z, 3, 'Z')
    loc = [(-a,-b,-c),(a,-b,-c),(a,b,-c),(-a,b,-c),(-a,-b,c),(a,-b,c),(a,b,c),(-a,b,c)]
    v = [Vector(center) + R @ Vector(p) for p in loc]
    f = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    return make_obj(name, v, f, mat, coll, origin=center)

def prism_x(name, profile_yz, x0, x1, mat, coll, origin=None):
    """Extrude a convex (y,z) profile between x0 and x1."""
    n = len(profile_yz)
    v = [(x0, y, z) for y, z in profile_yz] + [(x1, y, z) for y, z in profile_yz]
    f = [tuple(range(n)), tuple(range(n, 2*n))]
    for i in range(n):
        j = (i + 1) % n; f.append((i, j, n + j, n + i))
    if origin is None:
        origin = ((x0 + x1) / 2, sum(p[0] for p in profile_yz) / n, 0.0)
    return make_obj(name, v, f, mat, coll, origin=origin, recalc=True)

def prism_y(name, profile_xz, y0, y1, mat, coll, origin=None):
    n = len(profile_xz)
    v = [(x, y0, z) for x, z in profile_xz] + [(x, y1, z) for x, z in profile_xz]
    f = [tuple(range(n)), tuple(range(n, 2*n))]
    for i in range(n):
        j = (i + 1) % n; f.append((i, j, n + j, n + i))
    if origin is None:
        origin = (sum(p[0] for p in profile_xz) / n, (y0 + y1) / 2, 0.0)
    return make_obj(name, v, f, mat, coll, origin=origin, recalc=True)

def quad(name, x0, y0, x1, y1, z, mat, coll):
    v = [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]
    return make_obj(name, v, [(0, 1, 2, 3)], mat, coll, origin=((x0+x1)/2, (y0+y1)/2, z), uv=False)

FONT = None
for fp in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\segoeuib.ttf"):
    if os.path.exists(fp):
        FONT = bpy.data.fonts.load(fp, check_existing=True); break

def text(name, body, loc, size, mat, coll, rot=(0, 0, 0), extrude=0.0):
    cu = bpy.data.curves.new(name, 'FONT'); cu.body = body; cu.size = size
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'; cu.extrude = extrude; cu.resolution_u = 3
    if FONT: cu.font = FONT
    tmp = bpy.data.objects.new(name + "_tmp", cu); scene.collection.objects.link(tmp)
    tmp.location = loc; tmp.rotation_euler = rot
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    mw = tmp.matrix_world.copy()
    bpy.data.objects.remove(tmp, do_unlink=True); bpy.data.curves.remove(cu)
    me.materials.clear(); me.materials.append(MATS[mat])
    ob = bpy.data.objects.new(name, me); ob.matrix_world = mw; col(coll).objects.link(ob)
    return ob

def floor_text(name, body, x, y, size, coll, z=0.0, rot_z=0.0, mat="Marking"):
    return text(name, body, (x, y, z + 0.015), size, mat, coll, rot=(0, 0, rot_z))

def smooth(t):
    t = min(max(t, 0.0), 1.0); return t * t * (3 - 2 * t)

TGT_MATS = ["TgtYellow", "TgtRed", "TgtBlue", "TgtBlack", "TgtWhite", "Dark"]

def disk_target(name, cx, cy, cz, r, coll, face_to, thick=0.16, seg=32):
    """Round archery-style target : 5 concentric rings (yellow centre -> white), dark back and rim.
    Origin at the disk centre ; front faces the point face_to."""
    n = Vector((face_to[0] - cx, face_to[1] - cy, 0))
    n = n.normalized() if n.length > 1e-6 else Vector((0, -1, 0))
    R = Matrix.Rotation(math.atan2(n.x, -n.y), 3, 'Z')
    yf, yb = -thick / 2, thick / 2
    loc = [(0, yf, 0)]
    for k in range(5):
        rk = r * (k + 1) / 5
        loc += [(rk * math.cos(2 * math.pi * i / seg), yf, rk * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
    loc += [(r * math.cos(2 * math.pi * i / seg), yb, r * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
    ring = lambda k, i: 1 + k * seg + (i % seg)
    back = lambda i: 1 + 5 * seg + (i % seg)
    f = []; fm = []
    for i in range(seg): f.append((0, ring(0, i), ring(0, i + 1))); fm.append(0)
    for k in range(1, 5):
        for i in range(seg): f.append((ring(k - 1, i), ring(k, i), ring(k, i + 1), ring(k - 1, i + 1))); fm.append(k)
    f.append(tuple(back(i) for i in reversed(range(seg)))); fm.append(5)
    for i in range(seg): f.append((ring(4, i), back(i), back(i + 1), ring(4, i + 1))); fm.append(5)
    c = Vector((cx, cy, cz))
    return make_obj(name, [c + R @ Vector(p) for p in loc], f, TGT_MATS, coll, origin=(cx, cy, cz),
                    face_mats=fm, recalc=True, uv=False)

def target_stand(name, x, y, z0, cz, coll, face_to):
    n = Vector((face_to[0] - x, face_to[1] - y, 0)); n = n.normalized() if n.length > 1e-6 else Vector((0, -1, 0))
    a = math.atan2(n.x, -n.y); px, py = x - n.x * 0.25, y - n.y * 0.25
    return [box(name + "_Post", (px, py, (z0 + cz) / 2), (0.25, 0.25, cz - z0), "Dark", coll, rot_z=a),
            box(name + "_Base", (px, py, z0 + 0.1), (1.4, 1.0, 0.2), "Dark", coll, rot_z=a)]

def target_at(name, x, y, z0, coll, face_to, size=(4.0, 3.0, 3.0), stand=True):
    """Disk target on a post (radius from size) ; stand=False -> floating disk centred at z0 + r."""
    r = 0.42 * max(size[0], size[2])
    cz = z0 + (0.6 + r if stand else r)
    objs = [disk_target(name, x, y, cz, r, coll, face_to)]
    if stand: objs += target_stand(name, x, y, z0, cz, coll, face_to)
    return objs

# ---- moving targets : static root empty (with bf_* custom properties) + moving child empty ----
MOVERS = []
def empty(name, loc, coll, kind='PLAIN_AXES', size=1.5, rot_z=0.0):
    e = bpy.data.objects.new(name, None); e.empty_display_type = kind; e.empty_display_size = size
    e.location = loc; e.rotation_euler = (0, 0, rot_z); col(coll).objects.link(e); return e

def attach(objs, parent):
    bpy.context.view_layer.update()
    for o in objs:
        mw = o.matrix_world.copy(); o.parent = parent; o.matrix_world = mw

def moving_linear(name, p0, p1, coll, face_to, size=(4.0, 3.0, 3.0), speed=4.0):
    """Slider : <name>_Carriage travels from the root position to <name>_End (ping-pong)."""
    root = empty(name, p0, coll, 'CUBE', 0.6)
    root["bf_move"] = "linear"; root["bf_speed"] = float(speed); root["bf_pingpong"] = 1
    car = empty(name + "_Carriage", p0, coll, 'ARROWS', 1.5)
    end = empty(name + "_End", p1, coll, 'SPHERE', 0.8)
    attach(target_at(name + "_Target", p0[0], p0[1], p0[2] + 0.3, coll, face_to, size), car)
    d = Vector(p1) - Vector(p0); mid = (Vector(p0) + Vector(p1)) / 2
    rail = box(name + "_Rail", (mid.x, mid.y, p0[2] + 0.15), (d.length + 2.0, 0.6, 0.3), "Dark", coll, rot_z=math.atan2(d.y, d.x))
    attach([car, end, rail], root); MOVERS.append(name); return root

def moving_rotor(name, center, coll, arm=5.0, n_disks=2, r=1.2, speed=30.0, base_z=0.0, post=True, arms=True):
    """Turntable / orbit : <name>_Rotor spins around the vertical axis (bf_speed in deg/s)."""
    cx, cy, cz = center
    root = empty(name, center, coll, 'CUBE', 0.6); root["bf_move"] = "rotate"; root["bf_speed"] = float(speed)
    rot = empty(name + "_Rotor", center, coll, 'ARROWS', 2.0)
    parts = []
    if post: attach([box(name + "_Post", (cx, cy, (base_z + cz) / 2), (0.5, 0.5, cz - base_z), "Dark", coll)], root)
    for i in range(n_disks):
        t = 2 * math.pi * i / n_disks; px, py = cx + arm * math.cos(t), cy + arm * math.sin(t)
        if arms:
            parts.append(box("%s_Arm%d" % (name, i + 1), (cx + arm / 2 * math.cos(t), cy + arm / 2 * math.sin(t), cz),
                             (arm, 0.25, 0.25), "Dark", coll, rot_z=t))
        parts.append(disk_target("%s_Disk%d" % (name, i + 1), px, py, cz, r, coll,
                                 (px - math.sin(t) * 10, py + math.cos(t) * 10)))
    attach(parts, rot); attach([rot], root); MOVERS.append(name); return root

def moving_popup(name, x, y, z0, coll, face_to, r=1.0, angle=90.0):
    """Pop-up : <name>_Hinge rotates by bf_angle around the axis Hinge -> <name>_HingeAxis (target lies flat when down)."""
    n = Vector((face_to[0] - x, face_to[1] - y, 0)).normalized(); a = math.atan2(n.x, -n.y)
    root = empty(name, (x, y, z0), coll, 'CUBE', 0.5, rot_z=a); root["bf_move"] = "popup"; root["bf_angle"] = float(angle)
    hinge = empty(name + "_Hinge", (x, y, z0 + 0.25), coll, 'ARROWS', 1.0, rot_z=a)
    axis = empty(name + "_HingeAxis", (x + math.cos(a), y + math.sin(a), z0 + 0.25), coll, 'SPHERE', 0.3)
    base = box(name + "_Base", (x, y, z0 + 0.125), (2 * r + 0.6, 0.8, 0.25), "Dark", coll, rot_z=a)
    d = disk_target(name + "_Disk", x, y, z0 + 0.4 + r, r, coll, face_to)
    arm = box(name + "_Arm", (x - n.x * 0.2, y - n.y * 0.2, z0 + 0.25 + (0.15 + r) / 2), (0.2, 0.15, 0.15 + r), "Dark", coll, rot_z=a)
    attach([d, arm], hinge); attach([hinge, axis, base], root); MOVERS.append(name); return root

def cyl(name, center, r, length, mat, coll, axis='z', seg=12, rot_z=0.0):
    R = Matrix.Rotation(rot_z, 3, 'Z')
    A = Matrix.Rotation(math.pi / 2, 3, 'Y') if axis == 'x' else Matrix.Identity(3)
    v = [Vector(center) + R @ (A @ Vector((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), s * length)))
         for s in (-0.5, 0.5) for i in range(seg)]
    f = [tuple(range(seg)), tuple(range(seg, 2 * seg))] + [(i, (i + 1) % seg, seg + (i + 1) % seg, seg + i) for i in range(seg)]
    ob = make_obj(name, v, f, mat, coll, origin=center, recalc=True)
    for p in ob.data.polygons: p.use_smooth = len(p.vertices) == 4
    return ob

def extrude_profile(name, prof, length, center, a, mat, coll):
    """Convex (y,z) profile extruded along the local X axis (length), rotated by a around Z."""
    n = len(prof); R = Matrix.Rotation(a, 3, 'Z'); c = Vector(center)
    v = [c + R @ Vector((sx * length / 2, y, z)) for sx in (-1, 1) for y, z in prof]
    f = [tuple(range(n)), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    return make_obj(name, v, f, mat, coll, origin=center, recalc=True)

def ramp_local(name, center, a, x0, x1, w, zh, mat, coll):
    """Wedge in a local frame rotated by a : base on the ground, rising from x0 (0 m) to x1 (zh)."""
    R = Matrix.Rotation(a, 3, 'Z'); c = Vector(center)
    loc = [(x0, -w, 0), (x1, -w, 0), (x1, w, 0), (x0, w, 0), (x1, -w, zh), (x1, w, zh)]
    f = [(0, 3, 2, 1), (1, 2, 5, 4), (0, 4, 5, 3), (0, 1, 4), (3, 5, 2)]
    return make_obj(name, [c + R @ Vector(p) for p in loc], f, mat, coll, origin=center, recalc=True)

def slab_grid(name, F, holes, ztop, mat, coll, t=0.6):
    """One solid slab (top at ztop, thickness t) over rect F with rectangular openings (holes)."""
    fx0, fx1, fy0, fy1 = F
    xs = sorted({fx0, fx1} | {v for h in holes for v in (h[0], h[1]) if fx0 <= v <= fx1})
    ys = sorted({fy0, fy1} | {v for h in holes for v in (h[2], h[3]) if fy0 <= v <= fy1})
    nx, ny = len(xs), len(ys); off = nx * ny
    V = [(x, y, ztop) for y in ys for x in xs] + [(x, y, ztop - t) for y in ys for x in xs]
    faces = []; edges = {}
    for j in range(ny - 1):
        for i in range(nx - 1):
            cx_, cy_ = (xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2
            if any(h[0] < cx_ < h[1] and h[2] < cy_ < h[3] for h in holes): continue
            q = (j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i)
            faces.append(q); faces.append(tuple(off + k for k in reversed(q)))
            for e0, e1 in ((q[0], q[1]), (q[1], q[2]), (q[2], q[3]), (q[3], q[0])):
                edges.setdefault((min(e0, e1), max(e0, e1)), []).append((e0, e1))
    for lst in edges.values():
        if len(lst) == 1:
            e0, e1 = lst[0]; faces.append((e1, e0, off + e0, off + e1))
    ob = make_obj(name, V, faces, mat, coll, origin=((fx0 + fx1) / 2, (fy0 + fy1) / 2, ztop))
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS'); bm.to_mesh(ob.data); bm.free()
    return ob

def wedge(name, x0, x1, y0, y1, z_lo, z_hi, rise, mat, coll, zb=None):
    """Ramp on rect [x0,x1]x[y0,y1], top from z_lo to z_hi along rise ('+x','-x','+y','-y'), flat base at zb (default z_lo)."""
    zb = z_lo if zb is None else zb
    if rise in ('+x', '-x'):
        lo, hi = (x0, x1) if rise == '+x' else (x1, x0)
        prof = [(lo, zb), (hi, zb), (hi, z_hi)] + ([(lo, z_lo)] if z_lo - zb > 1e-6 else [])
        return prism_y(name, prof, y0, y1, mat, coll, origin=((x0 + x1) / 2, (y0 + y1) / 2, zb))
    lo, hi = (y0, y1) if rise == '+y' else (y1, y0)
    prof = [(lo, zb), (hi, zb), (hi, z_hi)] + ([(lo, z_lo)] if z_lo - zb > 1e-6 else [])
    return prism_x(name, prof, x0, x1, mat, coll, origin=((x0 + x1) / 2, (y0 + y1) / 2, zb))

def slab_with_hole(name, F, Hh, ztop, mat, coll, t=0.5):
    fx0, fx1, fy0, fy1 = F; hx0, hx1, hy0, hy1 = Hh
    for i, (a, b, c, d) in enumerate(((fx0, fx1, fy0, hy0), (fx0, fx1, hy1, fy1), (fx0, hx0, hy0, hy1), (hx1, fx1, hy0, hy1))):
        if b - a > 0.01 and d - c > 0.01:
            box("%s_%d" % (name, i), ((a + b) / 2, (c + d) / 2, ztop - t / 2), (b - a, d - c, t), mat, coll)

def _segments(a0, a1, gaps):
    segs = []; cur = a0
    for a, b in sorted(gaps):
        if a > cur: segs.append((cur, a))
        cur = max(cur, b)
    if cur < a1: segs.append((cur, a1))
    return [s for s in segs if s[1] - s[0] > 0.05]

def rail_x(name, x0, x1, y, z, coll, gaps=(), h=1.0, t=0.3, mat="Dark"):
    for i, (a, b) in enumerate(_segments(x0, x1, gaps)):
        box("%s_%d" % (name, i), ((a + b) / 2, y, z + h / 2), (b - a, t, h), mat, coll)

def rail_y(name, y0, y1, x, z, coll, gaps=(), h=1.0, t=0.3, mat="Dark"):
    for i, (a, b) in enumerate(_segments(y0, y1, gaps)):
        box("%s_%d" % (name, i), (x, (a + b) / 2, z + h / 2), (t, b - a, h), mat, coll)

STATS = {}

# ==== 02 : ground, hub, corridors, ramparts, bounds ====
def facing_rot(px, py, tx, ty):
    """rot_z for a vertical text/board (rot_x=90) at p so that its front faces point t."""
    return math.atan2(tx - px, -(ty - py))

def outward_rot(dx, dy):
    """rot_z for a floor text whose 'up' points along (dx,dy) (read while moving outward)."""
    return math.atan2(-dx, dy)

def annulus(name, cx, cy, r0, r1, z, mat, coll, seg=48):
    v = []; f = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        v += [(cx + r0*math.cos(a), cy + r0*math.sin(a), z), (cx + r1*math.cos(a), cy + r1*math.sin(a), z)]
    for i in range(seg):
        j = (i + 1) % seg; f.append((2*i, 2*i+1, 2*j+1, 2*j))
    return make_obj(name, v, f, mat, coll, origin=(cx, cy, z), uv=False)

HALF = 300.0; CELL = 10.0; HUB = 40.0
ROUGH = (-280.0, -50.0)   # rough-terrain patch extent on x and y (hole in the ground)

# --- ground 600 x 600, 10 m quads, hub faces lighter, hole for the rough patch ---
n = int(2 * HALF / CELL)
verts = [(-HALF + i*CELL, -HALF + j*CELL, 0.0) for j in range(n+1) for i in range(n+1)]
faces = []; fm = []
for j in range(n):
    for i in range(n):
        cx = -HALF + (i + .5)*CELL; cy = -HALF + (j + .5)*CELL
        if ROUGH[0] < cx < ROUGH[1] and ROUGH[0] < cy < ROUGH[1]: continue
        a = j*(n+1) + i; faces.append((a, a+1, a+n+2, a+n+1))
        fm.append(1 if (abs(cx) < HUB and abs(cy) < HUB) else 0)
g = make_obj("Ground", verts, faces, ["Ground", "Hub"], "00_Ground", face_mats=fm)
bpy.context.view_layer.objects.active = g
bm = bmesh.new(); bm.from_mesh(g.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS'); bm.to_mesh(g.data); bm.free()

# --- hub : spawn point, spawn ring, zone signs ---
sp = bpy.data.objects.new("SpawnPoint", None); sp.empty_display_type = 'ARROWS'; sp.empty_display_size = 3
sp.location = (0, 0, 0.5); col("01_Hub").objects.link(sp)
annulus("Hub_SpawnRing", 0, 0, 5.0, 5.6, 0.012, "Marking", "01_Hub")
floor_text("Hub_SpawnLabel", "SPAWN", 0, -8.5, 2.2, "01_Hub")
# hub border line
for k, (x0, y0, x1, y1) in enumerate([(-HUB, -HUB, HUB, -HUB+0.4), (-HUB, HUB-0.4, HUB, HUB),
                                       (-HUB, -HUB, -HUB+0.4, HUB), (HUB-0.4, -HUB, HUB, HUB)]):
    quad("Hub_Border_%d" % k, x0, y0, x1, y1, 0.012, "Marking", "01_Hub")

SIGNS = [("RAMPS & STEPS", 1, 1, "Ramps"), ("SHOOTING RANGE", -1, 1, "Range"),
         ("ROUGH TERRAIN", -1, -1, "Rough"), ("AIR COURSE", 1, -1, "Air"),
         ("MULTI-LEVEL", -1, 0, "Complex"), ("COMBAT YARD", 0, -1, "Yard")]
for label, sx, sy, mat in SIGNS:
    px, py = sx * 33.0, sy * 33.0
    a = facing_rot(px, py, 0, 0)
    nx, ny = -px / math.hypot(px, py), -py / math.hypot(px, py)
    ux, uy = math.cos(a), math.sin(a)
    tag = mat
    box("Sign_%s_Panel" % tag, (px, py, 6.0), (16, 0.4, 4), mat, "01_Hub", rot_z=a)
    for s in (-1, 1):
        box("Sign_%s_Post%d" % (tag, (s+1)//2), (px + s*6*ux, py + s*6*uy, 2.0), (0.5, 0.5, 4.0), "Dark", "01_Hub", rot_z=a)
    text("Sign_%s_Text" % tag, label, (px + nx*0.27, py + ny*0.27, 6.0), 1.5, "Marking", "01_Hub",
         rot=(math.pi/2, 0, a), extrude=0.05)

# --- corridors : distance markers every 50 m along the 4 axes ---
for dname, dx, dy in (("N", 0, 1), ("E", 1, 0)):   # W and S corridors are now zones
    for D in range(50, 300, 50):
        if dx == 0:
            quad("Corr_%s_%03d_Line" % (dname, D), -HUB, dy*D - 0.25, HUB, dy*D + 0.25, 0.012, "Marking", "02_Corridors")
        else:
            quad("Corr_%s_%03d_Line" % (dname, D), dx*D - 0.25, -HUB, dx*D + 0.25, HUB, 0.012, "Marking", "02_Corridors")
        floor_text("Corr_%s_%03d_Label" % (dname, D), "%d m" % D, dx*(D - 5), dy*(D - 5), 3.5, "02_Corridors",
                   rot_z=outward_rot(dx, dy))

# --- ramparts : rounded-square cliff ring around the playable area ---
def rampart_h(x, y):
    R = 60.0
    qx = abs(x) - (HALF - R); qy = abs(y) - (HALF - R)
    d = math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - R
    d += noise.noise(Vector((x*0.011, y*0.011, 3.1))) * 9.0
    if d <= 0: return -0.3
    h = 4.0 * smooth(d / 12.0)                       # talus
    h += 44.0 * smooth((d - 10.0) / 22.0)            # cliff
    h += max(d - 40.0, 0) * 0.35                     # plateau keeps rising
    h += noise.fractal(Vector((x*0.018, y*0.018, 7.0)), 1.0, 2.0, 4) * 6.0 * smooth(d / 25.0)
    return h - 0.3

EXT = 420.0; RS = 6.0; m = int(2*EXT/RS)
hv = [[rampart_h(-EXT + i*RS, -EXT + j*RS) for i in range(m+1)] for j in range(m+1)]
verts = [(-EXT + i*RS, -EXT + j*RS, hv[j][i]) for j in range(m+1) for i in range(m+1)]
faces = []
for j in range(m):
    for i in range(m):
        if max(hv[j][i], hv[j][i+1], hv[j+1][i], hv[j+1][i+1]) <= -0.29: continue
        a = j*(m+1) + i; faces.append((a, a+1, a+m+2, a+m+1))
r = make_obj("Ramparts", verts, faces, "Rampart", "03_Ramparts")
bm = bmesh.new(); bm.from_mesh(r.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY'); bm.to_mesh(r.data); bm.free()
world_uv(r)
for p in r.data.polygons: p.use_smooth = True

# --- invisible bounds (disable the renderer in Unity, keep the collider) ---
for nm, c, s in (("N", (0, 372, 100), (760, 2, 200)), ("S", (0, -372, 100), (760, 2, 200)),
                 ("E", (372, 0, 100), (2, 760, 200)), ("W", (-372, 0, 100), (2, 760, 200))):
    b = box("Bounds_%s" % nm, c, s, "Bounds", "09_Bounds_Invisible")
    b.display_type = 'WIRE'; b.hide_render = True

# ==== 03 : zone NE (ramps & steps) + zone NW (shooting range) ====
ZR = "10_Zone_Ramps"

# -- ramps : 8 slopes 10..45 deg, 5 m high, 10 m flat top, 12 m wide, same slope both sides --
H = 5.0; TOP = 10.0; W = 12.0; Y0 = 70.0
for i, a in enumerate([10, 15, 20, 25, 30, 35, 40, 45]):
    xc = 70 + i * 22; L = H / math.tan(math.radians(a))
    prof = [(Y0, 0), (Y0 + 2*L + TOP, 0), (Y0 + L + TOP, H), (Y0 + L, H)]
    prism_x("Ramp_%02ddeg" % a, prof, xc - W/2, xc + W/2, "Ramps", ZR, origin=(xc, Y0 + L + TOP/2, 0))
    floor_text("Ramp_%02ddeg_Label" % a, "%d°" % a, xc, Y0 - 5, 4.5, ZR)
    floor_text("Ramp_%02ddeg_TopLabel" % a, "%d°" % a, xc, Y0 + L + TOP/2, 3.0, ZR, z=H)

# -- steps : single blocks of increasing height --
SY0, SY1 = 165.0, 177.0
for i, h in enumerate([0.25, 0.5, 1.0, 1.5, 2.0, 3.0]):
    xc = 70 + i * 22
    box("Step_%03dcm" % int(h * 100), (xc, (SY0 + SY1) / 2, h / 2), (12, SY1 - SY0, h), "Ramps", ZR)
    floor_text("Step_%03dcm_Label" % int(h * 100), ("%g m" % h), xc, SY0 - 5, 3.0, ZR)
# -- stairs : 6 x 0.5 m, 2 m treads, 3 m drop at the back --
xc = 70 + 6 * 22
prof = [(SY0, 0), (SY1, 0), (SY1, 3.0), (175, 3.0), (175, 2.5), (173, 2.5), (173, 2.0), (171, 2.0), (171, 1.5),
        (169, 1.5), (169, 1.0), (167, 1.0), (167, 0.5), (165, 0.5)]
prism_x("Stairs_50cm", prof, xc - 6, xc + 6, "Ramps", ZR, origin=(xc, 171, 0))
floor_text("Stairs_Label", "STAIRS", xc, SY0 - 5, 3.0, ZR)
# -- climbing wall 6 m --
xc = 70 + 7 * 22
box("Wall_6m", (xc, 171, 3.0), (12, 4, 6.0), "Ramps", ZR)
floor_text("Wall_6m_Label", "WALL 6 m", xc, SY0 - 5, 3.0, ZR)

# -- side slopes (devers) : drive along Y across the slope --
for i, a in enumerate([15, 25, 35]):
    xs = 55 + i * 60; wdt = 22.0; hh = wdt * math.tan(math.radians(a))
    prism_y("SideSlope_%02ddeg" % a, [(xs, 0), (xs + wdt, 0), (xs + wdt, hh)], 215, 265, "Ramps", ZR,
            origin=(xs + wdt / 2, 240, 0))
    floor_text("SideSlope_%02ddeg_Label" % a, "SIDE %d°" % a, xs + wdt / 2, 209, 3.0, ZR)

# -- jump lines : kicker 18 deg + gap + landing ramp --
for h, xc in ((1.5, 248.0), (3.0, 272.0)):
    L = h / math.tan(math.radians(18)); gap = 8.0 + 4.0 * h; tag = "%02d" % int(h * 10)
    wedge("Jump_%s_Kicker" % tag, xc - 5, xc + 5, 70, 70 + L, 0, h, '+y', "Ramps", ZR)
    ly0 = 70 + L + gap; lr = 0.7 * h / math.tan(math.radians(12))
    wedge("Jump_%s_Landing" % tag, xc - 5, xc + 5, ly0, ly0 + lr, 0, 0.7 * h, '-y', "Ramps", ZR)
    floor_text("Jump_%s_Label" % tag, "JUMP %g m" % h, xc, 64, 2.2, ZR)

# -- speed bumps (4 x 0.25 m) between ramps and steps --
for k, yb in enumerate((144.0, 146.5, 149.0, 151.5)):
    prism_x("Bump_%d" % (k + 1), [(yb - 0.4, 0), (yb + 0.4, 0), (yb + 0.15, 0.25), (yb - 0.15, 0.25)], 60, 230, "Ramps", ZR, origin=(145, yb, 0))
floor_text("Bump_Label", "BUMPS", 52, 148, 2.5, ZR, rot_z=outward_rot(1, 0))
# -- slalom posts --
for k, x in enumerate(range(62, 232, 12)):
    box("Slalom_%02d" % (k + 1), (x, 186.0 if k % 2 == 0 else 198.0, 0.9), (0.6, 0.6, 1.8), "Ramps", ZR)
floor_text("Slalom_Label", "SLALOM", 52, 192, 2.5, ZR, rot_z=outward_rot(1, 0))
# -- 4-sided pyramid : 4 m platform, ramps at 10 / 20 / 30 / 40 deg --
PX, PY = 260.0, 195.0
box("Pyramid_Top", (PX, PY, 2.0), (12, 12, 4.0), "Ramps", ZR)
for a_, side in ((10, 'N'), (20, 'S'), (30, 'E'), (40, 'W')):
    L = 4.0 / math.tan(math.radians(a_))
    if side == 'N': wedge("Pyramid_Ramp%02d" % a_, PX - 6, PX + 6, PY + 6, PY + 6 + L, 0, 4, '-y', "Ramps", ZR); lx, ly = PX, PY + 6 + L + 3
    if side == 'S': wedge("Pyramid_Ramp%02d" % a_, PX - 6, PX + 6, PY - 6 - L, PY - 6, 0, 4, '+y', "Ramps", ZR); lx, ly = PX, PY - 6 - L - 3
    if side == 'E': wedge("Pyramid_Ramp%02d" % a_, PX + 6, PX + 6 + L, PY - 4, PY + 4, 0, 4, '-x', "Ramps", ZR); lx, ly = PX + 6 + L + 2.5, PY
    if side == 'W': wedge("Pyramid_Ramp%02d" % a_, PX - 6 - L, PX - 6, PY - 4, PY + 4, 0, 4, '+x', "Ramps", ZR); lx, ly = PX - 6 - L - 2.5, PY
    floor_text("Pyramid_Ramp%02d_Label" % a_, "%d°" % a_, lx, ly, 2.0, ZR)
target_at("Pyramid_Target", PX, PY + 2, 4.0, ZR, face_to=(PX, 0))
# -- moving target behind the side slopes + pop-ups at the jump landings --
moving_linear("MoveLinear_NE_Back", (55, 276, 0), (200, 276, 0), ZR, face_to=(127, 0), speed=6)
moving_popup("MovePopup_NE_Jump15", 248, 113, 0, ZR, face_to=(248, 0))
moving_popup("MovePopup_NE_Jump30", 272, 122, 0, ZR, face_to=(272, 0))

# ==== zone NW : shooting range (fire towards +Y) ====
ZT = "11_Zone_Range"
FX0, FX1, FY = -280.0, -50.0, 60.0
FIRE = ((FX0 + FX1) / 2, FY)
quad("Range_FireLine", FX0, FY - 0.6, FX1, FY + 0.6, 0.012, "RedPaint", ZT)
floor_text("Range_FireLine_Label", "FIRE LINE", -240, FY - 4, 2.5, ZT)
lw = (FX1 - FX0) / 6
for k in range(1, 6):
    x = FX1 - k * lw
    quad("Range_LaneLine_%d" % k, x - 0.15, FY, x + 0.15, 266, 0.012, "Marking", ZT)

box("Range_FiringDeck", (-165, 50, 1.5), (70, 12, 3), "Concrete", ZT)
wedge("Range_FiringDeck_Ramp", -130, -130 + 3 / math.tan(math.radians(15)), 44, 56, 0, 3, '-x', "Concrete", ZT)
floor_text("Range_FiringDeck_Label", "DECK 3 m", -165, 50, 3.0, ZT, z=3)

# 6 lanes x 3 disk sizes (S / M robot / L)
SIZES = (("S", (1.5, 1.5, 1.5), -11.0), ("M", (4.0, 3.0, 3.0), 0.0), ("L", (6.0, 4.0, 5.0), 11.0))
for k, D in enumerate([10, 25, 50, 100, 150, 200]):
    xc = FX1 - (k + 0.5) * lw
    for tag, sz, off in SIZES:
        target_at("Target_%03dm_%s" % (D, tag), xc + off, FY + D, 0.0, ZT, face_to=(xc + off, FY - 100), size=sz)
    floor_text("Target_%03dm_Label" % D, "%d m" % D, xc, FY + D - 5, 3.0, ZT)

# elevated targets on pillars
for h, x, y in ((5, -69.0, 140.0), (10, -107.0, 190.0), (15, -146.0, 240.0), (25, -69.0, 240.0)):
    box("Target_High%02d_Pillar" % h, (x, y, h / 2), (2.5, 2.5, h), "Concrete", ZT)
    target_at("Target_High%02d" % h, x, y, h, ZT, face_to=(x, FY - 100))
    floor_text("Target_High%02d_Label" % h, "H %d m" % h, x, y - 6, 2.5, ZT)

# pop-up targets in every lane
for k, D in ((0, 6), (1, 15), (2, 35), (3, 70), (4, 120), (5, 170)):
    xc = FX1 - (k + 0.5) * lw
    moving_popup("MovePopup_Range_%d" % (k + 1), xc - 6, FY + D, 0, ZT, face_to=(xc - 6, FY - 100))
# sliders : far rails + mid rail (behind the short lanes)
moving_linear("MoveLinear_Range_Far1", (-270, 272, 0), (-170, 272, 0), ZT, face_to=(-220, FY - 100), speed=5)
moving_linear("MoveLinear_Range_Far2", (-160, 272, 0), (-60, 272, 0), ZT, face_to=(-110, FY - 100), speed=7)
moving_linear("MoveLinear_Range_Mid", (-160, 150, 0), (-55, 150, 0), ZT, face_to=(-107, FY - 100), size=(3, 2, 3), speed=6)
# turntables
moving_rotor("MoveRotor_Range_1", (-260, 115, 3.0), ZT, arm=4, n_disks=2, r=1.2, speed=40)
moving_rotor("MoveRotor_Range_2", (-184, 130, 3.0), ZT, arm=4, n_disks=2, r=1.2, speed=-30)
# orbiting "drones" (anti-air) : disks on an invisible rotor
moving_rotor("MoveOrbit_Range_1", (-210, 190, 22), ZT, arm=12, n_disks=3, r=1.3, speed=20, post=False, arms=False)
moving_rotor("MoveOrbit_Range_2", (-245, 230, 32), ZT, arm=10, n_disks=2, r=1.3, speed=-25, post=False, arms=False)
moving_rotor("MoveOrbit_Range_3", (-150, 235, 40), ZT, arm=14, n_disks=3, r=1.3, speed=15, post=False, arms=False)

box("Range_Backstop", ((FX0 + FX1) / 2, 283.0, 6.0), (FX1 - FX0 + 6, 2.0, 12.0), "Concrete", ZT)

# ==== 04 : zone SW (rough terrain) + zone SE (air course) ====
ZG = "12_Zone_Rough"
CRATERS = ((-195.0, -105.0, 16.0, 3.5), (-115.0, -225.0, 13.0, 3.0), (-215.0, -225.0, 10.0, 2.5))
TRENCH_Y = -165.0

MESA = (-160.0, -262.0, 31.5, 10.0)   # centre x, y, half-size x (with ramps), half-size y

def rough_h(x, y):
    e = min(x - ROUGH[0], ROUGH[1] - x, y - ROUGH[0], ROUGH[1] - y)
    w = smooth(e / 14.0)
    r = min(max((-(x + 50) - (y + 50)) / 400.0, 0.0), 1.0)          # 0 near the hub -> 1 far corner
    h = noise.fractal(Vector((x * 0.035, y * 0.035, 1.7)), 1.0, 2.0, 3) * (0.4 + 3.6 * r)
    if -272 <= x <= -238:                                             # moguls strip (8 m wave)
        t = smooth(min(x + 272, -238 - x) / 4.0)
        h = h * (1 - t) + t * 0.45 * math.sin(2 * math.pi * y / 8.0)
    dt = abs(y - TRENCH_Y)                                            # trench 12 m wide, 1.8 m deep
    if dt < 6 and x > -232:
        h -= 1.8 * (0.5 + 0.5 * math.cos(math.pi * dt / 6.0)) * smooth((x + 232) / 6.0)
    for cx, cy, R, D in CRATERS:                                      # bowl + rim
        d = math.hypot(x - cx, y - cy) / R
        if d < 1.6:
            k = smooth((1.6 - d) / 0.6)
            h = h * (1 - 0.8 * k) + (-D + (D + 1.0) * d * d if d < 1 else 1.0 * k)
    dm = max(abs(x - MESA[0]) - MESA[2], abs(y - MESA[1]) - MESA[3])   # flatten around the mesa
    return h * w * smooth(dm / 8.0)

RS2 = 2.0; nr = int((ROUGH[1] - ROUGH[0]) / RS2)
verts = [(ROUGH[0] + i*RS2, ROUGH[0] + j*RS2, rough_h(ROUGH[0] + i*RS2, ROUGH[0] + j*RS2)) for j in range(nr+1) for i in range(nr+1)]
faces = [(j*(nr+1)+i, j*(nr+1)+i+1, (j+1)*(nr+1)+i+1, (j+1)*(nr+1)+i) for j in range(nr) for i in range(nr)]
rt = make_obj("RoughTerrain", verts, faces, "Rough", ZG, origin=((ROUGH[0]+ROUGH[1])/2, (ROUGH[0]+ROUGH[1])/2, 0))
for p in rt.data.polygons: p.use_smooth = True

placed = 0; tries = 0
while placed < 45 and tries < 2000:
    tries += 1
    x = random.uniform(ROUGH[0] + 10, ROUGH[1] - 10); y = random.uniform(ROUGH[0] + 10, ROUGH[1] - 10)
    if -276 <= x <= -234 or abs(y - TRENCH_Y) < 10: continue
    if any(math.hypot(x - cx, y - cy) < R * 1.3 for cx, cy, R, D in CRATERS): continue
    if -(x + 50) - (y + 50) < 40: continue
    if abs(x - MESA[0]) < MESA[2] + 10 and abs(y - MESA[1]) < MESA[3] + 10: continue
    s = random.uniform(0.7, 3.2)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    sx, sy, sz = random.uniform(0.8, 1.3), random.uniform(0.8, 1.3), random.uniform(0.5, 0.9)
    for v in bm.verts:
        nz = 1.0 + 0.28 * noise.noise(v.co * 1.7 + Vector((placed * 3.1, 0, 0)))
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz)) * s * nz
    rz = random.uniform(0, 2 * math.pi)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rz, 3, 'Z'))
    z = rough_h(x, y) - 0.3 * s
    vv = [tuple(Vector((x, y, z)) + v.co) for v in bm.verts]; ff = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    make_obj("Rock_%02d" % placed, vv, ff, "Rampart", ZG, origin=(x, y, z), recalc=True)
    placed += 1

floor_text("Rough_Moguls_Label", "MOGULS", -255, -45, 3.0, ZG)
floor_text("Rough_Trench_Label", "TRENCH", -45, TRENCH_Y, 3.0, ZG, rot_z=outward_rot(-1, 0))
floor_text("Rough_Gentle_Label", "ROUGH: GENTLE > HARD", -120, -45, 2.5, ZG)

# ==== zone SE : air course ====
ZA = "13_Zone_Air"
# altitude tower : 3 x 3 m, 120 m, alternating 10 m bands, labels facing the hub
TX, TY = 60.0, -60.0
a = facing_rot(TX, TY, 0, 0); nx, ny = -TX / math.hypot(TX, TY), -TY / math.hypot(TX, TY)
for k in range(12):
    box("AltTower_%03dm" % ((k + 1) * 10), (TX, TY, k * 10 + 5), (3, 3, 10), "Air" if k % 2 == 0 else "Marking", ZA, rot_z=a)
    text("AltTower_%03dm_Label" % ((k + 1) * 10), "%d" % ((k + 1) * 10), (TX + nx * 1.6, TY + ny * 1.6, k * 10 + 9.0),
         1.6, "Dark", ZA, rot=(math.pi / 2, 0, a), extrude=0.05)

# landing pads on pillars
for h, px, py in ((5, 95, -130), (10, 140, -185), (20, 195, -130), (30, 240, -185)):
    box("Pad_%02dm" % h, (px, py, h - 0.5), (14, 14, 1.0), "Air", ZA)
    box("Pad_%02dm_Pillar" % h, (px, py, (h - 1) / 2), (4, 4, h - 1), "Concrete", ZA)
    floor_text("Pad_%02dm_Label" % h, "%d m" % h, px, py, 4.0, ZA, z=h)

# runway markings (along +X)
RY = -75.0
quad("Runway_EdgeL", 90, RY - 10.25, 280, RY - 9.75, 0.012, "Marking", ZA)
quad("Runway_EdgeR", 90, RY + 9.75, 280, RY + 10.25, 0.012, "Marking", ZA)
for k in range(15):
    x0 = 105 + k * 12
    quad("Runway_Dash_%02d" % k, x0, RY - 0.3, x0 + 6, RY + 0.3, 0.012, "Marking", ZA)
floor_text("Runway_Label", "RUNWAY", 96, RY, 3.5, ZA, rot_z=outward_rot(1, 0))

# rings (major r 7 m, tube 0.55 m), numbered, oriented along the course
def torus(name, center, tangent, R=7.0, r=0.55, seg=32, sides=8):
    q = Vector((0, 0, 1)).rotation_difference(tangent.normalized())
    v = []; f = []
    for i in range(seg):
        a1 = 2 * math.pi * i / seg
        for j in range(sides):
            b = 2 * math.pi * j / sides
            p = Vector(((R + r * math.cos(b)) * math.cos(a1), (R + r * math.cos(b)) * math.sin(a1), r * math.sin(b)))
            v.append(Vector(center) + q @ p)
    for i in range(seg):
        for j in range(sides):
            i2 = (i + 1) % seg; j2 = (j + 1) % sides
            f.append((i * sides + j, i2 * sides + j, i2 * sides + j2, i * sides + j2))
    ob = make_obj(name, v, f, "Air", ZA, origin=center, recalc=True)
    for p in ob.data.polygons: p.use_smooth = True
    return ob

COURSE = [(70, -215, 3), (90, -250, 10), (140, -268, 16), (195, -262, 22), (245, -240, 28), (270, -190, 34),
          (258, -128, 40), (205, -100, 34), (150, -100, 26), (100, -168, 16)]
for k in range(1, len(COURSE)):
    p = Vector(COURSE[k]); prv = Vector(COURSE[k - 1]); nxt = Vector(COURSE[k + 1]) if k + 1 < len(COURSE) else p + (p - prv)
    t = (nxt - prv); t.z = 0
    torus("Ring_%02d" % k, tuple(p), t)
    ra = facing_rot(p.x, p.y, prv.x, prv.y)
    text("Ring_%02d_Number" % k, "%d" % k, (p.x, p.y, p.z + 9.5), 3.0, "Marking", ZA, rot=(math.pi / 2, 0, ra), extrude=0.1)
floor_text("Air_Start_Label", "RINGS START", 70, -205, 3.0, ZA)

# -- mesa 6 m with two access ramps (rough zone) --
box("Rough_Mesa", (-160, -262, 2.0), (30, 20, 8.0), "Rampart", ZG)
wedge("Rough_Mesa_RampE", -145, -128.5, -268, -256, 0, 6, '-x', "Rough", ZG)
wedge("Rough_Mesa_RampW", -191.5, -175, -268, -256, 0, 6, '+x', "Rough", ZG)
floor_text("Rough_Mesa_Label", "MESA 6 m", -160, -258, 3.0, ZG, z=6)
target_at("Rough_Mesa_Target", -160, -266, 6, ZG, face_to=(0, 0))

# -- sky stairs : 6 platforms every 3 m linked by ramps --
SY = -225.0
wedge("SkyStairs_Ramp0", 93, 105, SY - 4, SY + 4, 0, 3, '+x', "Air", ZA)
for i in range(1, 7):
    xc = 110 + 22 * (i - 1); h = 3.0 * i
    box("SkyStairs_P%d" % i, (xc, SY, h - 0.4), (10, 10, 0.8), "Air", ZA)
    box("SkyStairs_P%d_Pillar" % i, (xc, SY, (h - 0.8) / 2), (2, 2, h - 0.8), "Concrete", ZA)
    floor_text("SkyStairs_P%d_Label" % i, "%d m" % int(h), xc, SY, 2.5, ZA, z=h)
    if i < 6:
        wedge("SkyStairs_Ramp%d" % i, xc + 5, xc + 17, SY - 4, SY + 4, h, h + 3, '+x', "Air", ZA)
for k, (dx, dy) in enumerate(((-3.5, 3.5), (3.5, -3.5))):
    target_at("SkyStairs_Target_%d" % (k + 1), 220 + dx, SY + dy, 18, ZA, face_to=(0, 0), size=(2, 2, 2))

# -- air zone : orbiting drones, runway slider, pop-ups on the landing pads --
moving_rotor("MoveOrbit_Air_1", (150, -150, 25), ZA, arm=20, n_disks=3, r=1.5, speed=15, post=False, arms=False)
moving_rotor("MoveOrbit_Air_2", (225, -235, 45), ZA, arm=15, n_disks=2, r=1.5, speed=-20, post=False, arms=False)
moving_rotor("MoveOrbit_Air_3", (60, -60, 60), ZA, arm=18, n_disks=4, r=1.5, speed=12, post=False, arms=False)
moving_linear("MoveLinear_Air_Runway", (100, -95, 0), (270, -95, 0), ZA, face_to=(185, 0), speed=12)
for h, px, py in ((5, 95, -130), (10, 140, -185), (20, 195, -130), (30, 240, -185)):
    moving_popup("MovePopup_Pad%02d" % h, px + 3.5, py + 3.5, h, ZA, face_to=(0, 0))

# -- rough zone : ruins (broken walls) + pop-up targets --
for g, (rx, ry) in enumerate(((-110, -75), (-200, -140), (-250, -205), (-95, -262), (-70, -190))):
    a0 = random.uniform(0, math.pi)
    for k in range(random.randint(3, 5)):
        L = random.uniform(4, 9); h = random.uniform(1.0, 4.5)
        a = a0 + (math.pi / 2 if k % 2 else 0)
        ox, oy = random.uniform(-7, 7), random.uniform(-7, 7)
        x, y = rx + ox, ry + oy; z0 = rough_h(x, y) - 0.6
        box("Rough_Ruin%d_Wall%d" % (g + 1, k + 1), (x, y, z0 + (h + 0.6) / 2), (L, 0.7, h + 0.6), "Concrete", ZG, rot_z=a)
for k, (x, y) in enumerate(((-90, -140), (-130, -195), (-235, -150), (-230, -262), (-170, -95))):
    moving_popup("MovePopup_Rough_%d" % (k + 1), x, y, rough_h(x, y) - 0.05, ZG, face_to=(0, 0))

# ==== 04b : corridor W (multi-level complex) + corridor S (combat yard) ====
ZM = "14_MultiLevel"

# -- ziggurat : 3 tiers (2 / 4 / 6 m), one ramp per tier on a different side --
ZX, ZY = -82.0, 0.0
for i, (hx, hy) in enumerate(((18, 22), (13, 16), (8, 10))):
    top = 2.0 * (i + 1)
    box("Zig_Tier%d" % (i + 1), (ZX, ZY, top / 2), (2 * hx, 2 * hy, top), "Complex", ZM)
wedge("Zig_Ramp1", ZX + 18, ZX + 18 + 2 / math.tan(math.radians(15)), -4, 4, 0, 2, '-x', "Complex", ZM)
wedge("Zig_Ramp2", ZX - 4, ZX + 4, ZY + 16, ZY + 22, 2, 4, '-y', "Complex", ZM)
wedge("Zig_Ramp3", ZX - 13, ZX - 8, ZY - 3, ZY + 3, 4, 6, '+x', "Complex", ZM)
floor_text("Zig_Label", "ZIGGURAT 6 m", ZX, ZY - 4, 1.6, ZM, z=6)
target_at("Zig_Target", ZX, ZY + 5, 6, ZM, face_to=(0, 0))

# -- parking structure : 125 x 70 m, ground + 4 levels every 8 m (7.4 m clear), 15 deg ramps, open atrium --
PF = (-235.0, -110.0, -35.0, 35.0)
FH = 8.0
RL = FH / math.tan(math.radians(15))
HA = (-160.0, -160.0 + RL, -35.0, -25.0)     # strip A (south) : ramps rising +x
HB = (-200.0, -200.0 + RL, 25.0, 35.0)       # strip B (north) : ramps rising -x
ATR = (-190.0, -165.0, -10.0, 10.0)          # atrium, open on every level
wedge("Park_Ramp_0to1", *HA, 0.0, 8.0, '+x', "Complex", ZM)
wedge("Park_Ramp_1to2", *HB, 8.0, 16.0, '-x', "Complex", ZM)
wedge("Park_Ramp_2to3", *HA, 16.0, 24.0, '+x', "Complex", ZM)
wedge("Park_Ramp_3to4", *HB, 24.0, 32.0, '-x', "Complex", ZM)
T = 0.15
for k in range(1, 5):
    z = FH * k; hole = HA if k % 2 == 1 else HB
    slab_grid("Park_Slab_L%d" % k, PF, [hole, ATR], z, "Complex", ZM)
    rail_x("Park_L%d_RailS" % k, PF[0], PF[1], PF[2] + T, z, ZM, gaps=[(HA[0], HA[1])] if k <= 3 else [])
    rail_x("Park_L%d_RailN" % k, PF[0], PF[1], PF[3] - T, z, ZM, gaps=[(HB[0], HB[1])])
    rail_y("Park_L%d_RailW" % k, PF[2], PF[3], PF[0] + T, z, ZM, gaps={2: [(-24, -16)], 3: [(16, 24)]}.get(k, []))
    rail_y("Park_L%d_RailE" % k, PF[2], PF[3], PF[1] - T, z, ZM)
    if hole is HA:
        rail_x("Park_L%d_HoleSide" % k, HA[0], HA[1], HA[3] + T, z, ZM)
        rail_y("Park_L%d_HoleEnd" % k, HA[2], HA[3], HA[0] - T, z, ZM)
    else:
        rail_x("Park_L%d_HoleSide" % k, HB[0], HB[1], HB[2] - T, z, ZM)
        rail_y("Park_L%d_HoleEnd" % k, HB[2], HB[3], HB[1] + T, z, ZM)
    rail_x("Park_L%d_AtrS" % k, ATR[0] - 2 * T, ATR[1] + 2 * T, ATR[2] - T, z, ZM)
    rail_x("Park_L%d_AtrN" % k, ATR[0] - 2 * T, ATR[1] + 2 * T, ATR[3] + T, z, ZM)
    rail_y("Park_L%d_AtrW" % k, ATR[2], ATR[3], ATR[0] - T, z, ZM)
    rail_y("Park_L%d_AtrE" % k, ATR[2], ATR[3], ATR[1] + T, z, ZM)

def _in(px, py, r, m): return r[0] - m < px < r[1] + m and r[2] - m < py < r[3] + m
def _ov(r, q): return r[0] < q[1] and q[0] < r[1] and r[2] < q[3] and q[2] < r[3]
COLS_ = []
for x in (-234.5, -215, -195, -175, -155, -135, -110.5):
    for y in (-34.5, -12.5, 12.5, 34.5):
        if any(_in(x, y, r, 0.8) for r in (HA, HB, ATR)): continue
        box("Park_Col_%d_%d" % (int(-x), int(y)), (x, y, (4 * FH - 0.6) / 2), (1.0, 1.0, 4 * FH - 0.6), "Concrete", ZM)
        COLS_.append((x - 1.5, x + 1.5, y - 1.5, y + 1.5))

# roof penthouse (+5 m) with its own ramp
box("Park_Penthouse", (-223, 0, 32 + 2.5), (16, 12, 5), "Complex", ZM)
wedge("Park_Penthouse_Ramp", -215, -215 + 5 / math.tan(math.radians(15)), -4, 4, 32, 37, '-x', "Complex", ZM)
floor_text("Park_Penthouse_Label", "37 m", -223, -3, 2.5, ZM, z=37)
target_at("Park_Penthouse_Target", -227, 3, 37, ZM, face_to=(0, 0))

# per-level content : parked cars, cover walls, disk targets, one slider
random.seed(1234)
CAR_MATS = ("Steel", "Yard", "RedPaint", "Concrete")
for k in range(0, 5):
    z = FH * k
    lx = -222.0 if k < 4 else -140.0
    floor_text("Park_L%d_Label" % k, ("LEVEL %d  %d m" % (k, z)) if k else "PARKING", lx, 0, 2.5, ZM, z=z, rot_z=outward_rot(-1, 0))
    placed = [(lx - 3, lx + 3, -9, 9), (ATR[0] - 2, ATR[1] + 2, ATR[2] - 2, ATR[3] + 2)] + COLS_
    if k == 4: placed += [(-233, -195, -8, 8)]
    def try_place(w, h, tries=80):
        for _ in range(tries):
            x = random.uniform(PF[0] + 3 + w / 2, PF[1] - 3 - w / 2); y = random.uniform(-23 + h / 2, 23 - h / 2)
            r = (x - w / 2 - 1.5, x + w / 2 + 1.5, y - h / 2 - 1.5, y + h / 2 + 1.5)
            if any(_ov(r, q) for q in placed): continue
            placed.append(r); return x, y
        return None
    for i in range(7):
        rot90 = random.random() < 0.5; w, h = (2.0, 4.6) if rot90 else (4.6, 2.0)
        p = try_place(w, h)
        if p:
            m = random.choice(CAR_MATS); a_ = math.pi / 2 if rot90 else 0.0
            box("Park_L%d_Car%d" % (k, i + 1), (p[0], p[1], z + 0.6), (4.6, 2.0, 1.2), m, ZM, rot_z=a_)
            box("Park_L%d_Car%d_Cabin" % (k, i + 1), (p[0], p[1], z + 1.6), (2.4, 1.8, 0.8), m, ZM, rot_z=a_)
    for i in range(5):
        rot90 = random.random() < 0.5; w, h = (0.7, 5.0) if rot90 else (5.0, 0.7)
        p = try_place(w, h)
        if p: box("Park_L%d_Cover%d" % (k, i + 1), (p[0], p[1], z + 0.6), (5.0, 0.7, 1.2), "Concrete", ZM, rot_z=math.pi / 2 if rot90 else 0.0)
    for i in range(4):
        p = try_place(2.5, 2.5)
        if p: target_at("Park_L%d_Target%d" % (k, i + 1), p[0], p[1], z, ZM, face_to=(random.choice((PF[0], PF[1])), p[1] + random.uniform(-20, 20)))
    p = try_place(22, 3)
    if p: moving_linear("MoveLinear_Park_L%d" % k, (p[0] - 10, p[1], z), (p[0] + 10, p[1], z), ZM, face_to=(p[0], p[1] + (30 if p[1] < 0 else -30)), size=(3, 2, 3), speed=5)

# -- towers west of the parking : A (16 m) <- bridge from level 2, B (24 m) <- bridge from level 3, ramp A -> B --
box("TowerA", (-265, -20, 8.0), (20, 20, 16), "Complex", ZM)
box("TowerB", (-265, 20, 12.0), (20, 20, 24), "Complex", ZM)
box("Bridge_L2_TowerA", (-245, -20, 15.7), (20, 8, 0.6), "Complex", ZM)
box("Bridge_L3_TowerB", (-245, 20, 23.7), (20, 8, 0.6), "Complex", ZM)
prism_x("Ramp_TowerA_TowerB", [(-10, 16.0), (10, 24.0), (10, 23.4), (-10, 15.4)], -269, -261, "Complex", ZM, origin=(-265, 0, 20))
floor_text("TowerA_Label", "TOWER A 16 m", -265, -26, 2.0, ZM, z=16)
floor_text("TowerB_Label", "TOWER B 24 m", -265, 26, 2.0, ZM, z=24)
floor_text("Bridges_Label", "NO RAILS", -245, -20, 1.5, ZM, z=16, rot_z=outward_rot(-1, 0))
target_at("TowerA_Target", -272, -24, 16, ZM, face_to=(0, 0))
moving_rotor("MoveRotor_TowerB", (-265, 22, 27.0), ZM, arm=5, n_disks=2, r=1.2, speed=35, base_z=24.0)
floor_text("MultiLevel_Label", "MULTI-LEVEL", -46, 0, 3.0, ZM, rot_z=outward_rot(-1, 0))

# ==== corridor S : combat yard (containers, cover, watchtowers, bunker, dummies) ====
ZY_ = "15_Combat_Yard"
for nm, x, y, rot, z0, mat in (("C01", -20, -70, 0, 0, "Yard"), ("C02", 15, -85, 90, 0, "Steel"), ("C03", -25, -110, 90, 0, "Yard"),
                               ("C03b", -25, -110, 90, 2.6, "Steel"), ("C04", 20, -125, 0, 0, "Steel"), ("C05", 0, -150, 30, 0, "Yard"),
                               ("C06", -15, -170, 0, 0, "Steel"), ("C06b", -15, -170, 0, 2.6, "Yard"), ("C07", 22, -190, 90, 0, "Yard"),
                               ("C08", -5, -215, 0, 0, "Steel"), ("C09", 25, -240, 90, 0, "Steel"), ("C10", -25, -250, 0, 0, "Yard"),
                               ("C10b", -25, -250, 0, 2.6, "Steel")):
    box("Yard_Container_" + nm, (x, y, z0 + 1.3), (12, 2.5, 2.6), mat, ZY_, rot_z=math.radians(rot))
wedge("Yard_Container_C04_Ramp", 26, 36, -126.25, -123.75, 0, 2.6, '-x', "Concrete", ZY_)

for i, (x, y, rot) in enumerate(((10, -60, 0), (-8, -95, 90), (28, -105, 0), (-30, -140, 0), (8, -165, 90),
                                 (30, -160, 0), (-12, -195, 0), (5, -235, 90), (-30, -225, 90), (15, -265, 0))):
    box("Yard_Cover_%02d" % (i + 1), (x, y, 0.75), (6, 0.8, 1.5), "Concrete", ZY_, rot_z=math.radians(rot))

def watchtower(name, x, y, rise):
    box(name + "_Deck", (x, y, 7.75), (8, 8, 0.5), "Yard", ZY_)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("%s_Leg_%d%d" % (name, (sx + 1) // 2, (sy + 1) // 2), (x + sx * 3.5, y + sy * 3.5, 3.75), (0.6, 0.6, 7.5), "Dark", ZY_)
    L = 8.0 / math.tan(math.radians(20))
    if rise == '+y': wedge(name + "_Ramp", x - 3, x + 3, y - 4 - L, y - 4, 0, 8, '+y', "Concrete", ZY_)
    else:            wedge(name + "_Ramp", x - 3, x + 3, y + 4, y + 4 + L, 0, 8, '-y', "Concrete", ZY_)
    target_at(name + "_Target", x, y, 8, ZY_, face_to=(0, 0), size=(3, 2.5, 2.5))
watchtower("Yard_Tower1", 28, -75, '+y')
watchtower("Yard_Tower2", -28, -200, '-y')

box("Yard_Bunker_Back", (0, -276.5, 2.0), (14, 1, 4), "Concrete", ZY_)
box("Yard_Bunker_WallW", (-6.5, -272, 2.0), (1, 10, 4), "Concrete", ZY_)
box("Yard_Bunker_WallE", (6.5, -272, 2.0), (1, 10, 4), "Concrete", ZY_)
box("Yard_Bunker_Roof", (0, -272, 4.25), (14, 10, 0.5), "Yard", ZY_)
wedge("Yard_Bunker_Ramp", 7, 7 + 4.5 / math.tan(math.radians(15)), -276, -268, 0, 4.5, '-x', "Concrete", ZY_)
target_at("Yard_Bunker_Target", 0, -273.5, 0, ZY_, face_to=(0, 0))
target_at("Yard_Bunker_RoofTarget", 0, -272, 4.5, ZY_, face_to=(0, 0))

for i, (x, y) in enumerate(((-20, -76), (15, -95), (-25, -121), (20, -131), (0, -158), (-15, -176),
                            (22, -201), (-5, -221), (25, -252), (-25, -256))):
    target_at("Yard_Target_%02d" % (i + 1), x, y, 0, ZY_, face_to=(0, 0))
floor_text("Yard_Label", "COMBAT YARD", 0, -46, 3.0, ZY_, rot_z=outward_rot(0, -1))

# -- yard movers : pop-ups behind cover, two turntables --
for k, (x, y) in enumerate(((-32, -60), (32, -112), (-10, -128), (35, -178), (-35, -165), (12, -205), (-18, -238), (35, -270))):
    moving_popup("MovePopup_Yard_%d" % (k + 1), x, y, 0, ZY_, face_to=(0, 0))
moving_rotor("MoveRotor_Yard_1", (-30, -90, 2.5), ZY_, arm=4, n_disks=2, r=1.1, speed=45)
moving_rotor("MoveRotor_Yard_2", (30, -215, 2.5), ZY_, arm=4, n_disks=3, r=1.1, speed=-35)

# ==== 04c : fill the map — structured lane dressing + props placed on a free-space grid ====
ZF = "16_Props"
random.seed(7)
JERSEY = [(-0.35, 0), (0.35, 0), (0.15, 0.35), (0.12, 1.0), (-0.12, 1.0), (-0.15, 0.35)]
SANDBAG = [(-0.55, 0), (0.55, 0), (0.25, 0.9), (-0.25, 0.9)]

def light_pole(nm, x, y, a):
    box(nm + "_Post", (x, y, 5.0), (0.3, 0.3, 10.0), "Dark", ZF)
    box(nm + "_Arm", (x + 1.1 * math.cos(a), y + 1.1 * math.sin(a), 9.9), (2.5, 0.2, 0.2), "Dark", ZF, rot_z=a)
    box(nm + "_Lamp", (x + 2.1 * math.cos(a), y + 2.1 * math.sin(a), 9.7), (0.8, 0.5, 0.25), "Marking", ZF, rot_z=a)

# -- speed lanes N and E : jersey barriers every 12 m + light poles every 40 m --
for s_ in (-1, 1):
    side = "L" if s_ < 0 else "R"
    for k, t in enumerate(range(50, 290, 12)):
        extrude_profile("LaneN_Barrier_%s%02d" % (side, k), JERSEY, 3.0, (s_ * 13.5, t, 0), math.pi / 2, "Concrete", ZF)
        extrude_profile("LaneE_Barrier_%s%02d" % (side, k), JERSEY, 3.0, (t, s_ * 13.5, 0), 0.0, "Concrete", ZF)
    for k, t in enumerate(range(64, 290, 40)):
        light_pole("LaneN_Pole_%s%d" % (side, k), s_ * 17.0, t, math.pi if s_ > 0 else 0.0)
        light_pole("LaneE_Pole_%s%d" % (side, k), t, s_ * 17.0, -math.pi / 2 if s_ > 0 else math.pi / 2)

# -- occupancy grid (2 m) : existing geometry + reserved areas + ramparts --
GS = 2.0; NG = int(600 / GS)
occ = np.zeros((NG, NG), dtype=bool)
def _c(v): return int((v + 300.0) // GS)
def mark(x0, x1, y0, y1):
    i0, i1 = max(_c(x0), 0), min(_c(x1), NG - 1); j0, j1 = max(_c(y0), 0), min(_c(y1), NG - 1)
    if i0 <= i1 and j0 <= j1: occ[j0:j1 + 1, i0:i1 + 1] = True
def free(x0, x1, y0, y1):
    if x0 < -298 or x1 > 298 or y0 < -298 or y1 > 298: return False
    return not occ[_c(y0):_c(y1) + 1, _c(x0):_c(x1) + 1].any()

bpy.context.view_layer.update()
for ob in ROOT.all_objects:
    if ob.type != 'MESH' or ob.name in ("Ground", "Ramparts", "RoughTerrain") or ob.name.startswith("Bounds_"): continue
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    xs = [p.x for p in bb]; ys = [p.y for p in bb]; zs = [p.z for p in bb]
    if max(zs) - min(zs) < 0.05 and "Label" not in ob.name: continue   # flat paint lines
    if min(zs) > 4.0: continue                                         # floating (rings, drones, bridges)
    mark(min(xs) - 2, max(xs) + 2, min(ys) - 2, max(ys) + 2)
for r in ((-44, 44, -44, 44), (-15, 15, 40, 300), (40, 300, -15, 15), (-286, -46, 38, 292),
          (86, 284, -90, -60), (-284, -46, -284, -46)):
    mark(*r)
ram = np.zeros_like(occ)
for j in range(NG):
    for i in range(NG):
        if rampart_h(-300 + (i + .5) * GS, -300 + (j + .5) * GS) > -0.29: ram[j, i] = True
for _ in range(4):
    d = ram.copy(); d[1:, :] |= ram[:-1, :]; d[:-1, :] |= ram[1:, :]; d[:, 1:] |= ram[:, :-1]; d[:, :-1] |= ram[:, 1:]; ram = d
occ |= ram

def rot_off(x, y, a, dx, dy): return x + dx * math.cos(a) - dy * math.sin(a), y + dx * math.sin(a) + dy * math.cos(a)

# -- prop builders : (radius used for spacing, builder) --
def p_crates(nm, x, y, a):
    for k, (dx, dy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))[:random.randint(2, 4)]):
        px, py = rot_off(x, y, a, dx, dy); box("%s_%d" % (nm, k), (px, py, 0.9), (1.8, 1.8, 1.8), "Wood", ZF, rot_z=a)
    if random.random() < 0.6:
        px, py = rot_off(x, y, a, -1, -1); box(nm + "_Top", (px, py, 2.7), (1.8, 1.8, 1.8), "Wood", ZF, rot_z=a + 0.3)
def p_barrels(nm, x, y, a):
    for k in range(random.randint(3, 7)):
        px, py = rot_off(x, y, a, random.uniform(-2, 2), random.uniform(-2, 2))
        cyl("%s_%d" % (nm, k), (px, py, 0.6), 0.45, 1.2, random.choice(("RedPaint", "Steel", "Yard")), ZF, seg=10)
def p_jersey(nm, x, y, a):
    for k in range(random.randint(2, 4)):
        px, py = rot_off(x, y, a, (k - 1.5) * 3.05, 0); extrude_profile("%s_%d" % (nm, k), JERSEY, 3.0, (px, py, 0), a, "Concrete", ZF)
def p_blocks(nm, x, y, a):
    for k in range(random.randint(2, 4)):
        px, py = rot_off(x, y, a, random.uniform(-3, 3), random.uniform(-3, 3)); s = random.choice((1.5, 2.0, 2.5))
        box("%s_%d" % (nm, k), (px, py, s / 2), (s, s, s), "Concrete", ZF, rot_z=random.uniform(0, math.pi))
def p_pipe(nm, x, y, a):
    cyl(nm, (x, y, 1.0), 1.0, 8.0, "Steel", ZF, axis='x', seg=14, rot_z=a)
def p_sandbags(nm, x, y, a):
    for k in range(3):
        b = a + (k - 1) * 0.45; px, py = rot_off(x, y, b, 0, 3.0)
        extrude_profile("%s_%d" % (nm, k), SANDBAG, 3.0, (px, py, 0), b, "Rough", ZF)
def p_shed(nm, x, y, a):
    m = random.choice(("Yard", "Steel", "Concrete"))
    box(nm, (x, y, 1.75), (7, 5, 3.5), m, ZF, rot_z=a); box(nm + "_Roof", (x, y, 3.65), (7.6, 5.6, 0.3), "Dark", ZF, rot_z=a)
def p_container(nm, x, y, a):
    box(nm, (x, y, 1.3), (12, 2.5, 2.6), random.choice(("Yard", "Steel")), ZF, rot_z=a)
    if random.random() < 0.35: box(nm + "_Top", (x, y, 3.9), (12, 2.5, 2.6), random.choice(("Yard", "Steel")), ZF, rot_z=a + random.uniform(-0.1, 0.1))
def p_outpost(nm, x, y, a):
    box(nm + "_Deck", (x, y, 2.8), (6, 6, 0.4), "Yard", ZF, rot_z=a)
    for k, (dx, dy) in enumerate(((-2.7, -2.7), (2.7, -2.7), (-2.7, 2.7), (2.7, 2.7))):
        px, py = rot_off(x, y, a, dx, dy); box("%s_Leg%d" % (nm, k), (px, py, 1.3), (0.4, 0.4, 2.6), "Dark", ZF, rot_z=a)
    ramp_local(nm + "_Ramp", (x, y, 0), a, -3 - 3 / math.tan(math.radians(15)), -3, 2.0, 3.0, "Concrete", ZF)
    target_at(nm + "_Target", x, y, 3.0, ZF, face_to=(0, 0), size=(2, 2, 2))
def p_pole(nm, x, y, a): light_pole(nm, x, y, a)
def p_target(nm, x, y, a):
    target_at(nm, x, y, 0, ZF, face_to=(0, 0), size=random.choice(((2, 2, 2), (4, 3, 3), (5, 4, 4))))
def p_car(nm, x, y, a):
    m = random.choice(("Steel", "Yard", "RedPaint"))
    box(nm, (x, y, 0.6), (4.6, 2.0, 1.2), m, ZF, rot_z=a)
    px, py = rot_off(x, y, a, -0.3, 0); box(nm + "_Cabin", (px, py, 1.6), (2.4, 1.8, 0.8), m, ZF, rot_z=a)
def p_kicker(nm, x, y, a):
    ramp_local(nm, (x, y, 0), a, -2.5, 2.5, 2.0, 1.2, "Ramps", ZF)
def p_popup(nm, x, y, a):
    moving_popup("MovePopup_" + nm, x, y, 0, ZF, face_to=(0, 0))

PROPS = {"crates": (3.0, p_crates), "barrels": (3.0, p_barrels), "jersey": (6.5, p_jersey), "blocks": (4.5, p_blocks),
         "pipe": (4.5, p_pipe), "sandbags": (4.5, p_sandbags), "shed": (4.5, p_shed), "container": (6.5, p_container),
         "outpost": (10.0, p_outpost), "pole": (1.5, p_pole), "target": (2.0, p_target), "car": (3.0, p_car),
         "kicker": (3.5, p_kicker), "popup": (2.0, p_popup)}
REGIONS = [
    ("Yard",  -40, 40, -292, -44, 90, {"crates": 3, "barrels": 3, "jersey": 3, "blocks": 2, "sandbags": 4, "shed": 1, "container": 2, "target": 3, "car": 1, "popup": 1}),
    ("Air",    40, 292, -292, -40, 110, {"crates": 2, "barrels": 2, "blocks": 2, "pipe": 1, "shed": 2, "container": 2, "outpost": 2, "pole": 1, "target": 2, "kicker": 1, "car": 1, "popup": 1}),
    ("NE",     40, 292, 40, 292, 70, {"blocks": 3, "pipe": 2, "jersey": 2, "kicker": 2, "crates": 1, "target": 2, "pole": 1, "barrels": 1}),
    ("West", -292, -44, -44, 44, 60, {"crates": 2, "barrels": 2, "jersey": 2, "car": 3, "blocks": 1, "target": 1, "pole": 1, "sandbags": 1}),
    ("CorrN",  -44, 44, 44, 292, 35, {"crates": 2, "barrels": 2, "blocks": 2, "car": 2, "target": 1, "sandbags": 1}),
    ("CorrE",   44, 292, -44, 44, 35, {"crates": 2, "barrels": 2, "blocks": 2, "car": 2, "target": 1, "sandbags": 1}),
]
GAP = 4.0          # minimum free driving space kept between props
COUNT = {}
for reg, x0, x1, y0, y1, n, w in REGIONS:
    kinds = list(w.keys()); weights = [w[k] for k in kinds]; placed = 0; tries = 0
    while placed < n and tries < n * 40:
        tries += 1
        kind = random.choices(kinds, weights)[0]; rad, fn = PROPS[kind]
        x = random.uniform(x0 + rad, x1 - rad); y = random.uniform(y0 + rad, y1 - rad)
        if not free(x - rad - GAP, x + rad + GAP, y - rad - GAP, y + rad + GAP): continue
        a = random.choice((0.0, math.pi / 2, random.uniform(0, math.pi)))
        fn("Prop_%s_%s_%03d" % (reg, kind, placed + 1), x, y, a)
        mark(x - rad, x + rad, y - rad, y + rad); placed += 1
        COUNT[kind] = COUNT.get(kind, 0) + 1
    COUNT[reg] = placed
STATS["props"] = COUNT

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

# ==== 05 : finalize (triangulate ngons, checks), preview, save .blend, export FBX ====
issues = []; tris = {}
for c in ROOT.children:
    t = 0
    for ob in c.objects:
        if ob.type != 'MESH': continue
        bm = bmesh.new(); bm.from_mesh(ob.data)
        ng = [f for f in bm.faces if len(f.verts) > 4]
        if ng: bmesh.ops.triangulate(bm, faces=ng, quad_method='BEAUTY', ngon_method='BEAUTY')
        bm.to_mesh(ob.data); bm.free()
        zs = [v.co.z for v in ob.data.vertices]
        if max(zs) - min(zs) < 1e-4:   # flat decal-like meshes must face up
            down = sum(1 for p in ob.data.polygons if (ob.matrix_world.to_3x3() @ p.normal).z < 0)
            if down: issues.append("%s: %d faces vers le bas" % (ob.name, down))
        t += sum(len(p.vertices) - 2 for p in ob.data.polygons)
    tris[c.name] = t
STATS["tris"] = tris; STATS["total_tris"] = sum(tris.values()); STATS["issues"] = issues
STATS["meshes"] = sum(1 for o in ROOT.all_objects if o.type == 'MESH')
STATS["movers"] = len(MOVERS)

# --- export FBX (meshes + SpawnPoint empty only) ---
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=False, object_types={'MESH', 'EMPTY'},
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False,
    path_mode='RELATIVE', embed_textures=False, use_custom_props=True)

# --- preview helpers (not exported) ---
pc = bpy.data.collections.new("99_Preview"); scene.collection.children.link(pc)
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(35)); pc.objects.link(sun)
cam = bpy.data.objects.new("Cam_Overview", bpy.data.cameras.new("Cam_Overview")); pc.objects.link(cam)
cam.location = (430, -520, 420); cam.rotation_euler = (math.radians(55), 0, math.radians(40))
cam.data.lens = 28; cam.data.clip_end = 5000; scene.camera = cam
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'TEXTURE'
scene.display.shading.show_shadows = True; scene.display.shading.show_cavity = True
scene.render.resolution_x = 1920; scene.render.resolution_y = 1080; scene.render.resolution_percentage = 100
scene.render.filepath = os.path.join(SRC_DIR, "Preview_Overview.png"); scene.render.image_settings.file_format = 'PNG'
for b in COLS["09_Bounds_Invisible"].objects: b.hide_render = True
bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, relative_remap=True)
