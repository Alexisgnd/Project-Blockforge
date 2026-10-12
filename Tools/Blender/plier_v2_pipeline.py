# =========================================================
# PINCE DE CONSTRUCTION DU GARAGE - V2 (modele procedural)
# Meme famille que le pistolet a peinture v3 (memes materiaux,
# meme repere, meme export) : coque ceramique, chassis graphite,
# aluminium brosse, accents lumineux cyan.
#
# A lancer dans Blender (Scripting > Run, ou exec du fichier), ou
# en module Python (pip install bpy) :
#   build()        reconstruit tout le modele dans la scene courante
#   export(path)   ecrit le FBX (par defaut Art/Models/Plier_Garage_V2.fbx)
#
# Repere : Z haut, tete vers -X, flancs en +/-Y, metres.
# Contrat avec Unity (PlierV2Setup / PlierToolFX / PlierHologram) :
#   Plier_Head          tete tournante (axe X), pivot au centre du roulement
#     Jaw_0..2          griffes : pivot sur la charniere (s'ouvrent / se ferment)
#     Holo_Lens         lentille de l'emetteur (lumineuse)
#     Head_Glow         anneau lumineux de la tete
#   Holo_Anchor         centre de l'hologramme, entre les pointes des griffes
#   Holo_Size           repere : |Holo_Size - Holo_Anchor| = rayon de l'hologramme
#   Tool_Top            repere au-dessus du corps (haut de l'outil)
#   Status_Strip(_2)    bandeaux lumineux des flancs
#   Cell_Core_0..2      barreaux lumineux de la cellule d'energie
# =========================================================

import math
import os

import bpy  # avant bmesh (module bpy hors de Blender)
import bmesh
from mathutils import Matrix, Vector

PROJECT = r"D:\unity projects\Project-Blockforge"
FBX_PATH = os.path.join(PROJECT, r"Assets\_Project\Art\Models\Plier_Garage_V2.fbx")
COLLECTION = "Plier_V2"

# ---------------------------------------------------------
# MATERIAUX (apercu Blender ; Unity les remappe sur ses .mat HDRP,
# les noms communs avec le pistolet reprennent SES materiaux)
# ---------------------------------------------------------

MATERIALS = {
    "Ceramic_White":     ((0.80, 0.83, 0.86, 1), 0.10, 0.30),
    "Graphite":          ((0.035, 0.045, 0.055, 1), 0.40, 0.40),
    "Rubber_Grip":       ((0.018, 0.022, 0.028, 1), 0.00, 0.75),
    "Brushed_Aluminium": ((0.55, 0.58, 0.62, 1), 0.90, 0.28),
    "Accent_Red":        ((0.65, 0.035, 0.02, 1), 0.25, 0.35),
    "Glow_Accent":       ((0.20, 0.85, 1.00, 1), 0.00, 0.30),
    "Cell_Glass":        ((0.70, 0.90, 1.00, 0.16), 0.00, 0.05),
    "Holo_Lens":         ((0.45, 0.95, 1.00, 1), 0.00, 0.10),
}
EMISSIVE = {"Glow_Accent": 4.0, "Holo_Lens": 8.0}


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    color, metal, rough = MATERIALS[name]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Alpha"].default_value = color[3]
    if name in EMISSIVE:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = EMISSIVE[name]
    if color[3] < 1:
        try:
            mat.surface_render_method = 'BLENDED'
        except Exception:
            pass
    return mat


# ---------------------------------------------------------
# OUTILS DE MODELISATION (memes que paintspray_v3_pipeline)
# ---------------------------------------------------------

def coll():
    c = bpy.data.collections.get(COLLECTION)
    if c is None:
        c = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(c)
    return c


_SMOOTH = None


