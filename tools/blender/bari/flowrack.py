# Flow rack v2 + compact robot table + lighter gears, built in the live Blender (helpers prepended).
# Blender frame: X = rack width, -Y = pick face (towards the robot), +Y = loading side, Z up, floor z = 0.
# Everything is merged per material so the rack costs a handful of draw calls on the Quest.
import math

for o in [o for o in bpy.data.objects if o.name.startswith(("Rack", "KLT4", "Lane", "RobotTable"))]:
    bpy.data.objects.remove(o, do_unlink=True)

steel_blue = mat("RackBlue", (0.08, 0.25, 0.62), metal=0.4, rough=0.45)
galv = mat("Profile", (0.78, 0.8, 0.82), metal=0.9, rough=0.35)
roller_mat = mat("Roller", (0.12, 0.12, 0.13), metal=0.2, rough=0.5)
rubber = mat("Rubber", (0.03, 0.03, 0.03), rough=0.9)

RW, RD, RH = 1.24, 0.86, 1.30
POST = 0.04
Y0, Y1 = RD / 2 - POST, -RD / 2 + POST      # loading side, pick side
LEN = Y0 - Y1
LANE_X = (-0.38, 0.0, 0.38)                  # three box lanes, KLT 300 wide across the lane
SUPPLY = (0.90 + 0.11, 0.90)                 # z at loading end, z at pick end (7 deg)
RETURN = (0.30, 0.38)                        # empties roll back to the loading side

def bake(o):
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def lane_level(name, z_back, z_front, ret):
    frame_bits, rollers = [], []
    for sx in (-1, 1):
        frame_bits.append(box((0.03, LEN, 0.05), (sx * (RW / 2 - POST - 0.015), 0, -0.012), "b", steel_blue))
    stop_y = (-LEN / 2 + 0.012) if not ret else (LEN / 2 - 0.012)
    frame_bits.append(box((RW - 2 * POST, 0.02, 0.06), (0, stop_y, 0.022), "b", steel_blue))
    tracks = []
    for lx in LANE_X:
        for tx in (lx - 0.10, lx + 0.10):
            for wx in (-0.016, 0.016):
                tracks.append(box((0.003, LEN, 0.022), (tx + wx, 0, -0.004), "b", galv))
            tracks.append(box((0.035, LEN, 0.003), (tx, 0, -0.014), "b", galv))
            n = int(LEN / 0.05)
            for i in range(n):
                y = -LEN / 2 + (i + 0.5) * LEN / n
                bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.022, vertices=10,
                                                    location=(tx, y, 0.0), rotation=(0, math.pi / 2, 0))
                ro = bpy.context.active_object; ro.data.materials.append(roller_mat); rollers.append(ro)
    fr = join(frame_bits, name + "Frame"); bake(fr)
    tr = join(tracks, name + "Tracks"); bake(tr)
    rl = join(rollers, name + "Rollers"); bake(rl)
    tilt = math.atan2(z_back - z_front, LEN)
    for o in (fr, tr, rl):
        o.rotation_euler = (tilt, 0, 0); o.location = (0, 0, (z_back + z_front) / 2); bake(o)
    return [fr, tr, rl]

blue, metal, rolls, wheels = [], [], [], []
# uprights with a hole pattern, casters
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * (RW / 2 - POST / 2), sy * (RD / 2 - POST / 2)
        u = box((POST, POST, RH), (x, y, RH / 2 + 0.06), "RackPost", steel_blue)
        holes = [box((0.012, POST + 0.01, 0.008), (x, y, 0.12 + k * 0.03), "h", None) for k in range(int((RH - 0.1) / 0.03))]
        cut(u, join(holes, "cutter"))
        blue.append(u)
        metal.append(box((0.07, 0.07, 0.006), (x, y, 0.057), "RackCasterPlate", galv))
        bpy.ops.mesh.primitive_cylinder_add(radius=0.045, depth=0.03, vertices=16, location=(x, y + 0.02, 0.045), rotation=(math.pi / 2, 0, 0))
        w = bpy.context.active_object; w.data.materials.append(rubber); wheels.append(w)
# side ties and end cross beams
for sx in (-1, 1):
    for z in (0.12, 0.62, RH + 0.04):
        blue.append(box((0.03, RD - 2 * POST, 0.03), (sx * (RW / 2 - POST / 2), 0, z), "RackTie", steel_blue))
for (zb, zf) in (SUPPLY, RETURN):
    blue.append(box((RW - 2 * POST, 0.035, 0.05), (0, Y0, zb - 0.035), "RackBeam", steel_blue))
    blue.append(box((RW - 2 * POST, 0.035, 0.05), (0, Y1, zf - 0.035), "RackBeam", steel_blue))
# kanban board on the loading side
metal.append(box((RW, 0.02, 0.16), (0, RD / 2 - 0.01, RH - 0.02), "RackBoard", galv))

for name, (zb, zf), ret in (("Supply", SUPPLY, False), ("Return", RETURN, True)):
    fr, tr, rl = lane_level("Lane" + name, zb, zf, ret)
    blue.append(fr); metal.append(tr); rolls.append(rl)

rack = [join(blue, "RackFrame"), join(metal, "RackTracks"), join(rolls, "RackRollers"), join(wheels, "RackWheels")]
for o in rack: bake(o)
export("bari_flowrack", rack)

# ------------------------------------------------------------ compact robot table 1.0 x 0.6
for o in [o for o in bpy.data.objects if o.name in ("TableFrame", "TableTop")]:
    pass
alu = mat("Profile", (0.78, 0.8, 0.82), metal=0.9, rough=0.35)
esd = mat("ESDTop", (0.16, 0.24, 0.22), rough=0.8)
TW, TD, TH = 1.00, 0.60, 0.80
bits = []
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * (TW / 2 - 0.04), sy * (TD / 2 - 0.04)
        bits.append(box((0.04, 0.04, TH - 0.03), (x, y, (TH - 0.03) / 2), "RobotTableLeg", alu))
        bits.append(box((0.08, 0.08, 0.008), (x, y, 0.004), "RobotTableFoot", alu))
for z in (0.12, TH - 0.05):
    for sy in (-1, 1): bits.append(box((TW - 0.08, 0.04, 0.04), (0, sy * (TD / 2 - 0.04), z), "RobotTableBeam", alu))
    for sx in (-1, 1): bits.append(box((0.04, TD - 0.08, 0.04), (sx * (TW / 2 - 0.04), 0, z), "RobotTableBeam", alu))
tf = join(bits, "RobotTableFrame"); bake(tf)
tt = box((TW, TD, 0.02), (0, 0, TH - 0.01), "RobotTableTop", esd)
export("bari_robot_table", [tf, tt])

# lay out for viewing
for o in rack: o.location = (0.0, 0.73, 0)
tf.location = tt.location = (-0.15, -0.05, 0)
print("rack tris", sum(len(o.data.polygons) for o in rack), "objects", len(rack))
