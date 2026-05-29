# Slim GoFa dashboard image.
# Joins the existing metamove/ros2:jazzy ROS2 graph via rclpy — no colcon build,
# no MoveIt/RViz/ABB-driver compile. ABB custom msgs (abb_egm_msgs,
# abb_robot_msgs) are provided at runtime by the shared metamove build/install
# volumes, so the EGM-State tile keeps working without duplicating the stack.
#
# Build context is the MRE2-GOFA_Dashboard submodule checkout (Philip's app
# source); this Dockerfile lives in MetaMove (deploy/dashboard/) because it is
# integration glue, not part of the dashboard app itself.
FROM ros:jazzy-ros-base
ARG DEBIAN_FRONTEND=noninteractive

# control_msgs (FollowJointTrajectory action) is the only message package the
# backend needs that isn't already in ros-base. pip for the web layer only.
RUN apt-get update && apt-get install -y --no-install-recommends \
        python3-pip \
        ros-jazzy-control-msgs \
    && rm -rf /var/lib/apt/lists/*

RUN pip3 install --no-cache-dir --break-system-packages \
        fastapi==0.115.6 "uvicorn[standard]==0.34.0" "psycopg[binary]==3.2.3"

COPY backend /app/backend
COPY frontend /app/frontend
WORKDIR /app

ENV ROS_DOMAIN_ID=42
ENV RMW_IMPLEMENTATION=rmw_fastrtps_cpp
ENV PYTHONUNBUFFERED=1

EXPOSE 8080

# Source the distro, then the shared metamove workspace (EGM/SystemState msgs)
# if its install volume is mounted, then start the web server.
CMD ["bash","-lc","source /opt/ros/jazzy/setup.bash; if [ -f /opt/metamove_ws/install/setup.bash ]; then source /opt/metamove_ws/install/setup.bash; fi; exec uvicorn backend.app:app --host \"${SMAN_HTTP_HOST:-0.0.0.0}\" --port \"${SMAN_HTTP_PORT:-8080}\""]