def smooth_group():
    """Geometry nodes 'lissage par angle' (30 deg) : aretes vives nettes, courbes lisses."""
    global _SMOOTH
    if _SMOOTH is not None:
        return _SMOOTH
    ng = bpy.data.node_groups.get("PL_SmoothByAngle")
    if ng is None:
        ng = bpy.data.node_groups.new("PL_SmoothByAngle", 'GeometryNodeTree')
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


def bm_tube(r_out, r_in, depth, segs=48, axis='X', center=(0, 0, 0)):
    """Anneau epais (tube creux) d'axe donne."""
    bm = bmesh.new()
    outer = bmesh.ops.create_circle(bm, cap_ends=False, segments=segs, radius=r_out)['verts']
    inner = bmesh.ops.create_circle(bm, cap_ends=False, segments=segs, radius=r_in)['verts']
    # face annulaire puis extrusion
    faces = []
    for i in range(segs):
        j = (i + 1) % segs
        faces.append(bm.faces.new((outer[i], outer[j], inner[j], inner[i])))
    bmesh.ops.translate(bm, vec=(0, 0, -depth / 2), verts=bm.verts)
    ext = bmesh.ops.extrude_face_region(bm, geom=faces)
    vs = [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, 0, depth), verts=vs)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
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


def boolean(target, cutter, op='DIFFERENCE'):
    mod = target.modifiers.new("Bool_" + cutter.name, 'BOOLEAN')
    mod.operation = op
    mod.object = cutter
    mod.solver = 'EXACT'
    target.modifiers.move(len(target.modifiers) - 1, 0)
    cutter.hide_viewport = True
    cutter.hide_render = True
    return mod


# ---------------------------------------------------------
# CONSTRUCTION
# ---------------------------------------------------------

AXIS_Z = 0.070                          # hauteur de l'axe de la tete
HEAD_X = -0.205                         # centre du roulement de la tete
HINGE_X = -0.262                        # charnieres des griffes
HINGE_R = 0.066                         # rayon des charnieres
JAW_ANGLES = (90.0, 210.0, 330.0)       # autour de l'axe X (0 = +Y, 90 = +Z)
HOLO_X = -0.478                         # centre de l'hologramme (juste devant les pointes des griffes)
HOLO_R = 0.052                          # rayon de l'hologramme


def clear():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)


def build_body():
    # Coque superieure en ceramique : silhouette en coin, nez abaisse vers la tete
    shell = new_obj("Upper_Shell", bm_profile([
        (0.255, 0.010), (0.255, 0.140), (0.215, 0.168), (-0.060, 0.168),
        (-0.150, 0.140), (-0.178, 0.112), (-0.178, 0.010),
    ], 0.092), "Ceramic_White", bevel=0.022, segs=5)
    groove = new_obj("_Cut_TopGroove", bm_box(0.26, 0.012, 0.02, (0.03, 0, 0.172)), None, smooth=False)
    boolean(shell, groove)

    # Chassis inferieur graphite, en retrait (ligne d'ombre)
    new_obj("Body_Core", bm_profile([
        (0.240, -0.070), (0.240, 0.030), (-0.170, 0.030), (-0.170, -0.012),
        (-0.120, -0.055), (0.060, -0.070),
    ], 0.082), "Graphite", bevel=0.016, segs=4)

    for side, suffix in ((1, ""), (-1, "_2")):
        # Plaque laterale en ceramique, decoupee en chevron
        bm = bm_profile([(-0.150, 0.000), (0.010, 0.000), (0.040, -0.038), (-0.110, -0.038)], 0.009)
        bmesh.ops.translate(bm, vec=(0, side * 0.084, 0), verts=bm.verts)
        new_obj("Side_Armour" + suffix, bm, "Ceramic_White", bevel=0.005, segs=3)

        # Bandeau lumineux dans sa gorge graphite (en chevron vers la tete)
        bm = bm_box(0.15, 0.008, 0.012, (0.020, side * 0.092, 0.075))
        new_obj("Status_Groove" + suffix, bm, "Graphite", bevel=0.004, segs=2)
        bm = bm_box(0.13, 0.006, 0.0055, (0.020, side * 0.0955, 0.075))
        new_obj("Status_Strip" + suffix, bm, "Glow_Accent", bevel=0.002, segs=2)

        # Ouies a l'arriere
        for i in range(5):
            bm = bm_box(0.008, 0.010, 0.050, (0.170 + i * 0.015, side * 0.091, 0.098))
            new_obj(f"Vent{suffix}_{i}", bm, "Graphite", bevel=0.0025, segs=2)

        # Vis
        for j, (x, z) in enumerate(((-0.140, 0.125), (0.225, 0.030), (-0.140, 0.022), (0.225, 0.125))):
            bm = bm_cyl(0.006, 0.006, 0.006, 16, 'Y', (x, side * 0.0925, z))
            new_obj(f"Screw{suffix}_{j}", bm, "Brushed_Aluminium", bevel=0.0015, segs=2)

    # Plaque arriere + LED de statut
    new_obj("Rear_Plate", bm_box(0.014, 0.140, 0.120, (0.259, 0, 0.075)), "Graphite", bevel=0.006, segs=3)
    for i in range(3):
        new_obj(f"Rear_Led_{i}", bm_box(0.004, 0.018, 0.006, (0.267, -0.030 + i * 0.030, 0.105)),
                "Glow_Accent", bevel=0.0015, segs=1)
    new_obj("Rear_Badge", bm_box(0.004, 0.070, 0.022, (0.267, 0, 0.060)), "Accent_Red", bevel=0.002, segs=2)


