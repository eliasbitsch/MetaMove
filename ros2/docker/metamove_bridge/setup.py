from glob import glob

from setuptools import find_packages, setup

package_name = 'metamove_bridge'

setup(
    name=package_name,
    version='0.1.0',
    packages=find_packages(exclude=['test']),
    data_files=[
        ('share/ament_index/resource_index/packages',
            ['resource/' + package_name]),
        ('share/' + package_name, ['package.xml']),
        ('share/' + package_name + '/launch', glob('launch/*.launch.py')),
        ('share/' + package_name + '/config', glob('config/*.yaml')),
    ],
    install_requires=['setuptools', 'requests', 'websockets', 'pyyaml'],
    zip_safe=True,
    maintainer='Elias Bitsch',
    maintainer_email='eliasbitsch@hotmail.com',
    description='MetaMove RWS+EGM ROS2 bridge.',
    license='Apache-2.0',
    entry_points={
        'console_scripts': [
            'bridge_node = metamove_bridge.bridge_node:main',
            'fake_cloud_publisher = metamove_bridge.fake_cloud_publisher:main',
            'pose_to_twist_node = metamove_bridge.pose_to_twist_node:main',
            'fake_joint_state_publisher = metamove_bridge.fake_joint_state_publisher:main',
            'moveit_ik_relay = metamove_bridge.moveit_ik_relay:main',
            'jtc_servo_relay = metamove_bridge.jtc_servo_relay:main',
            'distance_speed_scaler = metamove_bridge.distance_speed_scaler:main',
            'dpp_teach = metamove_bridge.dpp_teach:main',
            'dpp_playback = metamove_bridge.dpp_playback:main',
            'dpp_orchestrate = metamove_bridge.dpp_orchestrate:main',
            'dpp_gui = metamove_bridge.dpp_gui:main',
            'jtc_egm_stub = metamove_bridge.jtc_egm_stub:main',
        ],
    },
)
