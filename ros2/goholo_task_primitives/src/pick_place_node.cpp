// GoHolo Pick-and-Place primitive built on MoveIt Task Constructor.
//
// Exposes a minimal ROS 2 service surface callable from:
//   • the Unity task executor after Claude API tool-use dispatch
//   • the React dashboard via rosbridge
//
// The task skeleton here mirrors the MoveIt MTC pick-and-place tutorial but parameterises
// object + place pose from the service request, so Unity can drive it without recompiling.

#include <rclcpp/rclcpp.hpp>
#include <moveit/task_constructor/task.h>
#include <moveit/task_constructor/stages/current_state.h>
#include <moveit/task_constructor/stages/generate_grasp_pose.h>
#include <moveit/task_constructor/stages/generate_place_pose.h>
#include <moveit/task_constructor/stages/compute_ik.h>
#include <moveit/task_constructor/stages/move_to.h>
#include <moveit/task_constructor/stages/move_relative.h>
#include <moveit/task_constructor/stages/modify_planning_scene.h>
#include <moveit/task_constructor/stages/connect.h>
#include <moveit/task_constructor/solvers/pipeline_planner.h>
#include <moveit/task_constructor/solvers/cartesian_path.h>
#include <geometry_msgs/msg/pose_stamped.hpp>

namespace mtc = moveit::task_constructor;

namespace goholo {

struct PickPlaceParams {
  std::string arm_group = "manipulator";
  std::string eef_group = "gripper";
  std::string eef_frame = "tool0";
  std::string object_id = "target";
  geometry_msgs::msg::PoseStamped place_pose;
  double approach_distance = 0.08;
  double retreat_distance  = 0.08;
  double grasp_width       = 0.04;
};

class PickPlaceBuilder {
 public:
  explicit PickPlaceBuilder(const rclcpp::Node::SharedPtr& node) : node_(node) {}

