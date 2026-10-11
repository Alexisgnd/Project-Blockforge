# =========================================================
# PISTOLET A PEINTURE DU GARAGE - V3 (modele procedural)
# A lancer dans Blender (Scripting > Run, ou exec du fichier) :
#   build()   reconstruit tout le modele dans la scene courante
#   export()  ecrit Assets/_Project/Art/Models/PaintSpray_Garage.fbx
#
# Repere : Z haut, buse vers -X, flancs en +/-Y, metres.
# Contrat avec Unity (PaintSprayMaterialSetup / PaintSprayColorWheel /
# PaintSprayFX) : noms des noeuds et des materiaux ci-dessous.
#   Colour_Wheel            axe de la molette (empty, rotation nulle)
#     Colour_Ring_Pivot     bague qui tourne (crans de 30 degres)
#       Colour_Swatch_1_00..11 / Colour_Swatch_-1_00..11  pastilles (Palette_00..11)
#     Selected_Colour_Marker(_2)  index fixe, au-dessus de la bague
#   Reservoir_Assembly      empty du reservoir (incline vers l'arriere)
#     Paint_Liquid          liquide (shape keys Slosh_PX/NX/PY/NY = houle)
#     Bubble_Template       bulle modele (cachee en jeu, clonee par PaintSprayFX)
#     Bubble_Spawn / Bubble_Radius / Liquid_Top   reperes (empties)
#   Nozzle_Glow, Status_Strip(_2)  anneau / bandeaux lumineux (Glow_Accent)
#   Paint_Outlet            sortie de buse (gouttelettes de peinture)
# =========================================================

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

PROJECT = r"D:\unity projects\Project-Blockforge"
FBX_PATH = os.path.join(PROJECT, r"Assets\_Project\Art\Models\PaintSpray_Garage.fbx")
COLLECTION = "PaintSpray_V3"

# ---------------------------------------------------------
# MATERIAUX (apercu Blender ; Unity les remappe sur ses .mat HDRP)
# ---------------------------------------------------------

MATERIALS = {
    "Ceramic_White":     ((0.80, 0.83, 0.86, 1), 0.10, 0.30),
    "Graphite":          ((0.035, 0.045, 0.055, 1), 0.40, 0.40),
    "Rubber_Grip":       ((0.018, 0.022, 0.028, 1), 0.00, 0.75),
    "Brushed_Aluminium": ((0.55, 0.58, 0.62, 1), 0.90, 0.28),
    "Accent_Red":        ((0.65, 0.035, 0.02, 1), 0.25, 0.35),
    "Reservoir_Clear":   ((0.80, 0.90, 0.95, 0.12), 0.00, 0.05),
    "Liquid_Red":        ((0.80, 0.035, 0.025, 0.80), 0.00, 0.15),
    "Bubbles_Pearl":     ((1.00, 0.70, 0.60, 0.60), 0.00, 0.05),
    "Glow_Accent":       ((0.20, 0.85, 1.00, 1), 0.00, 0.30),
}
HUES = [
    (0.850, 0.127, 0.127), (0.850, 0.489, 0.127), (0.850, 0.850, 0.127), (0.489, 0.850, 0.127),
    (0.127, 0.850, 0.127), (0.127, 0.850, 0.489), (0.127, 0.850, 0.850), (0.127, 0.489, 0.850),
    (0.127, 0.127, 0.850), (0.489, 0.127, 0.850), (0.850, 0.127, 0.850), (0.850, 0.127, 0.489),
]


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if name.startswith("Palette_"):
        rgb = HUES[int(name[-2:])]
        color, metal, rough = (*rgb, 1), 0.10, 0.35
    else:
        color, metal, rough = MATERIALS[name]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Alpha"].default_value = color[3]
    if name == "Glow_Accent":
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = 4.0
    if color[3] < 1:
        bsdf.inputs["Transmission Weight"].default_value = 0.0
        mat.surface_render_method = 'BLENDED'
    return mat


# ---------------------------------------------------------
# OUTILS DE MODELISATION
# ---------------------------------------------------------

def coll():
    c = bpy.data.collections.get(COLLECTION)
    if c is None:
        c = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(c)
    return c


