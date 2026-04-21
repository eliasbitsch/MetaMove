FROM osrf/ros:jazzy-desktop

ARG DEBIAN_FRONTEND=noninteractive
ENV ROS_DISTRO=jazzy \
    ROS_WS=/ros2_ws

RUN apt-get update && apt-get install -y --no-install-recommends \
      python3-colcon-common-extensions \
      python3-colcon-mixin \
      python3-rosdep \
      python3-vcstool \
      build-essential \
      git \
      curl \
      nano \
      iproute2 \
      iputils-ping \
      net-tools \
      libprotobuf-dev \
      protobuf-compiler \
      libboost-all-dev \
      libpoco-dev \
      ros-jazzy-moveit \
      ros-jazzy-ros2-control \
      ros-jazzy-ros2-controllers \
      ros-jazzy-xacro \
      ros-jazzy-joint-state-publisher \
      ros-jazzy-joint-state-publisher-gui \
      ros-jazzy-rviz2 \
      ros-jazzy-rqt \
    && rm -rf /var/lib/apt/lists/*

RUN colcon mixin add default \
      https://raw.githubusercontent.com/colcon/colcon-mixin-repository/master/index.yaml \
    && colcon mixin update default \
    || echo "WARN: colcon mixin setup failed (network?) - continuing without mixins"

WORKDIR ${ROS_WS}

RUN echo 'source /opt/ros/jazzy/setup.bash' >> /root/.bashrc \
 && echo '[ -f /ros2_ws/install/setup.bash ] && source /ros2_ws/install/setup.bash' >> /root/.bashrc

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
CMD ["bash"]
