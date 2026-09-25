"""Compare Unity joint positions (AxisConventionCheck output) against MoveIt FK.

usage: compare.py fk_ref.json unity_fk.txt [tol_m]
Prints per-config max error over link_1..link_6 and AXIS_OK / AXIS_MISMATCH.
Positions alone cannot see joint 6 (it turns the flange in place), so the flange's
rotation relative to the zero pose is compared too: dR = R(q) * R(zero)^-1 must agree.
Any constant offset between the Unity bone and the URDF frame cancels out of dR.
"""
import json, math, sys


def qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return (aw*bx + ax*bw + ay*bz - az*by, aw*by - ax*bz + ay*bw + az*bx,
            aw*bz + ax*by - ay*bx + az*bw, aw*bw - ax*bx - ay*by - az*bz)


def qinv(a):
    return (-a[0], -a[1], -a[2], a[3])


def qangle(a, b):
    d = abs(sum(x*y for x, y in zip(a, b)))
    return 2 * math.degrees(math.acos(min(1.0, d)))

ref = json.load(open(sys.argv[1]))
tol = float(sys.argv[3]) if len(sys.argv) > 3 else 0.005
worst = 0.0
worst_deg = 0.0
rows = []
for line in open(sys.argv[2]):
    if line.startswith("#") or line.startswith("AXIS_"):
        print(line.rstrip()) if line.startswith("#") else None
        continue
    f = line.split()
    rows.append((f[0], list(map(float, f[1:]))))
u_zero = dict(rows)["zero"][18:22]
r_zero = ref["zero"]["links"]["link_6"][3:7]
for name, u in rows:
    errs = []
    for i in range(6):
        r = ref[name]["links"][f"link_{i+1}"][:3]
        errs.append(math.dist(r, u[3 * i:3 * i + 3]))
    worst = max(worst, max(errs))
    d_ros = qmul(ref[name]["links"]["link_6"][3:7], qinv(r_zero))
    d_uni = qmul(u[18:22], qinv(u_zero))
    rot = qangle(d_ros, d_uni)
    worst_deg = max(worst_deg, rot)
    print(f"{name:5s} max_err={max(errs)*1000:7.1f} mm  flange_rot_err={rot:5.2f} deg  per-link " + " ".join(f"{e*1000:.1f}" for e in errs))
ok = worst <= tol and worst_deg <= 1.0
print(("AXIS_OK" if ok else "AXIS_MISMATCH") + f" worst={worst*1000:.1f}mm rot={worst_deg:.2f}deg tol={tol*1000:.1f}mm/1deg")
sys.exit(0 if ok else 1)