def new_obj(name, bm, mat, parent=None, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0, segs=3, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    coll().objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    if parent is not None:
        ob.parent = parent
    if mat:
        me.materials.append(material(mat))
    if bevel > 0:
        mod = ob.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = segs
        mod.limit_method = 'ANGLE'
        mod.angle_limit = math.radians(35)
        mod.miter_outer = 'MITER_ARC'
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
        mod = ob.modifiers.new("Smooth", 'NODES')
        try:
            mod.node_group = smooth_group()
        except Exception:
            ob.modifiers.remove(mod)
    return ob


_SMOOTH = None


def smooth_group():
    """Geometry nodes 'lissage par angle' (30 deg) : aretes vives nettes, courbes lisses."""
    global _SMOOTH
    if _SMOOTH is not None:
        return _SMOOTH
    ng = bpy.data.node_groups.get("PS_SmoothByAngle")
    if ng is None:
        ng = bpy.data.node_groups.new("PS_SmoothByAngle", 'GeometryNodeTree')
        ng.interface.new_socket("Geometry", in_out='INPUT', socket_type='NodeSocketGeometry')
        ng.interface.new_socket("Geometry", in_out='OUTPUT', socket_type='NodeSocketGeometry')
        n_in = ng.nodes.new('NodeGroupInput')
        n_out = ng.nodes.new('NodeGroupOutput')
        edge_angle = ng.nodes.new('GeometryNodeInputMeshEdgeAngle')
        less = ng.nodes.new('FunctionNodeCompare')
        less.data_type = 'FLOAT'
        less.operation = 'LESS_EQUAL'
        less.inputs[1].default_value = math.radians(30)
        set_smooth = ng.nodes.new('GeometryNodeSetShadeSmooth')
        set_smooth.domain = 'EDGE'
        ng.links.new(n_in.outputs[0], set_smooth.inputs['Geometry'])
        ng.links.new(edge_angle.outputs['Unsigned Angle'], less.inputs[0])
        ng.links.new(less.outputs[0], set_smooth.inputs['Shade Smooth'])
        ng.links.new(set_smooth.outputs[0], n_out.inputs[0])
    _SMOOTH = ng
    return ng


def empty(name, parent=None, loc=(0, 0, 0), rot=(0, 0, 0), size=0.02):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_size = size
    ob.empty_display_type = 'PLAIN_AXES'
    coll().objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    if parent is not None:
        ob.parent = parent
    return ob


def bm_box(sx, sy, sz, center=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    return bm


def bm_cyl(r1, r2, depth, segs=48, axis='Z', center=(0, 0, 0), cap=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segs,
                          radius1=r1, radius2=r2, depth=depth)
    if axis == 'X':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-90), 3, 'Y'))
    elif axis == 'Y':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    return bm


def bm_profile(points_xz, half_width):
    """Prisme : silhouette laterale (x, z) extrudee de -half_width a +half_width en Y."""
    bm = bmesh.new()
    left = [bm.verts.new((x, -half_width, z)) for x, z in points_xz]
    face = bm.faces.new(left)
    bm.normal_update()
    if face.normal.y > 0:
        face.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    verts = [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, 2 * half_width, 0), verts=verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def bm_ring_sector(r_in, r_out, a0, a1, thickness, segs=10):
    """Secteur d'anneau dans le plan XZ (axe Y), epaisseur vers +Y depuis 0."""
    bm = bmesh.new()
    pts = []
    for i in range(segs + 1):
        a = a0 + (a1 - a0) * i / segs
        pts.append((math.cos(a) * r_out, math.sin(a) * r_out))
    for i in range(segs, -1, -1):
        a = a0 + (a1 - a0) * i / segs
        pts.append((math.cos(a) * r_in, math.sin(a) * r_in))
    verts = [bm.verts.new((x, 0, z)) for x, z in pts]
    face = bm.faces.new(verts)
    bm.normal_update()
    if face.normal.y > 0:
        face.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    vs = [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, thickness, 0), verts=vs)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def mirror_y(bm):
    bmesh.ops.scale(bm, vec=(1, -1, 1), verts=bm.verts)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    return bm


