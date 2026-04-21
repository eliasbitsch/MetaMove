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

using System;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Abb.Egm;
using System.Runtime.InteropServices;


namespace MetaMove
{
    public partial class MainWindow : Window
    {
        private bool newJointTarget = false;

        public HoloLens2 holo = new HoloLens2();
        public RobotFeedback VC_data = new RobotFeedback();
        public RobotFeedback RC_data = new RobotFeedback();
        public CommandClass command = new CommandClass();


        public MainWindow()
        {

            ////////////////////////          init          ////////////////////////
            DataContext = this;
            InitializeComponent();

            ////////////////////////    activate console    ////////////////////////
            ConsoleAllocator.ShowConsoleWindow();

            ////////////////////////    UDP connections     ////////////////////////
            UDPserver UDP_VC_Server = new UDPserver(ref VC_data, command);
            UDPserver UDP_RC_Server = new UDPserver(ref RC_data, command);

            RC_data.UDP.IP_default  = "192.168.125.5";
            RC_data.UDP.PORT        = 6510;

            VC_data.UDP.IP_default  = "127.0.0.1";
            VC_data.UDP.PORT        = 6510;

            UDP_VC_Server.StartThread();
            UDP_RC_Server.StartThread();

            ////////////////////////         PCSDK          ////////////////////////
            PCSDK PCSDK_VC = new PCSDK(ref VC_data, command, this);
            PCSDK PCSDK_RC = new PCSDK(ref RC_data, command, this);

            VC_data.name = "GoFa";//"14050-Simulation";
            VC_data.type = "virtual";
            VC_name.Text = VC_data.name;

            RC_data.name = "15000-500078";//"14050-500257";
            RC_data.type = "real";
            RC_name.Text = RC_data.name;

            PCSDK_VC.startThread();
            PCSDK_RC.startThread();

            ////////////////////////          TCP           ////////////////////////
            TCPserver TCP = new TCPserver(ref holo, VC_data, RC_data, command, this);

            holo.TCP.IP_default     = "127.0.0.1";
            holo.TCP.PORT           = 5515;

            TCP.startThread();

            ////////////////////////          GUI           ////////////////////////
            Thread thr_uptGUI = new Thread(new ThreadStart(Thread_updateGUI));
            thr_uptGUI.Start();
        }


        ////////////////////////////////////////                ////////////////////////////////////////
        ////////////////////////////////////////  THREAD GUI    ////////////////////////////////////////
        ////////////////////////////////////////                ////////////////////////////////////////
        public void Thread_updateGUI() {

            InitializeGUI();
            while (true)
            {
                UpdateGUI();
                Thread.Sleep(50);
            }

        }


        ////////////////////////////////////////    init GUI    ////////////////////////////////////////
        private void InitializeGUI()
        {
            // angle limit for axes
            int[,] axisLimit = { { -180, 180 }, { -180, 180 }, { -225, 85 }, { -180, 180 }, { -180, 180 }, { -180, 180 } };
            Dispatcher.BeginInvoke((Action)(() =>
            {
                visu_sliderJoint01.Minimum = axisLimit[0, 0];
                visu_sliderJoint01.Maximum = axisLimit[0, 1];
                visu_sliderJoint02.Minimum = axisLimit[1, 0];
                visu_sliderJoint02.Maximum = axisLimit[1, 1];
                visu_sliderJoint03.Minimum = axisLimit[2, 0];
                visu_sliderJoint03.Maximum = axisLimit[2, 1];
                visu_sliderJoint04.Minimum = axisLimit[3, 0];
                visu_sliderJoint04.Maximum = axisLimit[3, 1];
                visu_sliderJoint05.Minimum = axisLimit[4, 0];
                visu_sliderJoint05.Maximum = axisLimit[4, 1];
                visu_sliderJoint06.Minimum = axisLimit[5, 0];
                visu_sliderJoint06.Maximum = axisLimit[5, 1];

                visu_RCsliderJoint01.Minimum = axisLimit[0, 0];
                visu_RCsliderJoint01.Maximum = axisLimit[0, 1];
                visu_RCsliderJoint02.Minimum = axisLimit[1, 0];
                visu_RCsliderJoint02.Maximum = axisLimit[1, 1];
                visu_RCsliderJoint03.Minimum = axisLimit[2, 0];
                visu_RCsliderJoint03.Maximum = axisLimit[2, 1];
                visu_RCsliderJoint04.Minimum = axisLimit[3, 0];
                visu_RCsliderJoint04.Maximum = axisLimit[3, 1];
                visu_RCsliderJoint05.Minimum = axisLimit[4, 0];
                visu_RCsliderJoint05.Maximum = axisLimit[4, 1];
                visu_RCsliderJoint06.Minimum = axisLimit[5, 0];
                visu_RCsliderJoint06.Maximum = axisLimit[5, 1];
            }));

        }