def build_power_cell():
    """Cellule d'energie horizontale sur le dessus : verre, 3 barreaux lumineux, embouts alu."""
    z = 0.208
    x0, x1 = -0.080, 0.190
    cx = (x0 + x1) / 2
    length = x1 - x0
    # berceau graphite
    new_obj("Cell_Cradle", bm_profile([
        (x0 - 0.012, 0.160), (x1 + 0.012, 0.160), (x1 + 0.012, 0.182), (x0 - 0.012, 0.182),
    ], 0.040), "Graphite", bevel=0.006, segs=3)
    # verre
    new_obj("Cell_Glass", bm_cyl(0.034, 0.034, length - 0.020, 48, 'X', (cx, 0, z)),
            "Cell_Glass", bevel=0.003, segs=2)
    # barreaux lumineux
    for k in range(3):
        a = math.radians(90 + k * 120)
        y, zz = math.cos(a) * 0.014, math.sin(a) * 0.014
        new_obj(f"Cell_Core_{k}", bm_cyl(0.0055, 0.0055, length - 0.040, 16, 'X', (cx, y, z + zz)),
                "Glow_Accent", smooth=True)
    # embouts
    for x, name in ((x0, "Cell_Cap_Front"), (x1, "Cell_Cap_Rear")):
        new_obj(name, bm_cyl(0.041, 0.041, 0.022, 48, 'X', (x, 0, z)), "Brushed_Aluminium", bevel=0.005, segs=3)
        new_obj(name + "_Ring", bm_cyl(0.043, 0.043, 0.006, 48, 'X', (x + (0.014 if x == x0 else -0.014), 0, z)),
                "Graphite", bevel=0.0015, segs=1)
    # colliers
    for i, x in enumerate((cx - 0.070, cx + 0.070)):
        new_obj(f"Cell_Clamp_{i}", bm_tube(0.040, 0.034, 0.014, 48, 'X', (x, 0, z)),
                "Graphite", bevel=0.002, segs=2)
    # cable de la cellule vers la tete
    curve = bpy.data.curves.new("Cell_Cable", 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = 0.008
    curve.bevel_resolution = 5
    curve.use_fill_caps = True
    spline = curve.splines.new('BEZIER')
    pts = [(x0 - 0.010, -0.022, z - 0.012), (-0.120, -0.050, 0.170), (-0.170, -0.060, 0.120)]
    spline.bezier_points.add(len(pts) - 1)
    for bp, co in zip(spline.bezier_points, pts):
        bp.co = co
        bp.handle_left_type = bp.handle_right_type = 'AUTO'
    tmp = bpy.data.objects.new("_CableCurve", curve)
    coll().objects.link(tmp)
    curve.materials.append(material("Graphite"))
    deps = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(deps))
    me.name = "Cell_Cable"
    bpy.data.objects.remove(tmp, do_unlink=True)
    ob = bpy.data.objects.new("Cell_Cable", me)
    coll().objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True