def boolean(target, cutter, op='DIFFERENCE'):
    mod = target.modifiers.new("Bool_" + cutter.name, 'BOOLEAN')
    mod.operation = op
    mod.object = cutter
    mod.solver = 'EXACT'
    # le booleen passe avant le biseau
    target.modifiers.move(len(target.modifiers) - 1, 0)
    cutter.hide_viewport = True
    cutter.hide_render = True
    return mod


# ---------------------------------------------------------
# CONSTRUCTION
# ---------------------------------------------------------

NOZZLE_Z = 0.075
DIAL_C = Vector((0.115, 0.0, 0.050))   # centre de la molette (x, -, z)
DIAL_Y = 0.104                         # face laterale du corps
RES_POS = Vector((0.155, 0.0, 0.165))  # pied du reservoir
RES_TILT = math.radians(18)            # incline vers l'arriere


def clear():
    c = bpy.data.collections.get(COLLECTION)
    if c is not None:
        for ob in list(c.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
    # ancien modele (v2) present dans le fichier : retire de la scene
    for ob in list(bpy.data.objects):
        if not ob.name.startswith("_Prev"):
            bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)


def build_body():
    # Coque superieure en ceramique : silhouette en coin, nez abaisse
    shell = new_obj("Upper_Shell", bm_profile([
        (0.300, 0.010), (0.300, 0.150), (0.255, 0.185), (-0.080, 0.185),
        (-0.215, 0.150), (-0.262, 0.110), (-0.262, 0.010),
    ], 0.100), "Ceramic_White", bevel=0.024, segs=5)

    # Rainure de la coque (ligne de construction) : decoupe fine sur le dessus
    groove = new_obj("_Cut_TopGroove", bm_box(0.30, 0.014, 0.02, (-0.06, 0, 0.19)), None, smooth=False)
    boolean(shell, groove)

    # Chassis inferieur en graphite, un peu en retrait (ligne d'ombre)
    new_obj("Body_Core", bm_profile([
        (0.285, -0.075), (0.285, 0.030), (-0.245, 0.030), (-0.245, -0.010),
        (-0.190, -0.055), (0.060, -0.075),
    ], 0.090), "Graphite", bevel=0.018, segs=4)

    # Plaques laterales (ceramique) sur le chassis, de part et d'autre
    for side, suffix in ((1, ""), (-1, "_2")):
        bm = bm_profile([
            (-0.215, -0.002), (-0.040, -0.002), (-0.010, -0.040), (-0.170, -0.040),
        ], 0.010)
        bmesh.ops.translate(bm, vec=(0, side * 0.092, 0), verts=bm.verts)
        new_obj("Side_Armour" + suffix, bm, "Ceramic_White", bevel=0.006, segs=3)

        # Bandeau lumineux (couleur courante) dans une gorge graphite
        bm = bm_box(0.17, 0.008, 0.012, (-0.120, side * 0.100, 0.060))
        new_obj("Status_Groove" + suffix, bm, "Graphite", bevel=0.004, segs=2)
        bm = bm_box(0.15, 0.006, 0.006, (-0.120, side * 0.1035, 0.060))
        new_obj("Status_Strip" + suffix, bm, "Glow_Accent", bevel=0.002, segs=2)

        # Ouies de refroidissement a l'arriere
        for i in range(4):
            bm = bm_box(0.009, 0.010, 0.040, (0.240 + i * 0.016, side * 0.099, 0.125))
            new_obj(f"Vent{suffix}_{i}", bm, "Graphite", bevel=0.003, segs=2)

        # Vis de la coque
        for j, (x, z) in enumerate(((-0.200, 0.140), (0.270, 0.035), (-0.200, 0.030))):
            bm = bm_cyl(0.0065, 0.0065, 0.006, 16, 'Y', (x, side * 0.1005, z))
            new_obj(f"Screw{suffix}_{j}", bm, "Brushed_Aluminium", bevel=0.0015, segs=2)

    # Rail superieur en aluminium (crante)
    new_obj("Top_Rail", bm_box(0.20, 0.030, 0.012, (-0.050, 0, 0.191)), "Brushed_Aluminium", bevel=0.004, segs=2)
    for i in range(7):
        new_obj(f"Top_Rail_Tooth_{i}", bm_box(0.012, 0.034, 0.006, (-0.135 + i * 0.028, 0, 0.199)),
                "Graphite", bevel=0.002, segs=2)


