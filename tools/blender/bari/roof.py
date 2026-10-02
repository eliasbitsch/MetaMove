# Roof fragment over the Bari cell, built with the helpers of bari_assets.py prepended:
#   two HEB columns in front of the wall, a crossbeam on top, two purlins running forward over
#   the cell, a second crossbeam between them, and a sign panel on two chains.
# Coordinates in the Unity cell frame measured from the FLOOR (X right, Y up, Z to the visitor);
# U() converts to Blender, the FBX lands in Unity in these numbers.
# The printed face of the sign is a quad added in Unity (texture kept out of the public repo).
import math

def U(x, y, z):
    return (-x, -z, y)

for o in [o for o in bpy.data.objects if o.name.startswith("Roof_")]:
    bpy.data.objects.remove(o, do_unlink=True)

steel = mat("FacSteel", (0.35, 0.37, 0.40), metal=0.8, rough=0.4)
chain_mat = mat("FacChain", (0.55, 0.56, 0.58), metal=0.9, rough=0.35)
panel = mat("FacSignBack", (0.10, 0.12, 0.14), metal=0.5, rough=0.5)

H = 0.20          # HEB 200: height = flange width
TF, TW = 0.015, 0.009

def ibeam(axis, c, length, name):
    """I-profile centred at c (cell coords), web vertical, running along 'x', 'z' or 'y' (column)."""
    x, y, z = c
    if axis == "x":
        web = box((length, TW, H - 2 * TF), U(x, y, z), name, steel)
        f1 = box((length, H, TF), U(x, y + H / 2 - TF / 2, z), "f", steel)
        f2 = box((length, H, TF), U(x, y - H / 2 + TF / 2, z), "f", steel)
    elif axis == "z":
        web = box((TW, length, H - 2 * TF), U(x, y, z), name, steel)
        f1 = box((H, length, TF), U(x, y + H / 2 - TF / 2, z), "f", steel)
        f2 = box((H, length, TF), U(x, y - H / 2 + TF / 2, z), "f", steel)
    else:  # column: web along x, flanges facing +-z
        web = box((H - 2 * TF, TW, length), U(x, y, z), name, steel)
        f1 = box((H, TF, length), U(x, y, z + H / 2 - TF / 2), "f", steel)
        f2 = box((H, TF, length), U(x, y, z - H / 2 + TF / 2), "f", steel)
    return join([web, f1, f2], name)

COL_X, COL_Z = 1.55, -1.10
TOP = 3.25                     # centre height of the horizontal beams
PURLIN_X, PURLIN_Z1 = 1.00, 1.00   # out over the rack: the spots sit on their tips
SIGN_Z = 0.45                  # crossbeam B and the sign hang here, in front of the lamp
SIGN_W, SIGN_H, SIGN_Y = 1.60, 0.512, 2.52

parts = []
for sx in (-1, 1):
    parts.append(ibeam("y", (sx * COL_X, (TOP - H / 2) / 2, COL_Z), TOP - H / 2, "c"))
    # base plate
    parts.append(box((0.32, 0.32, 0.02), U(sx * COL_X, 0.01, COL_Z), "bp", steel))
parts.append(ibeam("x", (0.0, TOP, COL_Z), 2 * COL_X + H, "a"))
for sx in (-1, 1):
    L = PURLIN_Z1 - COL_Z
    parts.append(ibeam("z", (sx * PURLIN_X, TOP, COL_Z + L / 2), L, "p"))
parts.append(ibeam("x", (0.0, TOP, SIGN_Z), 2 * PURLIN_X, "b"))
frame = join(parts, "Roof_Frame")