        ////////////////////////////////////////   update GUI   ////////////////////////////////////////
        private void UpdateGUI()
        {

            Dispatcher.BeginInvoke((Action)(() =>
            {
                switch (command.MODE)
                {
                    case 0:
                        // do nothing
                        mode_doNothing.IsChecked = true; 
                        break;
                    case 1:
                        // random Path 
                        mode_rapidPath.IsChecked = true;
                        break;
                    case 2:
                        // egm
                        mode_egmMotion.IsChecked = true;
                        break;
                    case 3:
                        // jointcontrol 
                        mode_jointController.IsChecked = true;
                        break;
                    case 4:
                        // holopath 
                        mode_holoPath.IsChecked = true; 
                        break;
                    case 5:
                        // wizard simulation 
                        mode_wizardSimulation.IsChecked = true;
                        break;
                }

                if (!holo.connected)
                {
                    TCPcustom.IsEnabled = true;
                    TCPButtons.IsEnabled = true;
                    TCPButtonStart.IsEnabled = true;
                    TCPButtonStop.IsEnabled = false;
                }
                else
                {
                    TCPcustom.IsEnabled = false;
                    TCPButtons.IsEnabled = false;
                    TCPButtonStart.IsEnabled = false;
                    TCPButtonStop.IsEnabled = true;

                }


                command.HoloDataSourceSimulation = (bool)holoLensDataSourceSimulation.IsChecked;
                if (command.OverrideDataSource)
                    command.HoloDataSourceSimulation = true; 

                RC_data.UDP.Enable = (bool)UDP_RC_enable.IsChecked;
                VC_data.UDP.Enable = (bool)UDP_VC_enable.IsChecked;

                VC_data.PCSDK.Enable = (bool)check_VC.IsChecked;
                RC_data.PCSDK.Enable = (bool)check_RC.IsChecked;

                if ((bool)RC_radio_UDPdefault.IsChecked)
                    RC_data.UDP.IP = RC_data.UDP.IP_default;
                else
                    RC_data.UDP.IP = RC_UDPcustomIP.Text;

                if ((bool)VC_radio_UDPdefault.IsChecked)
                    VC_data.UDP.IP = VC_data.UDP.IP_default;
                else
                    VC_data.UDP.IP = VC_UDPcustomIP.Text;

                if ((bool)radio_TCPdefault.IsChecked)
                    holo.TCP.IP = holo.TCP.IP_default;
                else
                    holo.TCP.IP = TCPcustom.Text;


                // automatic mode
                if (VC_data.RAPID.AutomaticMode)
                    VC_automaticON.Visibility = Visibility.Visible;
                else
                    VC_automaticON.Visibility = Visibility.Hidden;

                if (RC_data.RAPID.AutomaticMode)
                    RC_automaticON.Visibility = Visibility.Visible;
                else
                    RC_automaticON.Visibility = Visibility.Hidden;

                // RAPID running
                if (VC_data.RAPID.Running)
                    VC_rapidON.Visibility = Visibility.Visible;
                else
                    VC_rapidON.Visibility = Visibility.Hidden;

                if (RC_data.RAPID.Running)
                    RC_rapidON.Visibility = Visibility.Visible;
                else
                    RC_rapidON.Visibility = Visibility.Hidden;

                // PCSDK
                if (RC_data.PCSDK.Online)
                    RC_pcsdkON.Visibility = Visibility.Visible;
                else
                    RC_pcsdkON.Visibility = Visibility.Hidden;

                if (VC_data.PCSDK.Online)
                    VC_pcsdkON.Visibility = Visibility.Visible;
                else
                    VC_pcsdkON.Visibility = Visibility.Hidden;

                // EGM
                if (VC_data.UDP.Online)
                    VC_egmON.Visibility = Visibility.Visible;
                else
                    VC_egmON.Visibility = Visibility.Hidden;

                if (RC_data.UDP.Online)
                    RC_egmON.Visibility = Visibility.Visible;
                else
                    RC_egmON.Visibility = Visibility.Hidden;

                // cycle forever
                if (VC_data.RAPID.ExecutionCycleForever)
                    VC_foreverON.Visibility = Visibility.Visible;
                else
                    VC_foreverON.Visibility = Visibility.Hidden;

                if (RC_data.RAPID.ExecutionCycleForever)
                    RC_foreverON.Visibility = Visibility.Visible;
                else
                    RC_foreverON.Visibility = Visibility.Hidden;

                // HoloLens2 connection
                if (holo.connected)
                    holoON.Visibility = Visibility.Visible;
                else
                    holoON.Visibility = Visibility.Hidden;

                if (VC_data.PCSDK.ERROR || RC_data.PCSDK.ERROR)
                {
                    RC_data.PCSDK.Stop = true;
                    VC_data.PCSDK.Stop = true;
                    check_RC.IsEnabled = true;
                    check_VC.IsEnabled = true;
                    RC_name.IsEnabled = true;
                    VC_name.IsEnabled = true;

                }


                // data from real controller
                visuRC_angle01.Text = RC_data.positionEGM[0].ToString();
                visuRC_angle02.Text = RC_data.positionEGM[1].ToString();
                visuRC_angle03.Text = RC_data.positionEGM[2].ToString();
                visuRC_angle04.Text = RC_data.positionEGM[3].ToString();
                visuRC_angle05.Text = RC_data.positionEGM[4].ToString();
                visuRC_angle06.Text = RC_data.positionEGM[5].ToString();

                visuRC_tor01.Text = RC_data.torque[0].ToString();
                visuRC_tor02.Text = RC_data.torque[1].ToString();
                visuRC_tor03.Text = RC_data.torque[2].ToString();
                visuRC_tor04.Text = RC_data.torque[3].ToString();
                visuRC_tor05.Text = RC_data.torque[4].ToString();
                visuRC_tor06.Text = RC_data.torque[5].ToString();
                /*
                visuRC_extTor01.Text = RC_data.extTorque[0].ToString();
                visuRC_extTor02.Text = RC_data.extTorque[1].ToString();
                visuRC_extTor03.Text = RC_data.extTorque[2].ToString();
                visuRC_extTor04.Text = RC_data.extTorque[3].ToString();
                visuRC_extTor05.Text = RC_data.extTorque[4].ToString();
                visuRC_extTor06.Text = RC_data.extTorque[5].ToString();
                */
                /*
                visuRC_PCSDKangle01.Text = RC_data.position[0].ToString();
                visuRC_PCSDKangle02.Text = RC_data.position[1].ToString();
                visuRC_PCSDKangle03.Text = RC_data.position[2].ToString();
                visuRC_PCSDKangle04.Text = RC_data.position[3].ToString();
                visuRC_PCSDKangle05.Text = RC_data.position[4].ToString();
                visuRC_PCSDKangle06.Text = RC_data.position[5].ToString();
                */
                visuRC_poseX.Text = RC_data.cartesianPosition[0].ToString();
                visuRC_poseY.Text = RC_data.cartesianPosition[1].ToString();
                visuRC_poseZ.Text = RC_data.cartesianPosition[2].ToString();
                visuRC_poseRX.Text = RC_data.eulerOrientation[0].ToString();
                visuRC_poseRY.Text = RC_data.eulerOrientation[1].ToString();
                visuRC_poseRZ.Text = RC_data.eulerOrientation[2].ToString();

                // data from virtual controller
                visuVC_angle01.Text = VC_data.positionEGM[0].ToString();
                visuVC_angle02.Text = VC_data.positionEGM[1].ToString();
                visuVC_angle03.Text = VC_data.positionEGM[2].ToString();
                visuVC_angle04.Text = VC_data.positionEGM[3].ToString();
                visuVC_angle05.Text = VC_data.positionEGM[4].ToString();
                visuVC_angle06.Text = VC_data.positionEGM[5].ToString();

                visuVC_tor01.Text = VC_data.torque[0].ToString();
                visuVC_tor02.Text = VC_data.torque[1].ToString();
                visuVC_tor03.Text = VC_data.torque[2].ToString();
                visuVC_tor04.Text = VC_data.torque[3].ToString();
                visuVC_tor05.Text = VC_data.torque[4].ToString();
                visuVC_tor06.Text = VC_data.torque[5].ToString();
                /*
                visuVC_extTor01.Text = VC_data.extTorque[0].ToString();
                visuVC_extTor02.Text = VC_data.extTorque[1].ToString();
                visuVC_extTor03.Text = VC_data.extTorque[2].ToString();
                visuVC_extTor04.Text = VC_data.extTorque[3].ToString();
                visuVC_extTor05.Text = VC_data.extTorque[4].ToString();
                visuVC_extTor06.Text = VC_data.extTorque[5].ToString();
                */
                /*
                visuVC_PCSDKangle01.Text = VC_data.position[0].ToString();
                visuVC_PCSDKangle02.Text = VC_data.position[1].ToString();
                visuVC_PCSDKangle03.Text = VC_data.position[2].ToString();
                visuVC_PCSDKangle04.Text = VC_data.position[3].ToString();
                visuVC_PCSDKangle05.Text = VC_data.position[4].ToString();
                visuVC_PCSDKangle06.Text = VC_data.position[5].ToString();
                */
                visuVC_poseX.Text = VC_data.cartesianPosition[0].ToString();
                visuVC_poseY.Text = VC_data.cartesianPosition[1].ToString();
                visuVC_poseZ.Text = VC_data.cartesianPosition[2].ToString();
                visuVC_poseRX.Text = VC_data.eulerOrientation[0].ToString();
                visuVC_poseRY.Text = VC_data.eulerOrientation[1].ToString();
                visuVC_poseRZ.Text = VC_data.eulerOrientation[2].ToString();

                if (command.MODE != 3)
                {
                    visu_sliderJoint01.Value = VC_data.positionEGM[0];
                    visu_sliderJoint02.Value = VC_data.positionEGM[1];
                    visu_sliderJoint03.Value = VC_data.positionEGM[2];
                    visu_sliderJoint04.Value = VC_data.positionEGM[3];
                    visu_sliderJoint05.Value = VC_data.positionEGM[4];
                    visu_sliderJoint06.Value = VC_data.positionEGM[5];

                    visu_RCsliderJoint01.Value = RC_data.positionEGM[0];
                    visu_RCsliderJoint02.Value = RC_data.positionEGM[1];
                    visu_RCsliderJoint03.Value = RC_data.positionEGM[2];
                    visu_RCsliderJoint04.Value = RC_data.positionEGM[3];
                    visu_RCsliderJoint05.Value = RC_data.positionEGM[4];
                    visu_RCsliderJoint06.Value = RC_data.positionEGM[5];

                    visu_sliderJoint01.IsEnabled = false;
                    visu_sliderJoint02.IsEnabled = false;
                    visu_sliderJoint03.IsEnabled = false;
                    visu_sliderJoint04.IsEnabled = false;
                    visu_sliderJoint05.IsEnabled = false;
                    visu_sliderJoint06.IsEnabled = false;

                }
                else {
                    visu_sliderJoint01.IsEnabled = true;
                    visu_sliderJoint02.IsEnabled = true;
                    visu_sliderJoint03.IsEnabled = true;
                    visu_sliderJoint04.IsEnabled = true;
                    visu_sliderJoint05.IsEnabled = true;
                    visu_sliderJoint06.IsEnabled = true;
                }


            }));

        }