def build_grip():
    pivot = Vector((0.120, 0, -0.045))
    tilt = math.radians(-14)
    grip_prof = [
        (-0.038, 0.000), (0.040, 0.000), (0.044, -0.115), (0.038, -0.240),
        (0.032, -0.278), (-0.028, -0.278), (-0.034, -0.240), (-0.042, -0.115),
    ]
    grip = new_obj("Grip", bm_profile(grip_prof, 0.038), "Rubber_Grip", bevel=0.015, segs=4,
                   loc=pivot, rot=(0, tilt, 0))
    for i in range(3):
        bm = bm_cyl(0.021, 0.021, 0.12, 32, 'Y', (-0.058, 0, -0.070 - i * 0.062))
        cutter = new_obj(f"_Cut_Finger_{i}", bm, None, smooth=False, loc=pivot, rot=(0, tilt, 0))
        boolean(grip, cutter)
    for side in (1, -1):
        for i in range(6):
            bm = bm_box(0.048, 0.006, 0.007, (0.004, side * 0.038, -0.058 - i * 0.029))
            new_obj(f"Grip_Rib_{'L' if side > 0 else 'R'}{i}", bm, "Graphite", bevel=0.0025, segs=2,
                    loc=pivot, rot=(0, tilt, 0))
    new_obj("Grip_Backstrap", bm_profile([
        (0.028, -0.010), (0.050, -0.010), (0.052, -0.115), (0.046, -0.240), (0.026, -0.240),
    ], 0.028), "Ceramic_White", bevel=0.008, segs=3, loc=pivot, rot=(0, tilt, 0))
    new_obj("Grip_Heel", bm_box(0.090, 0.078, 0.025, (0.002, 0, -0.288)), "Brushed_Aluminium",
            bevel=0.009, segs=3, loc=pivot, rot=(0, tilt, 0))
    trig = bm_profile([(0.000, 0.000), (0.016, 0.000), (0.010, -0.030), (-0.004, -0.060),
                       (-0.018, -0.068), (-0.010, -0.038)], 0.010)
    new_obj("Trigger", trig, "Brushed_Aluminium", bevel=0.004, segs=2, loc=(0.035, 0, -0.058))
    guard = bm_profile([
        (0.075, -0.058), (0.075, -0.140), (-0.060, -0.140), (-0.090, -0.106), (-0.090, -0.050),
        (-0.076, -0.050), (-0.076, -0.102), (-0.054, -0.126), (0.061, -0.126), (0.061, -0.058),
    ], 0.012)
    new_obj("Trigger_Guard", guard, "Graphite", bevel=0.004, segs=2)


def build_neck():
    """Col entre le corps et la tete : bague alu, roulement graphite, anneau lumineux fixe."""
    z = AXIS_Z
    new_obj("Neck_Collar", bm_cyl(0.074, 0.070, 0.034, 64, 'X', (-0.180, 0, z)),
            "Brushed_Aluminium", bevel=0.005, segs=3)
    for i in range(3):
        new_obj(f"Neck_Fin_{i}", bm_cyl(0.076, 0.076, 0.004, 64, 'X', (-0.168 - i * 0.010, 0, z)),
                "Graphite", bevel=0.001, segs=1)
    new_obj("Head_Bearing", bm_tube(0.080, 0.050, 0.014, 64, 'X', (HEAD_X + 0.010, 0, z)),
            "Graphite", bevel=0.003, segs=2)


