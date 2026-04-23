###############################################################################
# MetaMove ROS2 Jazzy — ABB GoFa (CRB15000) bridge + MoveIt + RViz
#
# Dep-only image. Sources live on host and are bind-mounted via
# docker-compose.yml. First start triggers `colcon build` inside the container
# (see entrypoint.sh). Rebuild image only when system/apt deps change.
#
#   docker build -f ros2/docker/Dockerfile -t metamove/ros2:jazzy .
###############################################################################
FROM ros:jazzy-ros-base

ARG DEBIAN_FRONTEND=noninteractive
ENV WS=/opt/metamove_ws

# ---------------------------------------------------------------------------
# System deps: build toolchain, MoveIt 2, RViz, ros2_control, xacro, debug tooling
# ---------------------------------------------------------------------------
RUN apt-get update && apt-get install -y --no-install-recommends \
        build-essential git cmake python3-pip python3-colcon-common-extensions \
        python3-rosdep python3-vcstool \
        libpoco-dev libprotobuf-dev protobuf-compiler \
        libcurl4-openssl-dev libssl-dev \
        ros-jazzy-moveit \
        ros-jazzy-rviz2 \
        ros-jazzy-ros2-control ros-jazzy-ros2-controllers \
        ros-jazzy-joint-state-publisher ros-jazzy-joint-state-publisher-gui \
        ros-jazzy-xacro ros-jazzy-urdf \
        ros-jazzy-rqt-graph ros-jazzy-rqt-plot ros-jazzy-rqt-topic \
        ros-jazzy-rosbag2 ros-jazzy-rosbag2-storage-mcap \
        ros-jazzy-foxglove-bridge \
        ros-jazzy-rosbridge-suite \
        ros-jazzy-ros-testing \
        iputils-ping iproute2 curl jq tmux nano \
    && rm -rf /var/lib/apt/lists/*

RUN pip3 install --no-cache-dir --break-system-packages requests websockets

# rosdep init already done in the base image; just refresh the cache
RUN . /opt/ros/jazzy/setup.sh && rosdep update --rosdistro=jazzy || true

# ---------------------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------------------
ENV ROS_DOMAIN_ID=42
ENV RMW_IMPLEMENTATION=rmw_fastrtps_cpp
ENV METAMOVE_RWS_IP=192.168.125.1
ENV METAMOVE_RWS_PORT=443
ENV METAMOVE_EGM_PORT=6511
# Set to 1 to auto-run `colcon build` on container start if install/ is missing
ENV METAMOVE_AUTOBUILD=1

COPY ros2/docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

WORKDIR ${WS}
ENTRYPOINT ["/entrypoint.sh"]
CMD ["bash"]