# chains: alternating links from the lower flange of crossbeam B to the sign's eye bolts
links = []
top_y = TOP - H / 2
bot_y = SIGN_Y + SIGN_H / 2 + 0.03
for sx in (-0.62, 0.62):
    n = int((top_y - bot_y) / 0.034)
    for i in range(n):
        y = top_y - 0.02 - i * 0.034
        bpy.ops.mesh.primitive_torus_add(major_radius=0.019, minor_radius=0.0045, major_segments=10, minor_segments=4,
                                         location=U(sx, y, SIGN_Z),
                                         rotation=(0.0, math.pi / 2, (math.pi / 2) * (i % 2)))
        lk = bpy.context.active_object; lk.scale = (1.0, 1.0, 1.6)
        bpy.ops.object.transform_apply(scale=True)
        lk.data.materials.append(chain_mat); links.append(lk)
    # eye bolt on the sign
    bpy.ops.mesh.primitive_torus_add(major_radius=0.022, minor_radius=0.006, major_segments=10, minor_segments=4,
                                     location=U(sx, SIGN_Y + SIGN_H / 2 + 0.02, SIGN_Z), rotation=(0.0, math.pi / 2, 0.0))
    eb = bpy.context.active_object; eb.data.materials.append(chain_mat); links.append(eb)
chains = join(links, "Roof_Chains")

# sign panel: dark backing plate with a steel frame; the face quad goes on in Unity at +Z
back = box((SIGN_W, 0.03, SIGN_H), U(0.0, SIGN_Y, SIGN_Z - 0.005), "Roof_SignPanel", panel)
fr = [box((SIGN_W + 0.05, 0.045, 0.025), U(0.0, SIGN_Y + sy * (SIGN_H / 2 + 0.0125), SIGN_Z), "sf", steel) for sy in (-1, 1)]
fr += [box((0.025, 0.045, SIGN_H + 0.05), U(sx * (SIGN_W / 2 + 0.0125), SIGN_Y, SIGN_Z), "sf", steel) for sx in (-1, 1)]
sign = join([back] + fr, "Roof_Sign")

# two spots on the purlin tips, aimed obliquely at the sign face
from mathutils import Vector
SPOT_Y = TOP - H / 2 - 0.09
spots = []
aim = Vector((0.0, SIGN_Y, SIGN_Z + 0.013))
for sx in (-1, 1):
    p = Vector((sx * PURLIN_X, SPOT_Y, PURLIN_Z1 - 0.06))
    d = (aim - p).normalized()
    db = Vector(U(d.x, d.y, d.z))
    q = db.to_track_quat("Z", "Y")
    bpy.ops.mesh.primitive_cylinder_add(radius=0.055, depth=0.16, vertices=16, location=U(p.x, p.y, p.z))
    h = bpy.context.active_object; h.rotation_mode = "QUATERNION"; h.rotation_quaternion = q
    h.data.materials.append(mat("FacLampShade", (0.12, 0.13, 0.14), metal=0.5, rough=0.4))
    bpy.ops.object.transform_apply(rotation=True)
    front = p + d * 0.081
    bpy.ops.mesh.primitive_cylinder_add(radius=0.047, depth=0.004, vertices=16, location=U(front.x, front.y, front.z))
    g = bpy.context.active_object; g.rotation_mode = "QUATERNION"; g.rotation_quaternion = q
    g.data.materials.append(mat("FacLampGlow", (1.0, 0.92, 0.75), rough=0.3, emit=(1.0, 0.92, 0.75)))
    bpy.ops.object.transform_apply(rotation=True)
    yoke = box((0.03, 0.03, 0.09), U(p.x, p.y + 0.06, p.z), "y", steel)
    spots += [h, g, yoke]
    print("spot", sx, "lens at (cell, floor-relative)", tuple(round(v, 3) for v in front), "dir", tuple(round(v, 3) for v in d))
spot_obj = join(spots, "Roof_Spots")

export("bari_roof", [frame, chains, sign, spot_obj])
print("roof ok; sign face centre (cell, floor-relative):", (0.0, SIGN_Y, SIGN_Z + 0.011), "size", (SIGN_W, SIGN_H))