def build_rear():
    # Plaque arriere graphite + manometre
    bm = bm_box(0.014, 0.150, 0.130, (0.304, 0, 0.085))
    new_obj("Rear_Plate", bm, "Graphite", bevel=0.006, segs=3)
    for i in range(3):
        new_obj(f"Rear_Slot_{i}", bm_box(0.006, 0.090, 0.008, (0.312, 0, 0.040 + i * 0.016)),
                "Brushed_Aluminium", bevel=0.002, segs=2)
    new_obj("Gauge_Bezel", bm_cyl(0.034, 0.034, 0.016, 48, 'X', (0.315, 0, 0.115)),
            "Brushed_Aluminium", bevel=0.004, segs=3)
    new_obj("Gauge_Face", bm_cyl(0.027, 0.027, 0.004, 48, 'X', (0.3235, 0, 0.115)),
            "Ceramic_White", smooth=False)
    needle = bm_box(0.002, 0.003, 0.022, (0.326, 0, 0.122))
    bmesh.ops.rotate(needle, verts=needle.verts, cent=(0.326, 0, 0.115), matrix=Matrix.Rotation(math.radians(-35), 3, 'X'))
    new_obj("Gauge_Needle", needle, "Accent_Red", smooth=False)
    new_obj("Gauge_Glass", bm_cyl(0.028, 0.028, 0.003, 48, 'X', (0.329, 0, 0.115)),
            "Reservoir_Clear", smooth=False)
    # Embase du reservoir sur le dessus du corps
    new_obj("Reservoir_Boss", bm_cyl(0.078, 0.082, 0.014, 64, 'Z', (RES_POS.x, 0, 0.188)),
            "Graphite", bevel=0.004, segs=2)


def build_nozzle():
    z = NOZZLE_Z
    new_obj("Nozzle_Base", bm_cyl(0.072, 0.072, 0.040, 64, 'X', (-0.272, 0, z)),
            "Brushed_Aluminium", bevel=0.006, segs=3)
    for i in range(3):
        new_obj(f"Nozzle_Fin_{i}", bm_cyl(0.074, 0.074, 0.004, 64, 'X', (-0.258 - i * 0.012, 0, z)),
                "Graphite", bevel=0.001, segs=1)
    new_obj("Nozzle_Shroud", bm_cyl(0.066, 0.052, 0.110, 64, 'X', (-0.345, 0, z)),
            "Ceramic_White", bevel=0.010, segs=4)
    new_obj("Nozzle_Glow", bm_cyl(0.054, 0.054, 0.010, 64, 'X', (-0.405, 0, z)),
            "Glow_Accent", bevel=0.002, segs=2)
    new_obj("Nozzle_Tip", bm_cyl(0.048, 0.030, 0.040, 64, 'X', (-0.430, 0, z)),
            "Graphite", bevel=0.005, segs=3)
    new_obj("Outlet_Ring", bm_cyl(0.022, 0.022, 0.010, 32, 'X', (-0.452, 0, z)),
            "Brushed_Aluminium", bevel=0.002, segs=2)
    outlet = new_obj("Paint_Outlet", bm_cyl(0.013, 0.013, 0.004, 32, 'X', (-0.458, 0, z)),
                     "Liquid_Red", smooth=False)
    return outlet