def jaw_matrix(angle_deg):
    """Repere local d'une griffe : x = vers l'avant (-X monde), y = tangente, z = radial."""
    a = math.radians(angle_deg)
    radial = Vector((0, math.cos(a), math.sin(a)))
    tangent = Vector((0, -math.sin(a), math.cos(a)))
    forward = Vector((-1, 0, 0))
    m = Matrix((forward, tangent, radial)).transposed()  # colonnes
    return m.to_4x4(), radial


def build_head():
    z = AXIS_Z
    head = empty("Plier_Head", loc=(HEAD_X, 0, z), size=0.04)

    # Moyeu de tete : ceramique, cone vers l'avant + anneau lumineux
    new_obj("Head_Hub", bm_cyl(0.076, 0.060, 0.058, 64, 'X', (HEAD_X - 0.040 - HEAD_X, 0, 0)),
            "Ceramic_White", bevel=0.008, segs=4, parent=head)
    new_obj("Head_Glow", bm_cyl(0.0775, 0.0775, 0.006, 64, 'X', (-0.012, 0, 0)),
            "Glow_Accent", bevel=0.0015, segs=1, parent=head)
    new_obj("Head_Ring", bm_cyl(0.079, 0.079, 0.010, 64, 'X', (-0.003, 0, 0)),
            "Brushed_Aluminium", bevel=0.002, segs=2, parent=head)
    # Nez graphite et emetteur
    new_obj("Emitter_Body", bm_cyl(0.058, 0.044, 0.030, 64, 'X', (-0.082, 0, 0)),
            "Graphite", bevel=0.005, segs=3, parent=head)
    new_obj("Emitter_Bezel", bm_tube(0.044, 0.030, 0.012, 64, 'X', (-0.100, 0, 0)),
            "Brushed_Aluminium", bevel=0.002, segs=2, parent=head)
    new_obj("Holo_Lens", bm_cyl(0.031, 0.024, 0.006, 64, 'X', (-0.099, 0, 0)),
            "Holo_Lens", bevel=0.0015, segs=2, parent=head)

    # Griffes
    for i, ang in enumerate(JAW_ANGLES):
        m, radial = jaw_matrix(ang)
        hinge = Vector((HINGE_X - HEAD_X, 0, 0)) + radial * HINGE_R   # relatif a la tete
        jaw = empty(f"Jaw_{i}", parent=head, loc=hinge, size=0.02)

        def put(bm, name, mat, bevel=0.0, segs=3):
            bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
            return new_obj(name, bm, mat, bevel=bevel, segs=segs, parent=jaw)

        # Chape de charniere sur le moyeu (graphite) + axe alu
        put(bm_box(0.034, 0.040, 0.020, (0.004, 0, -0.008)), f"Jaw_{i}_Clevis", "Graphite", 0.004, 2)
        bm = bm_cyl(0.011, 0.011, 0.046, 24, 'Y', (0, 0, 0))
        put(bm, f"Jaw_{i}_Pin", "Brushed_Aluminium", 0.002, 2)

        # Doigt : silhouette courbe (avance, s'ecarte puis revient vers l'axe)
        finger = bm_profile([
            (-0.010, -0.010), (-0.010, 0.012), (0.040, 0.030), (0.095, 0.034), (0.140, 0.016),
            (0.162, -0.010), (0.160, -0.030), (0.140, -0.034), (0.118, -0.016), (0.080, -0.004),
            (0.035, -0.010),
        ], 0.013)
        put(finger, f"Jaw_{i}_Finger", "Ceramic_White", 0.006, 4)
        # Dos graphite (nervure) et pointe graphite
        spine = bm_profile([(0.020, 0.024), (0.090, 0.036), (0.132, 0.022), (0.128, 0.016),
                            (0.088, 0.028), (0.024, 0.018)], 0.007)
        put(spine, f"Jaw_{i}_Spine", "Graphite", 0.002, 2)
        tip = bm_profile([(0.140, -0.034), (0.162, -0.010), (0.170, -0.020), (0.166, -0.040),
                          (0.150, -0.044)], 0.012)
        put(tip, f"Jaw_{i}_Tip", "Graphite", 0.003, 2)
        # Liseré lumineux sur la face interieure
        strip = bm_profile([(0.040, -0.0115), (0.082, -0.0055), (0.120, -0.0185), (0.118, -0.0225),
                            (0.081, -0.0095), (0.040, -0.0155)], 0.0035)
        put(strip, f"Jaw_{i}_Glow", "Glow_Accent", 0.0, 1)
        # Verins (alu) de part et d'autre du doigt, du moyeu vers le milieu du doigt
        for t, side in ((0.0165, "L"), (-0.0165, "R")):
            bm = bm_cyl(0.0040, 0.0040, 0.058, 16, 'X', (0.022, t, 0.004))
            bmesh.ops.rotate(bm, verts=bm.verts, cent=(0.022, t, 0.004), matrix=Matrix.Rotation(math.radians(-16), 3, 'Y'))
            put(bm, f"Jaw_{i}_Piston_{side}", "Brushed_Aluminium", 0.0, 1)

    # Reperes de l'hologramme (fixes : l'hologramme ne roule pas avec la tete)
    empty("Holo_Anchor", loc=(HOLO_X, 0, z), size=0.03)
    empty("Holo_Size", loc=(HOLO_X, 0, z + HOLO_R), size=0.01)
    empty("Tool_Top", loc=(0.0, 0, 0.30), size=0.02)
    return head


