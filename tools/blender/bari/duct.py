# Extraction: a wall exhaust fan in the broken wall (rotor exported as its own object, pivot at
# the hub, spun in Unity) and a galvanised round duct from the fan up to the roof fragment,
# along the right purlin over the cell, ending in a suction bell. Helpers of bari_assets.py
# prepended. Coordinates in the Unity cell frame from the FLOOR (X right, Y up, Z to the visitor).
import math

def U(x, y, z):
    return (-x, -z, y)

for o in [o for o in bpy.data.objects if o.name.startswith("Duct_")]:
    bpy.data.objects.remove(o, do_unlink=True)

galv = mat("Profile", (0.78, 0.8, 0.82), metal=0.9, rough=0.35)
dark = mat("ControllerDark", (0.16, 0.17, 0.19), metal=0.3, rough=0.6)
blade_mat = mat("FacSteel", (0.35, 0.37, 0.40), metal=0.8, rough=0.4)

WALL_FACE = -1.29
FX, FY = 1.05, 1.80            # fan centre on the wall
FZ = WALL_FACE + 0.10          # housing stands proud of the wall
R = 0.24                       # opening radius
DUCT_R = 0.11

# ---------------------------------------------------------------- housing + guard
house = [box((0.62, 0.20, 0.62), U(FX, FY, FZ - 0.05), "Duct_Housing", dark)]
bpy.ops.mesh.primitive_cylinder_add(radius=R, depth=0.24, vertices=32, location=U(FX, FY, FZ), rotation=(math.pi / 2, 0, 0))
hole = bpy.context.active_object
cut(house[0], hole)
bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=0.012, major_segments=32, minor_segments=6,
                                 location=U(FX, FY, FZ + 0.055), rotation=(math.pi / 2, 0, 0))
ring = bpy.context.active_object; ring.data.materials.append(galv); house.append(ring)
for i in range(5):   # guard: wires across the opening
    yy = FY - R * 0.8 + i * R * 0.4
    house.append(box((2 * math.sqrt(max(0.0, R * R - (yy - FY) ** 2)), 0.006, 0.006), U(FX, yy, FZ + 0.06), "g", galv))
housing = join(house, "Duct_Housing")

# ---------------------------------------------------------------- rotor (own object, origin at the hub)
rot = []
bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=0.08, vertices=16, location=U(FX, FY, FZ), rotation=(math.pi / 2, 0, 0))
hub = bpy.context.active_object; hub.data.materials.append(dark); rot.append(hub)
# blades from computed corners (fan plane = cell XY, axis = cell Z): radius 0.05..0.22,
# chord 0.075 pitched 30 deg about the radius
for i in range(5):
    a = i * 2 * math.pi / 5
    rad = (math.cos(a), math.sin(a)); tan = (-math.sin(a), math.cos(a))
    pitch = math.radians(30)
    bm = bmesh.new()
    for r in (0.05, 0.22):
        for c in (-0.0375, 0.0375):
            for t in (-0.004, 0.004):
                x = FX + rad[0] * r + tan[0] * c * math.cos(pitch)
                y = FY + rad[1] * r + tan[1] * c * math.cos(pitch)
                z = FZ + c * math.sin(pitch) + t
                bm.verts.new(U(x, y, z))
    bmesh.ops.convex_hull(bm, input=bm.verts)
    rot.append(obj_from_bm(bm, "b", blade_mat))
rotor = join(rot, "Duct_Rotor")
bpy.context.scene.cursor.location = U(FX, FY, FZ)
bpy.ops.object.select_all(action="DESELECT"); rotor.select_set(True); bpy.context.view_layer.objects.active = rotor
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")

# ---------------------------------------------------------------- duct: fan top -> roof -> along the purlin -> bell
def pipe(points, radius, name):
    cd = bpy.data.curves.new(name, type="CURVE")
    cd.dimensions = "3D"
    cd.bevel_depth = radius
    cd.bevel_resolution = 3
    cd.resolution_u = 10
    cd.use_fill_caps = False
    sp = cd.splines.new("BEZIER")
    sp.bezier_points.add(len(points) - 1)
    for bp, p in zip(sp.bezier_points, points):
        bp.co = U(*p)
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    ob = bpy.data.objects.new(name, cd)
    bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.convert(target="MESH")
    ob = bpy.context.active_object
    ob.data.materials.append(galv)
    return ob

TOPY = 2.95
path = [(FX, FY + 0.31, FZ - 0.05), (FX, 2.40, FZ - 0.05), (FX, TOPY - 0.12, FZ - 0.03), (FX, TOPY, FZ + 0.12),
        (FX - 0.02, TOPY, -0.60), (FX - 0.04, TOPY, 0.05), (FX - 0.05, TOPY - 0.10, 0.22), (FX - 0.05, 2.62, 0.26)]
duct = [pipe(path, DUCT_R, "Duct_Pipe")]
# ring bands every ~0.6 m along the straight run, the bell at the end, two hangers to the purlin
for z in (-0.95, -0.35, 0.05):
    bpy.ops.mesh.primitive_torus_add(major_radius=DUCT_R + 0.004, minor_radius=0.008, major_segments=24, minor_segments=4,
                                     location=U(FX - 0.03, TOPY, z), rotation=(math.pi / 2, 0, 0))
    b = bpy.context.active_object; b.data.materials.append(galv); duct.append(b)
bpy.ops.mesh.primitive_cone_add(radius1=0.22, radius2=DUCT_R + 0.005, depth=0.16, vertices=24, location=U(FX - 0.05, 2.52, 0.26))
bell = bpy.context.active_object; bell.data.materials.append(galv); duct.append(bell)
for z in (-0.65, -0.05):
    duct.append(box((0.01, 0.01, 0.18), U(FX - 0.03, TOPY + 0.19, z), "hang", galv))
pipe_obj = join(duct, "Duct_Pipe")

export("bari_duct", [housing, rotor, pipe_obj])
print("duct ok; rotor hub (cell, floor-relative)", (FX, FY, FZ))