def build_grip():
    # Poignee inclinee vers l'arriere, gainee de caoutchouc
    pivot = Vector((0.150, 0, -0.050))
    tilt = math.radians(-14)  # bas de la poignee vers l'arriere
    grip_prof = [
        (-0.040, 0.000), (0.042, 0.000), (0.046, -0.120), (0.040, -0.250),
        (0.034, -0.290), (-0.030, -0.290), (-0.036, -0.250), (-0.044, -0.120),
    ]
    grip = new_obj("Grip", bm_profile(grip_prof, 0.040), "Rubber_Grip", bevel=0.016, segs=4,
                   loc=pivot, rot=(0, tilt, 0))
    # Rainures pour les doigts (avant)
    for i in range(3):
        bm = bm_cyl(0.022, 0.022, 0.12, 32, 'Y', (-0.060, 0, -0.072 - i * 0.064))
        cutter = new_obj(f"_Cut_Finger_{i}", bm, None, smooth=False, loc=pivot, rot=(0, tilt, 0))
        boolean(grip, cutter)
    # Nervures laterales antiderapantes
    for side in (1, -1):
        for i in range(6):
            bm = bm_box(0.050, 0.006, 0.007, (0.004, side * 0.040, -0.060 - i * 0.030))
            new_obj(f"Grip_Rib_{'L' if side > 0 else 'R'}{i}", bm, "Graphite", bevel=0.0025, segs=2,
                    loc=pivot, rot=(0, tilt, 0))
    # Dos en ceramique
    new_obj("Grip_Backstrap", bm_profile([
        (0.030, -0.010), (0.052, -0.010), (0.054, -0.120), (0.048, -0.250), (0.028, -0.250),
    ], 0.030), "Ceramic_White", bevel=0.008, segs=3, loc=pivot, rot=(0, tilt, 0))
    # Talon en aluminium
    new_obj("Grip_Heel", bm_box(0.094, 0.082, 0.026, (0.002, 0, -0.300)), "Brushed_Aluminium",
            bevel=0.009, segs=3, loc=pivot, rot=(0, tilt, 0))
    # Detente (lame courbe) et pontet
    trig = bm_profile([(0.000, 0.000), (0.016, 0.000), (0.010, -0.030), (-0.004, -0.062),
                       (-0.018, -0.070), (-0.010, -0.040)], 0.010)
    new_obj("Trigger", trig, "Brushed_Aluminium", bevel=0.004, segs=2, loc=(0.060, 0, -0.060))
    guard = bm_profile([
        (0.100, -0.060), (0.100, -0.145), (-0.040, -0.145), (-0.070, -0.110), (-0.070, -0.050),
        (-0.056, -0.050), (-0.056, -0.106), (-0.034, -0.131), (0.086, -0.131), (0.086, -0.060),
    ], 0.012)
    new_obj("Trigger_Guard", guard, "Graphite", bevel=0.004, segs=2)


def build_dial():
    wheel = empty("Colour_Wheel", loc=DIAL_C)
    ring = empty("Colour_Ring_Pivot", parent=wheel)
    n = 12
    step = 2 * math.pi / n
    gap = math.radians(3.5)
    for side, prefix, suffix in ((1, "Colour_Swatch_1_", ""), (-1, "Colour_Swatch_-1_", "_2")):
        y0 = side * DIAL_Y

        # Embase fixe en aluminium (vissee sur le corps)
        bm = bm_cyl(0.112, 0.112, 0.010, 72, 'Y', (0, y0 + side * 0.005, 0))
        new_obj("Dial_Base" + suffix, bm, "Brushed_Aluminium", bevel=0.003, segs=2, parent=wheel)

        # Bague tournante : fond graphite + crans sur la tranche + 12 pastilles
        bm = bm_cyl(0.104, 0.104, 0.012, 72, 'Y', (0, y0 + side * 0.016, 0))
        new_obj("Dial_Ring" + suffix, bm, "Graphite", bevel=0.003, segs=2, parent=ring)
        for i in range(n * 3):
            a = i * step / 3
            bm = bm_box(0.010, 0.012, 0.006, (0, 0, 0))
            bmesh.ops.translate(bm, vec=(0.106, 0, 0), verts=bm.verts)
            bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(-a, 3, 'Y'))
            bmesh.ops.translate(bm, vec=(0, y0 + side * 0.016, 0), verts=bm.verts)
            new_obj(f"Dial_Knurl{suffix}_{i:02d}", bm, "Brushed_Aluminium", bevel=0.0015, segs=1, parent=ring)
        for i in range(n):
            a0 = i * step + gap / 2
            a1 = (i + 1) * step - gap / 2
            bm = bm_ring_sector(0.064, 0.097, a0, a1, 0.008, 8)
            if side < 0:
                mirror_y(bm)
            bmesh.ops.translate(bm, vec=(0, y0 + side * 0.017, 0), verts=bm.verts)
            new_obj(f"{prefix}{i:02d}", bm, f"Palette_{i:02d}", bevel=0.002, segs=2, parent=ring)

        # Moyeu fixe : ceramique + insert graphite + bouton aluminium
        bm = bm_cyl(0.060, 0.056, 0.024, 64, 'Y', (0, y0 + side * 0.026, 0))
        new_obj("Palette_Hub" + suffix, bm, "Ceramic_White", bevel=0.006, segs=3, parent=wheel)
        bm = bm_cyl(0.040, 0.040, 0.006, 64, 'Y', (0, y0 + side * 0.038, 0))
        new_obj("Palette_Hub_Insert" + suffix, bm, "Graphite", bevel=0.002, segs=2, parent=wheel)
        bm = bm_cyl(0.016, 0.016, 0.008, 32, 'Y', (0, y0 + side * 0.042, 0))
        new_obj("Palette_Hub_Cap" + suffix, bm, "Brushed_Aluminium", bevel=0.002, segs=2, parent=wheel)

        # Index fixe au-dessus de la bague (pointe vers le centre)
        tri = bm_profile([(-0.016, 0.140), (0.016, 0.140), (0.0, 0.113)], 0.007)
        bmesh.ops.translate(tri, vec=(0, y0 + side * 0.022, 0), verts=tri.verts)
        new_obj("Selected_Colour_Marker" + suffix, tri, "Ceramic_White", bevel=0.002, segs=2, parent=wheel)
        bm = bm_box(0.008, 0.004, 0.006, (0, y0 + side * 0.030, 0.133))
        new_obj("Marker_Led" + suffix, bm, "Glow_Accent", bevel=0.001, segs=1, parent=wheel)
    return wheel, ring