        ////////////////////////////////////////                ////////////////////////////////////////
        ////////////////////////////////////////   Properties   ////////////////////////////////////////
        ////////////////////////////////////////                ////////////////////////////////////////
        /*
        public int Versuch = 404;
        private char _ThrRunning = new char();
        public char ThrRunning
        {
            get { return _ThrRunning; }
            set
            {
                if (_ThrRunning != value)
                {
                    _ThrRunning = value;
                    OnPropertyChanged();

                }

            }
        }

        private double _FeedbackAngle01 = new double();
        public double FeedbackAngle01 {
            get { return _FeedbackAngle01; }
            set
            {
                if (_FeedbackAngle01 != value)
                {
                    _FeedbackAngle01 = value;
                    OnPropertyChanged();

                }
            }
        }

        private double _FeedbackAngle02 = new double();
        public double FeedbackAngle02
        {
            get { return _FeedbackAngle02; }
            set
            {
                if (_FeedbackAngle02 != value)
                {
                    _FeedbackAngle02 = value;
                    OnPropertyChanged();

                }
            }
        }

        private double _FeedbackAngle03 = new double();
        public double FeedbackAngle03
        {
            get { return _FeedbackAngle03; }
            set
            {
                if (_FeedbackAngle03 != value)
                {
                    _FeedbackAngle03 = value;
                    OnPropertyChanged();

                }
            }
        }

        private double _FeedbackAngle04 = new double();
        public double FeedbackAngle04
        {
            get { return _FeedbackAngle04; }
            set
            {
                if (_FeedbackAngle04 != value)
                {
                    _FeedbackAngle04 = value;
                    OnPropertyChanged();

                }
            }
        }

        private double _FeedbackAngle05 = new double();
        public double FeedbackAngle05
        {
            get { return _FeedbackAngle05; }
            set
            {
                if (_FeedbackAngle05 != value)
                {
                    _FeedbackAngle05 = value;
                    OnPropertyChanged();

                }
            }
        }

        private double _FeedbackAngle06 = new double();
        public double FeedbackAngle06
        {
            get { return _FeedbackAngle06; }
            set
            {
                if (_FeedbackAngle06 != value)
                {
                    _FeedbackAngle06 = value;
                    OnPropertyChanged();

                }
            }
        }
        */
        /*
        private int _boundNumber = 100;
        public int BoundNumber
        {
            get { return _boundNumber; }
            set
            {
                if (_boundNumber != value)
                {
                    _boundNumber = value;
                    OnPropertyChanged();

                }

            }
        }


        public string TCPserverIP
        {
            get { return _TCPserverIP; }
            set
            {
                if (_TCPserverIP != value)
                {
                    _TCPserverIP = value;
                    OnPropertyChanged();

                }

            }
        }
     

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
           */


