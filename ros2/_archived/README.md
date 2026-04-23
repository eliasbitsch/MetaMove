# Archived ROS2 Packages

Packages moved here are no longer part of the active build. They are preserved
for reference / potential future reactivation, but **not** compiled by the
`metamove/ros2:jazzy` image (they are outside the `src/*` bind mounts in
[docker-compose.yml](../docker/docker-compose.yml)).

## `goholo_task_primitives/`

**Archived:** 2026-04-23.

MoveIt Task Constructor-based pick-and-place node from the pre-Meta-headset era.
Replaced by:

- **Unity-side perception** via Meta Quest 3 Scene API + Depth + hand tracking
- **RAPID-side motion** — the 6 committed demo scenarios (chess, stone sort,
  framing, mug, pins, bigstone) run as RAPID procedures, triggered via the
  `metamove_bridge` RWS services

Reactivation would require:

1. `moveit_task_constructor_core` to become available for ROS 2 Jazzy (currently
   not packaged for Jazzy — was Humble-only at time of archive), or vendoring
   it from source
2. Moving this folder back to `ros2/`
3. Re-adding the mount in [docker-compose.yml](../docker/docker-compose.yml)