def build_reservoir():
    asm = empty("Reservoir_Assembly", loc=RES_POS, rot=(0, RES_TILT, 0))
    r_glass = 0.074
    new_obj("Reservoir_Socket", bm_cyl(0.064, 0.058, 0.040, 64, 'Z', (0, 0, 0.000)),
            "Brushed_Aluminium", bevel=0.006, segs=3, parent=asm)
    new_obj("Reservoir_Bottom", bm_cyl(0.082, 0.082, 0.022, 64, 'Z', (0, 0, 0.030)),
            "Graphite", bevel=0.006, segs=3, parent=asm)
    glass = new_obj("Transparent_Reservoir", bm_cyl(r_glass, r_glass, 0.200, 64, 'Z', (0, 0, 0.141)),
                    "Reservoir_Clear", bevel=0.004, segs=2, parent=asm)

    # Liquide : cylindre plein dont le dessus porte la houle (shape keys)
    liq_bottom, liq_top, r_liq = 0.045, 0.190, r_glass - 0.004
    bm = bm_cyl(r_liq, r_liq, liq_top - liq_bottom, 48, 'Z', (0, 0, (liq_top + liq_bottom) / 2))
    # anneau de sommets sous la surface : la houle ne plie que le haut
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                           plane_co=(0, 0, liq_top - 0.012), plane_no=(0, 0, 1))
    # pas de modificateur : l'export FBX n'ecrit les shape keys que sans modificateurs
    liquid = new_obj("Paint_Liquid", bm, "Liquid_Red", parent=asm, smooth=False)
    for poly in liquid.data.polygons:
        poly.use_smooth = True
    liquid.shape_key_add(name="Basis")
    amp = 0.022
    for key, axis, sign in (("Slosh_PX", 0, 1), ("Slosh_NX", 0, -1), ("Slosh_PY", 1, 1), ("Slosh_NY", 1, -1)):
        sk = liquid.shape_key_add(name=key)
        for i, v in enumerate(liquid.data.vertices):
            if v.co.z > liq_top - 0.001:
                sk.data[i].co.z = v.co.z + sign * amp * (v.co[axis] / r_liq)

    # Reperes des bulles (lus par PaintSprayFX)
    empty("Bubble_Spawn", parent=asm, loc=(0, 0, liq_bottom + 0.008))
    empty("Liquid_Top", parent=asm, loc=(0, 0, liq_top))
    empty("Bubble_Radius", parent=asm, loc=(r_liq - 0.012, 0, liq_bottom + 0.008))
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.006)
    new_obj("Bubble_Template", bm, "Bubbles_Pearl", parent=asm, loc=(0, 0, liq_bottom + 0.02))

    # Graduations sur le verre (deux faces)
    for side in (1, -1):
        for i in range(5):
            w = 0.020 if i % 2 == 0 else 0.012
            bm = bm_box(w, 0.002, 0.003, (0, side * (r_glass + 0.001), 0.075 + i * 0.026))
            new_obj(f"Fill_Mark_{'L' if side > 0 else 'R'}{i}", bm, "Ceramic_White", parent=asm, smooth=False)

    # Cage : 3 montants
    for k in range(3):
        a = math.radians(90 + k * 120)
        x, y = math.cos(a) * (r_glass + 0.010), math.sin(a) * (r_glass + 0.010)
        new_obj(f"Cage_Bar_{k}", bm_cyl(0.0065, 0.0065, 0.214, 16, 'Z', (x, y, 0.145)),
                "Brushed_Aluminium", bevel=0.002, segs=2, parent=asm)

    # Couvercle : bague alu, capuchon crante, valve
    new_obj("Reservoir_Top_Rim", bm_cyl(0.086, 0.086, 0.020, 64, 'Z', (0, 0, 0.250)),
            "Brushed_Aluminium", bevel=0.005, segs=3, parent=asm)
    new_obj("Red_Cap", bm_cyl(0.076, 0.070, 0.026, 64, 'Z', (0, 0, 0.272)),
            "Graphite", bevel=0.006, segs=3, parent=asm)
    for i in range(24):
        a = i * 2 * math.pi / 24
        bm = bm_box(0.006, 0.010, 0.020, (0, 0, 0))
        bmesh.ops.translate(bm, vec=(0.075, 0, 0), verts=bm.verts)
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, 'Z'))
        bmesh.ops.translate(bm, vec=(0, 0, 0.272), verts=bm.verts)
        new_obj(f"Cap_Grip_{i:02d}", bm, "Graphite", bevel=0.0015, segs=1, parent=asm)
    new_obj("Cap_Valve", bm_cyl(0.020, 0.016, 0.022, 32, 'Z', (0, 0, 0.296)),
            "Accent_Red", bevel=0.004, segs=2, parent=asm)
    return asm


