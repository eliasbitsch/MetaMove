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
        ('share/' + package_name + '/launch', ['launch/bridge.launch.py']),
    ],
    install_requires=['setuptools', 'requests', 'websockets'],
    zip_safe=True,
    maintainer='Elias Bitsch',
    maintainer_email='eliasbitsch@hotmail.com',
    description='MetaMove RWS+EGM ROS2 bridge.',
    license='Apache-2.0',
    entry_points={
        'console_scripts': [
            'bridge_node = metamove_bridge.bridge_node:main',
        ],
    },
)