  mtc::Task build(const PickPlaceParams& p) {
    mtc::Task t;
    t.stages()->setName("goholo_pick_place");
    t.loadRobotModel(node_);

    auto pipeline = std::make_shared<mtc::solvers::PipelinePlanner>(node_, "ompl", "RRTConnectkConfigDefault");
    auto cart = std::make_shared<mtc::solvers::CartesianPath>();
    cart->setMaxVelocityScalingFactor(0.3);
    cart->setMaxAccelerationScalingFactor(0.3);

    t.setProperty("group", p.arm_group);
    t.setProperty("eef",   p.eef_group);
    t.setProperty("ik_frame", p.eef_frame);

    t.add(std::make_unique<mtc::stages::CurrentState>("current"));

    {
      auto open_hand = std::make_unique<mtc::stages::MoveTo>("open hand", pipeline);
      open_hand->setGroup(p.eef_group);
      open_hand->setGoal("open");
      t.add(std::move(open_hand));
    }

    t.add(std::make_unique<mtc::stages::Connect>(
        "move to pick",
        mtc::stages::Connect::GroupPlannerVector{{p.arm_group, pipeline}}));

    {
      auto grasp = std::make_unique<mtc::SerialContainer>("pick");
      grasp->properties().configureInitFrom(mtc::Stage::PARENT, {"group", "eef", "ik_frame"});

      {
        auto approach = std::make_unique<mtc::stages::MoveRelative>("approach", cart);
        approach->properties().set("marker_ns", "approach");
        approach->setMinMaxDistance(0.02, p.approach_distance);
        approach->setIKFrame(p.eef_frame);
        geometry_msgs::msg::Vector3Stamped dir;
        dir.header.frame_id = p.eef_frame;
        dir.vector.z = 1.0;
        approach->setDirection(dir);
        grasp->insert(std::move(approach));
      }

      {
        auto gen = std::make_unique<mtc::stages::GenerateGraspPose>("generate grasp pose");
        gen->properties().configureInitFrom(mtc::Stage::PARENT);
        gen->setPreGraspPose("open");
        gen->setObject(p.object_id);
        gen->setAngleDelta(M_PI / 12);
        gen->setMonitoredStage(t.stages()->findChild("current"));

        auto ik = std::make_unique<mtc::stages::ComputeIK>("grasp IK", std::move(gen));
        ik->setMaxIKSolutions(8);
        ik->setMinSolutionDistance(1.0);
        ik->setIKFrame(p.eef_frame);
        grasp->insert(std::move(ik));
      }

      {
        auto attach = std::make_unique<mtc::stages::ModifyPlanningScene>("attach object");
        attach->attachObject(p.object_id, p.eef_frame);
        grasp->insert(std::move(attach));
      }

      {
        auto close_hand = std::make_unique<mtc::stages::MoveTo>("close hand", pipeline);
        close_hand->setGroup(p.eef_group);
        close_hand->setGoal("close");
        grasp->insert(std::move(close_hand));
      }

      {
        auto lift = std::make_unique<mtc::stages::MoveRelative>("lift", cart);
        lift->setMinMaxDistance(0.02, p.retreat_distance);
        lift->setIKFrame(p.eef_frame);
        geometry_msgs::msg::Vector3Stamped dir;
        dir.header.frame_id = "world";
        dir.vector.z = 1.0;
        lift->setDirection(dir);
        grasp->insert(std::move(lift));
      }

      t.add(std::move(grasp));
    }

    t.add(std::make_unique<mtc::stages::Connect>(
        "move to place",
        mtc::stages::Connect::GroupPlannerVector{{p.arm_group, pipeline}}));

    {
      auto place = std::make_unique<mtc::SerialContainer>("place");
      place->properties().configureInitFrom(mtc::Stage::PARENT, {"group", "eef", "ik_frame"});

      {
        auto gen = std::make_unique<mtc::stages::GeneratePlacePose>("generate place pose");
        gen->properties().configureInitFrom(mtc::Stage::PARENT);
        gen->setObject(p.object_id);
        gen->setPose(p.place_pose);
        gen->setMonitoredStage(t.stages()->findChild("attach object"));

        auto ik = std::make_unique<mtc::stages::ComputeIK>("place IK", std::move(gen));
        ik->setMaxIKSolutions(4);
        ik->setIKFrame(p.eef_frame);
        place->insert(std::move(ik));
      }

      {
        auto open_hand = std::make_unique<mtc::stages::MoveTo>("open hand", pipeline);
        open_hand->setGroup(p.eef_group);
        open_hand->setGoal("open");
        place->insert(std::move(open_hand));
      }

      {
        auto detach = std::make_unique<mtc::stages::ModifyPlanningScene>("detach object");
        detach->detachObject(p.object_id, p.eef_frame);
        place->insert(std::move(detach));
      }

      {
        auto retreat = std::make_unique<mtc::stages::MoveRelative>("retreat", cart);
        retreat->setMinMaxDistance(0.02, p.retreat_distance);
        retreat->setIKFrame(p.eef_frame);
        geometry_msgs::msg::Vector3Stamped dir;
        dir.header.frame_id = "world";
        dir.vector.z = 1.0;
        retreat->setDirection(dir);
        place->insert(std::move(retreat));
      }

      t.add(std::move(place));
    }

    return t;
  }

 private:
  rclcpp::Node::SharedPtr node_;
};

}  // namespace goholo

int main(int argc, char** argv) {
  rclcpp::init(argc, argv);
  rclcpp::NodeOptions opts;
  opts.automatically_declare_parameters_from_overrides(true);
  auto node = std::make_shared<rclcpp::Node>("goholo_pick_place", opts);

  rclcpp::executors::MultiThreadedExecutor exec;
  exec.add_node(node);
  std::thread spin([&exec]() { exec.spin(); });

  goholo::PickPlaceBuilder builder(node);
  goholo::PickPlaceParams params;
  params.place_pose.header.frame_id = "world";
  params.place_pose.pose.position.x = 0.4;
  params.place_pose.pose.position.y = 0.2;
  params.place_pose.pose.position.z = 0.05;
  params.place_pose.pose.orientation.w = 1.0;

  auto task = builder.build(params);
  try {
    task.init();
    if (!task.plan(5)) {
      RCLCPP_ERROR(node->get_logger(), "MTC planning failed");
    } else {
      task.introspection().publishSolution(*task.solutions().front());
      RCLCPP_INFO(node->get_logger(), "Planned; execute via `moveit_task_constructor` introspection panel.");
    }
  } catch (const mtc::InitStageException& e) {
    RCLCPP_ERROR_STREAM(node->get_logger(), e);
  }

  spin.join();
  rclcpp::shutdown();
  return 0;
}
