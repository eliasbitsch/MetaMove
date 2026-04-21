#!/usr/bin/env bash
set -e

source /opt/ros/jazzy/setup.bash

if [ -d "/ros2_ws/src" ] && [ ! -f "/ros2_ws/.rosdep_done" ]; then
  echo ">>> Running rosdep install (first run only)..."
  cd /ros2_ws
  rosdep update --rosdistro=jazzy || true
  rosdep install --from-paths src --ignore-src -r -y --rosdistro=jazzy || true
  touch /ros2_ws/.rosdep_done
fi

if [ -f "/ros2_ws/install/setup.bash" ]; then
  source /ros2_ws/install/setup.bash
fi

exec "$@"
