cmake_minimum_required(VERSION 3.16)
project(goholo_task_primitives)

if(NOT CMAKE_CXX_STANDARD)
  set(CMAKE_CXX_STANDARD 17)
  set(CMAKE_CXX_STANDARD_REQUIRED ON)
endif()

find_package(ament_cmake REQUIRED)
find_package(rclcpp REQUIRED)
find_package(moveit_core REQUIRED)
find_package(moveit_ros_planning_interface REQUIRED)
find_package(moveit_task_constructor_core REQUIRED)
find_package(moveit_task_constructor_msgs REQUIRED)
find_package(geometry_msgs REQUIRED)
find_package(shape_msgs REQUIRED)
find_package(std_msgs REQUIRED)
find_package(tf2_eigen REQUIRED)

add_executable(pick_place_node src/pick_place_node.cpp)
ament_target_dependencies(pick_place_node
  rclcpp
  moveit_core
  moveit_ros_planning_interface
  moveit_task_constructor_core
  moveit_task_constructor_msgs
  geometry_msgs
  shape_msgs
  std_msgs
  tf2_eigen
)

install(TARGETS pick_place_node
  DESTINATION lib/${PROJECT_NAME})

install(DIRECTORY launch config
  DESTINATION share/${PROJECT_NAME}
  OPTIONAL)

ament_package()