def build_hose():
    # Durite du reservoir vers la buse, le long du flanc gauche du dessus
    curve = bpy.data.curves.new("Feed_Hose", 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = 0.011
    curve.bevel_resolution = 6
    curve.use_fill_caps = True
    spline = curve.splines.new('BEZIER')
    pts = [(0.110, -0.050, 0.175), (0.020, -0.075, 0.205), (-0.150, -0.075, 0.190), (-0.240, -0.060, 0.130)]
    spline.bezier_points.add(len(pts) - 1)
    for bp, co in zip(spline.bezier_points, pts):
        bp.co = co
        bp.handle_left_type = bp.handle_right_type = 'AUTO'
    tmp = bpy.data.objects.new("_HoseCurve", curve)
    coll().objects.link(tmp)
    curve.materials.append(material("Graphite"))
    # courbe -> maillage (exportable en FBX comme les autres pieces)
    deps = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(deps))
    me.name = "Feed_Hose"
    bpy.data.objects.remove(tmp, do_unlink=True)
    ob = bpy.data.objects.new("Feed_Hose", me)
    coll().objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    for x, y, z in ((0.110, -0.050, 0.175), (-0.240, -0.060, 0.130)):
        new_obj(f"Hose_Clamp_{'A' if x > 0 else 'B'}", bm_cyl(0.016, 0.016, 0.016, 24, 'Z', (x, y, z)),
                "Brushed_Aluminium", bevel=0.003, segs=2)
    return ob


def flatten_rotations():
    """Rotations/echelles cuites dans les maillages : tous les noeuds exportes
    n'ont plus qu'une position (pivots conserves). L'option FBX 'Apply
    Transform' gere mal les enfants tournes ; ainsi rien ne peut tourner de
    travers a l'import, et les axes se deduisent des reperes en jeu."""
    objs = sorted(coll().objects, key=lambda o: len(o.users_collection) and depth(o))
    worlds = {o.name: o.matrix_world.copy() for o in objs}
    for o in objs:
        w = worlds[o.name]
        loc = w.to_translation()
        lin = w.to_3x3().to_4x4()
        if o.type == 'MESH':
            o.data.transform(lin, shape_keys=True)
    for o in objs:
        o.matrix_parent_inverse.identity()
    for o in objs:
        w = worlds[o.name]
        if o.parent is None:
            o.location = w.to_translation()
        else:
            o.location = w.to_translation() - worlds[o.parent.name].to_translation()
        o.rotation_euler = (0, 0, 0)
        o.scale = (1, 1, 1)


