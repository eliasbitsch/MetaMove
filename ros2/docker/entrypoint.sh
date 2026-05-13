#!/usr/bin/env bash
# MetaMove ROS2 Jazzy entrypoint.
# - Sources ROS distro
# - Optionally auto-builds the mounted workspace on first start
# - Sources workspace install if available
# - Runs CMD
set -e

source /opt/ros/jazzy/setup.bash

# Unity ↔ ROS2 TCP bridge (built into image extras prefix during docker build)
if [ -f /opt/ros2_extras/install/setup.bash ]; then
    source /opt/ros2_extras/install/setup.bash
fi

cd "${WS:-/opt/metamove_ws}"

if [ ! -d install ] && [ -d src ] && [ "${METAMOVE_AUTOBUILD:-1}" = "1" ]; then
    echo "[entrypoint] install/ missing — running initial colcon build..."
    set +e
    rosdep install --from-paths src --ignore-src -r -y \
        --skip-keys="abb_libegm abb_librws abb_egm_rws_managers" || true
    colcon build --symlink-install \
        --cmake-args -DCMAKE_BUILD_TYPE=Release \
        --event-handlers console_direct+
    rc=$?
    set -e
    if [ $rc -ne 0 ]; then
        echo "[entrypoint] colcon build returned $rc — shell still usable for incremental rebuilds."
    fi
fi

if [ -f install/setup.bash ]; then
    source install/setup.bash
fi

if [ -t 0 ]; then
    cat <<EOF
-------------------------------------------------------------
 MetaMove ROS2 Jazzy
   Workspace:   ${WS}
   RWS target:  ${METAMOVE_RWS_IP}:${METAMOVE_RWS_PORT}
   EGM port:    ${METAMOVE_EGM_PORT}/udp
   Domain:      ${ROS_DOMAIN_ID}
 Quick debug:
   colcon build --symlink-install     # rebuild after source edits
   ros2 topic list
   ros2 topic echo /joint_states
   ros2 topic hz   /joint_states
   ros2 launch metamove_bridge bridge.launch.py
   ros2 service call /metamove/run_demo std_srvs/srv/Trigger
-------------------------------------------------------------
EOF
fi

exec "$@"
