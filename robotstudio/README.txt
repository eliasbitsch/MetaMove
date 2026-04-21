/*
 *      ---------------------------------   
 *                   GoHolo    
 *      ---------------------------------
 *      
 *      Copyright (C)   2021 Jakob Hörbst
 *      author:         Jakob Hörbst
 *      email:          jakob@hoerbst.net
 *      year:           2021
 * 
*/

WiFi-ROUTER:
IP Reservation: 
HoloLens 	192.168.1.100
PC		192.168.1.10
Static IP on Management Port: 
PC		192.168.125.50
(see GoHoloOverview.jpg)

CONTROLLER:
Required Options: 
- PC Interface 
- Externally Guided Motion 
- Multitasking

Configuration: 
Communication - UDP Unicast Device 
Name 		Type	Remote Address	Remote Port Number	Local Port Number
UCdevice	UDPUC	127.0.0.1	6510			0
UCstream	UDPUC	127.0.0.1	6510			0

Controller - Task 
Task	Task in Foreground 	Type	Main Entry	Check Unsolved References	Trust Level	Motion Tast	Use Mechanical Unit Group	Hidden		RMQ Type	RMQ Mode
T_DATA				Normal	main		1				N/A		No						N/A		None		Interrupt
T_ROB1				Normal	main		1				N/A		Yes						N/A		None		Interrupt

Motion - External Motion Interface Data 
Name	Level 		Do Not Restart after Motors Off		Return to Program Position when Stopped		Default Ramp Time	Default Proportional Position Gain 	Default Low Pass Filter Bandwith
GoHolo	Filtering	Yes					Yes						0,5			5					20