        ////////////////////////////////////////                ////////////////////////////////////////
        ////////////////////////////////////////     Buttons    ////////////////////////////////////////
        ////////////////////////////////////////                ////////////////////////////////////////
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            /*
            if (HelloButton.IsChecked == true)
                MessageBox.Show("hello");
            else
                MessageBox.Show("tschüss");
            */
        }


        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            
            if (VC_data.PCSDK.Online && RC_data.PCSDK.Online)
            {
                command.WIZARD.Simulate = true;
                command.MODE = command.CMD_WIZARD;
            }
            else
            {
                command.WIZARD.Simulate = false;
                MessageBox.Show("CONNECT BOTH CONTROLLERS FIRST (VC & RC)");
            }
        }

        private void rapidStart_Click(object sender, RoutedEventArgs e)
        {
            VC_data.RAPID.Start = true; 
        }

        private void rapidStop_Click(object sender, RoutedEventArgs e)
        {
            VC_data.RAPID.Stop = true; 
        }

        private void visu_sliderJoint06_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VC_data.CTRL_jointTarget[5] = (float)visu_sliderJoint06.Value;
            RC_data.CTRL_jointTarget[5] = (float)visu_sliderJoint06.Value;
            newJointTarget = true;

        }
        private void visu_sliderJoint05_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VC_data.CTRL_jointTarget[4] = (float)visu_sliderJoint05.Value;
            RC_data.CTRL_jointTarget[4] = (float)visu_sliderJoint05.Value;
            newJointTarget = true;
        }

        private void visu_sliderJoint04_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VC_data.CTRL_jointTarget[3] = (float)visu_sliderJoint04.Value;
            RC_data.CTRL_jointTarget[3] = (float)visu_sliderJoint04.Value;
            newJointTarget = true;
        }

        private void visu_sliderJoint03_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VC_data.CTRL_jointTarget[2] = (float)visu_sliderJoint03.Value;
            RC_data.CTRL_jointTarget[2] = (float)visu_sliderJoint03.Value;
            newJointTarget = true;
        }

        private void visu_sliderJoint02_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VC_data.CTRL_jointTarget[1] = (float)visu_sliderJoint02.Value;
            RC_data.CTRL_jointTarget[1] = (float)visu_sliderJoint02.Value;
            newJointTarget = true;
        }

        private void visu_sliderJoint01_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VC_data.CTRL_jointTarget[0] = (float)visu_sliderJoint01.Value;
            RC_data.CTRL_jointTarget[0] = (float)visu_sliderJoint01.Value;
            newJointTarget = true;
        }

        private void btn_PCSDK_start_Click(object sender, RoutedEventArgs e)
        {
            GUI_PCSDK_start.IsEnabled = false;
            GUI_PCSDK_stop.IsEnabled = true;

            RC_data.PCSDK.Start = true;
            VC_data.PCSDK.Start = true; 

            check_RC.IsEnabled = false;
            check_VC.IsEnabled = false;
            RC_name.IsEnabled = false;
            VC_name.IsEnabled = false; 
        }

        private void btn_PCSDK_stop_Click(object sender, RoutedEventArgs e)
        {
            GUI_PCSDK_start.IsEnabled = true;
            GUI_PCSDK_stop.IsEnabled = false;

            RC_data.PCSDK.Stop = true;
            VC_data.PCSDK.Stop = true;
            check_RC.IsEnabled = true;
            check_VC.IsEnabled = true;
            RC_name.IsEnabled = true;
            VC_name.IsEnabled = true;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            holo.TCP.Start = true;
            TCPcustom.IsEnabled = false;
            TCPButtons.IsEnabled = false;
            TCPButtonStart.IsEnabled = false;
            TCPButtonStop.IsEnabled = true; 
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            holo.TCP.Stop = true;
            TCPcustom.IsEnabled = true;
            TCPButtons.IsEnabled = true;
            TCPButtonStart.IsEnabled = true;
            TCPButtonStop.IsEnabled = false;
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            RC_data.UDP.Start = true;
            VC_data.UDP.Start = true;
            GUI_UDP_stop.IsEnabled = true;
            GUI_UDP_start.IsEnabled = false; 
            UDP_RC_enable.IsEnabled = false;
            UDP_VC_enable.IsEnabled = false;
            RC_radio_UDPdefault.IsEnabled = false;
            RC_radio_UDPcustom.IsEnabled = false;
            RC_UDPcustomIP.IsEnabled = false;

            VC_radio_UDPdefault.IsEnabled = false;
            VC_radio_UDPcustom.IsEnabled = false;
            VC_UDPcustomIP.IsEnabled = false;
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            RC_data.UDP.Stop = true;
            VC_data.UDP.Stop = true;
            GUI_UDP_stop.IsEnabled = false;
            GUI_UDP_start.IsEnabled = true;
            UDP_RC_enable.IsEnabled = true;
            UDP_VC_enable.IsEnabled = true;
            RC_radio_UDPdefault.IsEnabled = true;
            RC_radio_UDPcustom.IsEnabled = true;
            RC_UDPcustomIP.IsEnabled = true;
            VC_radio_UDPdefault.IsEnabled = true;
            VC_radio_UDPcustom.IsEnabled = true;
            VC_UDPcustomIP.IsEnabled = true;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RC_data.name = RC_name.Text;
        }

        private void VC_name_TextChanged(object sender, TextChangedEventArgs e)
        {
            VC_data.name = VC_name.Text;
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            command.MODE = command.CMD_DONOTHING;
            mode_doNothing.IsChecked = true; 
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            command.MODE = command.CMD_RANDOMPATH;
            mode_rapidPath.IsChecked = true;
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            command.MODE = command.CMD_EGMPOSE;
            mode_egmMotion.IsChecked = true; 
        }

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            command.MODE = command.CMD_JOINTCONTROL;
            mode_jointController.IsChecked = true; 
        }

        private void Button_Click_10(object sender, RoutedEventArgs e)
        {
            command.MODE = command.CMD_HOLOPATH;
            mode_holoPath.IsChecked = true; 
        }

        private void holoLensDataSourceSimulation_Checked(object sender, RoutedEventArgs e)
        {
            //holoLensDataSourceReal.IsEnabled = true;
            //holoLensDataSourceSimulation.IsEnabled = false; 
        }

        private void holoLensDataSourceReal_Checked(object sender, RoutedEventArgs e)
        {
            //holoLensDataSourceReal.IsEnabled = false;
            //holoLensDataSourceSimulation.IsEnabled = true;
        }

        private void TCPcustom_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }

    ////////////////////////////////////////                ////////////////////////////////////////
    ////////////////////////////////////////     Classes    ////////////////////////////////////////
    ////////////////////////////////////////                ////////////////////////////////////////

    /*
    public class User : INotifyPropertyChanged
    {
        private string name;
        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    this.name = value;
                    this.NotifyPropertyChanged("Name");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
    }
    */
    ////////////////////////////////////////                ////////////////////////////////////////
    ////////////////////////////////////////     Console    ////////////////////////////////////////
    ////////////////////////////////////////                ////////////////////////////////////////

    internal static class ConsoleAllocator
    {
        [DllImport(@"kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        [DllImport(@"kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport(@"user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SwHide = 0;
        const int SwShow = 5;


        public static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();

            if (handle == IntPtr.Zero)
            {
                AllocConsole();
            }
            else
            {
                ShowWindow(handle, SwShow);
            }
        }

        public static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();

            ShowWindow(handle, SwHide);
        }
    }
    
}