# ---------------------------------------------------------
# REPERE UNITY + EXPORT (memes conventions que le pistolet v3)
# ---------------------------------------------------------

def depth(o):
    d = 0
    while o.parent is not None:
        o = o.parent
        d += 1
    return d


def flatten_rotations():
    """Rotations/echelles cuites dans les maillages : chaque noeud exporte n'a
    plus qu'une position (pivots conserves), sans rotation ni echelle."""
    bpy.context.view_layer.update()
    objs = sorted(coll().objects, key=depth)
    worlds = {o.name: o.matrix_world.copy() for o in objs}
    for o in objs:
        lin = worlds[o.name].to_3x3().to_4x4()
        if o.type == 'MESH':
            o.data.transform(lin)
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
    return worlds


FRAME_ROT_Z = math.radians(90)
FRAME_OFFSET = Vector((0.0, 0.0, 0.395))


def to_unity_frame():
    frame = empty("_Frame", loc=FRAME_OFFSET, rot=(0, 0, FRAME_ROT_Z))
    bpy.context.view_layer.update()
    for o in list(coll().objects):
        if o.parent is None and o is not frame:
            mw = o.matrix_world.copy()
            o.parent = frame
            o.matrix_world = frame.matrix_world @ mw
    bpy.context.view_layer.update()
    worlds = flatten_rotations()
    root = empty("Plier_V2")
    for o in list(frame.children):
        # position monde AVANT l'aplatissement (independant du rafraichissement des matrices)
        world = worlds[o.name].to_translation()
        o.parent = root
        o.matrix_parent_inverse.identity()
        o.location = world
    bpy.data.objects.remove(frame, do_unlink=True)
    bpy.context.view_layer.update()


def build():
    clear()
    build_body()
    build_power_cell()
    build_grip()
    build_neck()
    build_head()
    to_unity_frame()
    return {"objects": len(coll().objects)}


def bake_meshes():
    """Applique les modificateurs et nettoie la geometrie (faces d'aire nulle =
    tangentes NaN dans Unity, qui noircissent les sondes de lumiere)."""
    deps = bpy.context.evaluated_depsgraph_get()
    removed = 0
    for o in list(coll().objects):
        if o.type != 'MESH' or o.name.startswith("_Cut"):
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


if __name__ == "__main__":
    print(build())