# Repere du FBX v2 (celui pour lequel la pose du pistolet est reglee dans
# la scene Garage) : modele tourne de +90 deg autour de Z (buse vers -Y)
# et remonte de 0,395 m, sous une racine "PaintSpray_Garage".
FRAME_ROT_Z = math.radians(90)
FRAME_OFFSET = Vector((0.0, 0.0, 0.395))


def to_unity_frame():
    frame = empty("_Frame", loc=FRAME_OFFSET, rot=(0, 0, FRAME_ROT_Z))
    for o in list(coll().objects):
        if o.parent is None and o is not frame and not o.name.startswith("_Cut"):
            mw = o.matrix_world.copy()
            o.parent = frame
            o.matrix_world = frame.matrix_world @ mw
    for o in coll().objects:
        if o.name.startswith("_Cut"):
            mw = o.matrix_world.copy()
            o.parent = frame
            o.matrix_world = frame.matrix_world @ mw
    bpy.context.view_layer.update()
    flatten_rotations()
    # racine finale a l'origine, sans rotation ; enfants a leur position monde
    root = empty("PaintSpray_Garage")
    for o in list(frame.children):
        world = o.matrix_world.to_translation()
        o.parent = root
        o.matrix_parent_inverse.identity()
        o.location = world
    bpy.data.objects.remove(frame, do_unlink=True)


def depth(o):
    d = 0
    while o.parent is not None:
        o = o.parent
        d += 1
    return d


def build():
    clear()
    build_body()
    build_nozzle()
    build_rear()
    build_grip()
    build_dial()
    build_reservoir()
    build_hose()
    to_unity_frame()
    return {"objects": len(coll().objects)}


# ---------------------------------------------------------
# EXPORT FBX
# ---------------------------------------------------------

def bake_meshes():
    """Applique les modificateurs (biseaux, booleens, lissage) et nettoie la
    geometrie : les faces d'aire nulle laissees par les booleens/biseaux
    donnent des tangentes NaN dans Unity, et un pixel NaN noircit les sondes
    de lumiere du garage (tout le hangar devient sombre)."""
    deps = bpy.context.evaluated_depsgraph_get()
    removed = 0
    for o in list(coll().objects):
        if o.type != 'MESH' or o.name.startswith("_Cut") or o.data.shape_keys is not None:
            continue
        if o.modifiers:
            me = bpy.data.meshes.new_from_object(o.evaluated_get(deps))
            old = o.data
            o.modifiers.clear()
            o.data = me
            me.name = old.name
            if old.users == 0:
                bpy.data.meshes.remove(old)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        before = len(bm.faces)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges[:])
        tiny = [f for f in bm.faces if f.calc_area() < 1e-9]
        if tiny:
            bmesh.ops.delete(bm, geom=tiny, context='FACES')
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context='VERTS')
        removed += before - len(bm.faces)
        bm.to_mesh(o.data)
        bm.free()
    for o in list(coll().objects):
        if o.name.startswith("_Cut"):
            bpy.data.objects.remove(o, do_unlink=True)
    return removed


def export(path=FBX_PATH):
    bake_meshes()
    objs = [o for o in coll().objects if not o.name.startswith("_Cut")]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.hide_viewport = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        # 'Apply Transform' (bake_space_transform) tourne a tort les enfants
        # d'empties (reservoir, bague...) : conversion d'axes laissee a la racine.
        bake_space_transform=False,
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_tspace=False,
        use_custom_props=False,
        add_leaf_bones=False,
        bake_anim=False,
        use_mesh_edges=False,
        path_mode='AUTO',
        embed_textures=False,
    )
    bpy.ops.object.select_all(action='DESELECT')
    return path
