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

using ABB.Robotics.Controllers;
using ABB.Robotics.Controllers.Discovery;
using ABB.Robotics.Controllers.RapidDomain;
using ABB.Robotics.Controllers.EventLogDomain;
using System.IO; // for saving and loading local files
using System.Windows;
using System.Threading;
using System.ComponentModel;

namespace MetaMove
{
	class PCSDK
	{

		Controller controller;
		public bool loginSuccess = false;
		Tools tools = new Tools();

		byte preMod = 0;


		public MainWindow Guid { get; set; }
		private CommandClass command = new CommandClass();
		private RobotFeedback controllerData = new RobotFeedback();
		public PCSDK(ref RobotFeedback ctrl, CommandClass cmd, MainWindow guid)
		{
			controllerData = ctrl;
			command = cmd;
			Guid = guid;
		}

		private Thread PCSDKTHREAD;
		public void startThread()
		{

			PCSDKTHREAD = new Thread(new ThreadStart(PCSDKThread));
			PCSDKTHREAD.Start();

		}

		/*
		public event PropertyChangedEventHandler PropertyChanged;
		private RapidData _selectedRapidData;
		public RapidData SelectedRapidData
		{
			get => _selectedRapidData;
			set
			{
				_selectedRapidData = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedRapidData"));
			}
		}
		*/


		private void PCSDKThread() {

			byte state = 0;
			byte WizardSimulationState = 0;

			bool setDataSourceHolo = false;

			controllerData.PCSDK.Enable = false;
			DateTime startTime = DateTime.Now;
			DateTime endTime = DateTime.Now;

			ResetWizardSimulation();

			while (true)
			{
                if (controllerData.PCSDK.Enable) 
				{ 
					if (controllerData.PCSDK.Stop /*|| controllerData.PCSDK.ERROR*/) // restarting with GUI when Error occurs
					{
						Logoff();

						controllerData.RAPID.ExecutionCycleForever = false;
						controllerData.RAPID.Running = false;
						controllerData.RAPID.AutomaticMode = false; 

						state = 0;

						Console.WriteLine("PCSDK\tstop");
						controllerData.MsgTorque = string.Empty;

						controllerData.PCSDK.Stop = false;
						controllerData.PCSDK.ERROR = false; 

					}

					controllerData.PCSDK.Online = Connected();
					if (!controllerData.PCSDK.Online)
						Logoff();

					startTime = DateTime.Now;
					switch (state)
					{
						case 0:
							if (controllerData.PCSDK.Start)
							{
								state++;
								controllerData.PCSDK.Start = false;
								Console.WriteLine("PCSDK\tstart");
							}
							break;

						case 1:
							if (controllerData.PCSDK.Enable)
							{
								if (Login(controllerData.type, controllerData.name))
								{
									controllerData.PCSDK.Online = true;
									state++;
									Console.WriteLine("PCSDK\tconnected to " + controllerData.type + " controller");

									// EVENTS:

									// operating mode (auto/manual/...)
									controller.OperatingModeChanged += new EventHandler<OperatingModeChangeEventArgs>(ctrl_OperatingModeChanged);
									// state (motors on/off)
									// controller.StateChanged += new EventHandler<StateChangedEventArgs>(ctrl_StateChanged);
									// status (running/stopped)
									controller.Rapid.ExecutionStatusChanged += new EventHandler<ExecutionStatusChangedEventArgs>(ctrl_ExecutionStatusChanged);
									// cycle (forever/single)
									controller.Rapid.ExecutionCycleChanged += new EventHandler<EventArgs>(ctrl_ExecutionCycleChanged);
								
									// SelectedRapidData = controller.Rapid.GetTask("T_DATA").GetModule("module_GETDATA").GetRapidData("TOR");
									// SelectedRapidData.ValueChanged += new EventHandler<DataValueChangedEventArgs>(ctrl_ValueChanged);
									
								}
								else
								{
									state = 0;
									MessageBox.Show(controllerData.type + "controllerData not found: \"" + controllerData.name + "\"");
								}
							}
							else
							{
								state++;
							}
							break;

						case 2:
							if (controllerData.PCSDK.Online)
							{
								controllerData.RAPID.Running = getRapidStatus();
								if (ExecutionCycle())
									controllerData.RAPID.ExecutionCycleForever = true;
								else
								{
									controllerData.RAPID.ExecutionCycleForever = false;
									SetExecutionCycle();
								}
								state++;
							}
							else
								state = 0;
							break;

						case 3:

							if (controllerData.PCSDK.Enable)
							{
								if(command.MODE != command.CMD_WIZARD && controllerData.type == "virtual") 
								{ 
									GetTorque(controllerData.torque);
									controllerData.MsgTorque = tools.TorqueFromFeedback(controllerData.torque);
								}
								// GetExtTorque(controllerData.extTorque);
								// foreach(double t in controllerData.extTorque)
								//	 Console.WriteLine("torque: " + t);
								//GetPosition(controllerData.position);

								// SET JOINT CONTROL ANGLES
								if (command.MODE == command.CMD_JOINTCONTROL && controllerData.CTRL_jointTargetReceived)
								{
									controllerData.CTRL_jointTargetReceived = false;
									SetJointControl(controllerData.CTRL_jointTarget);
									//command.MODE = command.CMD_DONOTHING;
									Console.WriteLine("PCSDK\tset jointTarget");
								}

								// SET MODE IN RAPID CODE (if changed) 
								SetMode(command.MODE);

								// HOLOPATH
								{
									if (command.MODE == command.CMD_HOLOPATH && command.HOLOPATH.Simulate)
									{
										if(controllerData.type == "virtual") {
											/*
                                            if (!command.HoloDataSourceSimulation) 
											{
												command.HoloDataSourceSimulation = true; 
												setDataSourceHolo = true; 
											}
											*/
											command.OverrideDataSource = true;
											SetVariable("module_HOLOPATH", "validationComplete", false);
											SetHoloPath();
											command.HOLOPATH.Simulate = false;
											command.HOLOPATH.WaitValidation = true;
											
										}
									}
									if (command.MODE == command.CMD_HOLOPATH && command.HOLOPATH.WaitValidation)
									{
										if (controllerData.type == "virtual")
										{
											bool[] TargetValid = new bool[10];
											if (CheckTargetsValid(ref TargetValid)) { 
												command.HOLOPATH.WaitValidation = false;
											}
										}
									}
									if (command.MODE == command.CMD_HOLOPATH && command.HOLOPATH.Move)
									{
										if (controllerData.type == "real")
										{
											command.OverrideDataSource = false; 
											/*
                                            if (setDataSourceHolo)
											{
												command.HoloDataSourceSimulation = false;
												setDataSourceHolo = false;
											}
											*/
											SetHoloPath();
											command.HOLOPATH.Move = false; //jakobnode das muss noch weg 
										}
									}
								}

								// WIZARD SIMULATION
								if (command.MODE == command.CMD_WIZARD)
								{
									switch (WizardSimulationState)
									{
										case 0:
											if (controllerData.type == "virtual" && controllerData.RAPID.Running == false)
												MessageBox.Show("PCSDK\tWIZARD - VC RAPID not running!");
											if (command.WIZARD.RC_start && controllerData.type == "real")
											{
												SetMode(command.CMD_WIZARD);
												WizardSimulationState++;
											}
											else if (command.WIZARD.VC_start && controllerData.type == "virtual")
												WizardSimulationState++;
											break;
										case 1:
											if (controllerData.type == "real")
											{
											
												if (!command.WIZARD.ModuleDownloadComplete)
												{
													Console.WriteLine(" ");
													Console.WriteLine("PCSDK\tWIZARD STARTING");

													Console.WriteLine("PCSDK\tWIZARD downloading");
													if (downloadFromRC())
													{
														command.WIZARD.ModuleDownloadComplete = true;
														command.WIZARD.ModuleUploadComplete = false;
														WizardSimulationState = 100;
													}
													else
													{
														MessageBox.Show("ERROR - WIZARD downloading file");
														ResetWizardSimulation();
													}
												}
											}
											else
												WizardSimulationState++;
											break;

										case 2:
											if(command.WIZARD.ModuleDownloadComplete)
											{
												controller.Rapid.Stop();
												Console.WriteLine("PCSDK\tWIZARD stopping VC");
												WizardSimulationState++;
											}
											break;

										case 3:
											if (controllerData.RAPID.Running == false/* && GetMode() == 50*/)
											{
												Console.WriteLine("PCSDK\tWIZARD uploading");
												command.OverrideDataSource = true; 
												/*
												if (!command.HoloDataSourceSimulation)
												{
													command.HoloDataSourceSimulation = true;
													setDataSourceHolo = true;
												}
												*/
												if (uploadTocontroller())
												{
													//MessageBox.Show("UPLOAD COMPLETE");
													Console.WriteLine("WIZARD\tstarting simulation");
													command.WIZARD.ModuleUploadComplete = true;
													WizardSimulationState = 100;
												}
												else
												{
													MessageBox.Show("ERROR - WIZARD uploading file");
													ResetWizardSimulation();
												}

											}
											//if (controllerData.RAPID.Running == false)
											//	MessageBox.Show("RAPID not running (type: " + controllerData.type + ")");
											break;

										case 100:
											Console.WriteLine("PCSDK\tWIZARD " + controllerData.type + " finished");
											WizardSimulationState++;
											break;
										case 101:
											if (controllerData.type == "real")
											{
												WizardSimulationState = 0;
												command.WIZARD.RC_start = false;
												Console.WriteLine("PCSDK\tWIZARD RC reset");
											}

											if (GetMode() == 0 && controllerData.type == "virtual")
											{
												WizardSimulationState = 0;
												Console.WriteLine("PCSDK\tWIZARD VC reset");
												command.MODE = command.CMD_DONOTHING;
												ResetWizardSimulation();
											}
											break;

									}


								}
								else
								{
								
								}

								if (controllerData.type == "virtual")
								{
									if (controllerData.RAPID.Start)
									{
										Start();
										controllerData.RAPID.Start = false; 
									}
									else if (controllerData.RAPID.Stop)
									{
										Stop();
										controllerData.RAPID.Stop = false;
									}

								}


								//GetPosition(controllerData.position);




								/*
									DateTime startOp1 = DateTime.Now;
										//SelectedRapidData = controller.Rapid.GetRapidData("T_DATA", "module_GETDATA", "TOR");

										//Console.WriteLine("VALUE: " + SelectedRapidData.Value);
									DateTime endOp1 = DateTime.Now;
									TimeSpan durationOp1 = endOp1 - startOp1;
									Console.WriteLine("PCSDK\tOP1 duration: " + durationOp1.TotalMilliseconds + " ms ");


									//DateTime startOp2 = DateTime.Now;

									//DateTime endOp2 = DateTime.Now;
									//TimeSpan durationOp2 = endOp2 - startOp2;
									Console.WriteLine("PCSDK\tOP2 duration: " + durationOp2.TotalMilliseconds + " ms ");


									DateTime startOp2 = DateTime.Now;
									float val = (float)(Num)SelectedRapidData.Value;
									DateTime endOp2 = DateTime.Now;
									TimeSpan durationOp2 = endOp2 - startOp2;
									Console.WriteLine("PCSDK\tdirect duration: " + durationOp2.TotalMilliseconds + " ms " + val);
									*/


								//bool rapidStatus = false;//controller_pcsdk.getRapidStatus();
								/*
								if (start)
								{
									controller_pcsdk.Start();
									start = false;
								}
								else if (stop)
								{
									controller_pcsdk.Stop();
									stop = false;
								}
								*/
								/*
								if (newJointTarget)
								{
									controller_pcsdk.JointControl(jointTarget);
									newJointTarget = false;
								}
								*/
							}
							else
								Console.WriteLine("PCSDK\tgetNOdata");
							/*
							if (simulate)
							{

								simulate = false;
								RC_pcsdk.downloadFromRC();
								controller_pcsdk.uploadTocontroller();

							}
							*/


							break;

					}
					endTime = DateTime.Now;
					TimeSpan duration = endTime - startTime;
					//if(controllerData.PCSDK.Enable && controllerData.PCSDK.Online)
					//	Console.WriteLine("PCSDK\tduration: " + duration.TotalMilliseconds + " ms (state: " + state + ") " + controllerData.type);
				}
				Thread.Sleep(5); // jakobnode speed up when not connected to unity simulation
				
			}

		}


		/*
		private void Rd_ValueChanged(object sender, DataValueChangedEventArgs e)
		{


				var subscribedRapidVariable = sender as RapidData;
				Console.WriteLine(subscribedRapidVariable.Value.ToString());


		}
		
		private void ctrl_ValueChanged(object sender, DataValueChangedEventArgs e)
		{


			DateTime startOp2 = DateTime.Now;
			var subscribedRapidVariable = sender as RapidData;
			Console.WriteLine("PCSDK\tnew Value: " + subscribedRapidVariable.Value.ToString());


			DateTime endOp2 = DateTime.Now;
			TimeSpan durationOp2 = endOp2 - startOp2;
			Console.WriteLine("PCSDK\tevent duration: " + durationOp2.TotalMilliseconds + " ms ");

		}
		*/
		private void ResetWizardSimulation()
		{
			command.WIZARD.ModuleDownloadComplete = false;
			command.WIZARD.ModuleUploadComplete = false;
			command.WIZARD.VC_start = true;
			command.WIZARD.RC_start = true;
			command.MODE = command.CMD_DONOTHING;
		}


		private void ctrl_OperatingModeChanged(object sender, OperatingModeChangeEventArgs e)
		{
			Console.WriteLine("PCSDK\tnewOperating mode at: {0} new mode is: {1}", e.Time, e.NewMode);

			if (e.NewMode == ControllerOperatingMode.Auto)
				controllerData.RAPID.AutomaticMode = true;
			else
				controllerData.RAPID.AutomaticMode = false;

		}

		private void ctrl_StateChanged(object sender, StateChangedEventArgs e)
		{
			Console.WriteLine("PCSDK\tnewSTATE mode at: {0} new mode is: {1}", e.Time, e.NewState);
			/*
			if (e.NewState == ControllerOperatingMode.Auto)
				controllerData.RAPID.automaticMode = true;
			else
				controllerData.RAPID.automaticMode = false;
			*/
		}

		private void ctrl_ExecutionStatusChanged(object sender, ExecutionStatusChangedEventArgs e)
		{
			Console.WriteLine("PCSDK\tnewSTATE mode at: {0} new mode is: {1}", e.Time, e.Status);
			
			if (e.Status == ExecutionStatus.Running)
				controllerData.RAPID.Running = true;
			else
				controllerData.RAPID.Running = false;
		}

		
		private void ctrl_ExecutionCycleChanged(object sender, EventArgs e)
		{
			Console.WriteLine("PCSDK\tctrl_ExecutionCycleChanged " + e.ToString());

			/*
			if (e. == ControllerOperatingMode.Auto)
				controllerData.RAPID.automaticMode = true;
			else
				controllerData.RAPID.automaticMode = false;
			*/

			controllerData.RAPID.ExecutionCycleForever = ExecutionCycle();
		}


		private bool Login(string ctrType, string ctrName) {

			int timeout = 0;
			while ((controller = CreateController(ctrType, ctrName)) == null)
			{
				timeout++;
				if (timeout >= 1)
				{
					Console.WriteLine("PCSDK\tTIMEOUT");
					return false;
				}
			}
			
			controller.Logon(UserInfo.DefaultUser);
			if (controller.Connected)
				loginSuccess = true;
			controller.Logoff();
			return true;
		}

		private void Logoff()
		{
			
			if (loginSuccess)
			{
				Console.WriteLine("PCSDK\tLogoff");
				loginSuccess = false; 
				if (true)//controller.Connected)
				{ 


					controller.OperatingModeChanged -= new EventHandler<OperatingModeChangeEventArgs>(ctrl_OperatingModeChanged);
					controller.Rapid.ExecutionStatusChanged -= new EventHandler<ExecutionStatusChangedEventArgs>(ctrl_ExecutionStatusChanged);
					controller.Rapid.ExecutionCycleChanged -= new EventHandler<EventArgs>(ctrl_ExecutionCycleChanged);

					controller.Logoff();
					//jakobnode dispose controller?
					controller.Dispose();

					controllerData.RAPID.ExecutionCycleForever = false;
					controllerData.RAPID.Running = false;
					controllerData.RAPID.AutomaticMode = false;
				}
			}
		}

		private bool Connected() {

			if (loginSuccess)
				return controller.Connected;
			else
			{
				return false;
			}
				

		}
		/*
		private void JointControl(float[] val){

			//controller.Logon(UserInfo.DefaultUser);
			using (Mastership mastercontroller = Mastership.Request(controller.Rapid))
			{
				try
				{

					JointTarget joiTar = new JointTarget();
					joiTar.RobAx.Rax_1 = val[0];
					joiTar.RobAx.Rax_2 = val[1];
					joiTar.RobAx.Rax_3 = val[2];
					joiTar.RobAx.Rax_4 = val[3];
					joiTar.RobAx.Rax_5 = val[4];
					joiTar.RobAx.Rax_6 = val[5];

					controller.Rapid.GetRapidData("T_Rob1", "module_MAIN", "jointControlTarget").Value = joiTar;

				}
				catch (Exception ex)
				{
					Console.WriteLine("controller\tERROR\n " + ex);
				}
				finally
				{
					mastercontroller.Release();
				}

			}
			//controller.Logoff();

		}
		*/
		private bool ExecutionCycle() {

			if (controller.Rapid.Cycle == ABB.Robotics.Controllers.RapidDomain.ExecutionCycle.Forever)
				return true;
			else
				return false; 
		
		}

		private void SetExecutionCycle()
		{
			try
			{
				using (Mastership mastercontroller = Mastership.Request(controller))
				{
					try
					{
						controller.Rapid.Cycle = ABB.Robotics.Controllers.RapidDomain.ExecutionCycle.Forever;
					}
					catch (Exception ex)
					{
						Console.WriteLine("controller\tERROR\n " + ex);
					}
					finally
					{
						mastercontroller.Release();
					}
				}
			}
			catch (Exception)
			{
				controllerData.PCSDK.Stop = true;
				MessageBox.Show("ERROR: PCSDK connection (Execution Cycle)");
			}
		}

		private void SetJointControl(float[] joint)
		{
			controller.Logon(UserInfo.DefaultUser);
			try
			{
				//controller.Logon(UserInfo.DefaultUser); /jakobnode überprüfen ob mastership übernommen werden kann
				using (Mastership mastercontroller = Mastership.Request(controller.Rapid))
				{
					try
					{
						JointTarget jointTar = JointTarget.Parse("[[0,0,0,0,0,0],[0,0,0,0,0,0]]");
						jointTar.RobAx.Rax_1 = joint[0];
						jointTar.RobAx.Rax_2 = joint[1];
						jointTar.RobAx.Rax_3 = joint[2];
						jointTar.RobAx.Rax_4 = joint[3];
						jointTar.RobAx.Rax_5 = joint[4];
						jointTar.RobAx.Rax_6 = joint[5];
						Console.WriteLine("PCSDK\tjointTarget: " + jointTar.ToString());
						controller.Rapid.GetRapidData("T_Rob1", "module_MAIN_GOHOLO", "jointControlTarget").Value = jointTar; //.WriteItem(jointTar, 1);

						Bool activate = new ABB.Robotics.Controllers.RapidDomain.Bool();
						activate.Value = true;
						controller.Rapid.GetRapidData("T_Rob1", "module_MAIN_GOHOLO", "actJointControlTarget").Value = activate;

						/*
							robTarNum.Value = 1;
							RapidData robTarNumRap = controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_TargetNumber");
							robTarNumRap.Value = robTarNum;






							Num rapMod = new Num();
							rapMod.Value = mode;
							controller.Rapid.GetRapidData("T_Rob1", "module_MAIN", "mode").Value = rapMod;


							RobTarget[] robTar = new RobTarget[10];

							for (int i = 0; i < 10; i++)
								robTar[i] = (RobTarget)controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_Target").ReadItem(i);



							/*
							foreach (RobTarget tar in robTar)
								Console.WriteLine(tar.Trans + " " + tar.Rot);



							foreach (RobTarget tar in robTar)
								Console.WriteLine(tar.Trans + " " + tar.Rot);
							*/
						// Release and dispose mastership

					}
					catch (Exception ex)
					{
						Console.WriteLine("controller\tERROR\n " + ex);
					}
					finally
					{
						mastercontroller.Release();
					}

				}
			}
			catch (Exception)
			{
				MessageBox.Show("ERROR mastership");
			}
			controller.Logoff();


		}

		private void SetMode(byte mode) {

			if( controllerData.CTRL_sendMode )
			{
			//if (preMod != mode)

				command.OverrideDataSource = false;
				controllerData.MsgValidation = string.Empty;

				preMod = mode;
					controllerData.CTRL_sendMode = false;

				Console.WriteLine("PCSDK\tset mode to " + mode);
				//controller.Logon(UserInfo.DefaultUser); /jakobnode überprüfen ob mastership übernommen werden kann
				try
				{
					controller.Logon(UserInfo.DefaultUser);
					using (Mastership mastercontroller = Mastership.Request(controller))//.Rapid))
					{
						try
						{
							Console.WriteLine("PCSDK\tsetting mode");
							//Num robTarNum = (Num)controller.Rapid.GetRapidData("T_Rob1", "module_MAIN", "mode").Value;

							Num rapMod = new Num();
							rapMod.Value = mode;
							controller.Rapid.GetRapidData("T_Rob1", "module_MAIN_GOHOLO", "mode").Value = rapMod;

							/*
							RobTarget[] robTar = new RobTarget[10];

							for (int i = 0; i < 10; i++)
								robTar[i] = (RobTarget)controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_Target").ReadItem(i);


							foreach (RobTarget tar in robTar)
								Console.WriteLine(tar.Trans + " " + tar.Rot);

							Console.WriteLine("\nUPDATING POSITIONS");
							RobTarget robTarUpd = RobTarget.Parse("[[0, 0, 0],[0, 1, 0, 0],[0, 0, 0, 0],[9E+09, 9E+09, 9E+09, 9E+09, 9E+09, 9E+09]]");
							robTarUpd.Trans.X = 4;
							robTarUpd.Trans.Y = 5;
							robTarUpd.Trans.Z = 6;

							controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_Target").WriteItem(robTarUpd, 1);

							robTarNum.Value = 1;
							RapidData robTarNumRap = controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_TargetNumber");
							robTarNumRap.Value = robTarNum;

							foreach (RobTarget tar in robTar)
								Console.WriteLine(tar.Trans + " " + tar.Rot);
							*/
							mastercontroller.Release();
						}
						catch (Exception ex)
						{
							Console.WriteLine("PCSDK\tERROR setting mode\n " + ex);
						}
						finally
						{
							mastercontroller.Release();
						}
						
					}
					controller.Logoff();
				}
				catch (Exception ex)
				{
					MessageBox.Show("PCSDK: Error requesting mastership (mode) on controller: " + controllerData.type + "\n"+ ex);
				}
				
				
				controller.Logoff();
			}

		}

		private void SetVariable(string module, string variable, bool value)
		{

			try
			{
				controller.Logon(UserInfo.DefaultUser);
				using (Mastership mastercontroller = Mastership.Request(controller))//.Rapid))
				{
					try
					{
						Bool rapMod = new Bool();
						rapMod.Value = value;
						controller.Rapid.GetRapidData("T_Rob1", module, variable).Value = rapMod;

						mastercontroller.Release();
					}
					catch (Exception ex)
					{
						Console.WriteLine("PCSDK\tERROR setting variable\n " + ex);
					}
					finally
					{
						mastercontroller.Release();
					}

				}
				controller.Logoff();
			}
			catch (Exception ex)
			{
				MessageBox.Show("PCSDK: Error requesting mastership (variable) " + ex);
			}

			controller.Logoff();
		}


		private bool getMotors() {

			return true; // controller.Rapid.Controller.MotionSystem.
		}

		private bool getRapidStatus() { //jakobnode hier noch try catch einbauen (schauenob es das Modul überhaupt gibt)

			//Task task_DATA = controller.Rapid.GetTask("T_DATA");
			Task task_ROB1 = controller.Rapid.GetTask("T_ROB1");

			//bool runData = task_DATA.ExecutionStatus.Equals(TaskExecutionStatus.Running);
			bool runRob = task_ROB1.ExecutionStatus.Equals(TaskExecutionStatus.Running);

			//return runData&&runRob;
			return runRob;
		}

		private bool getAutomaticMode() {

			if (controller.OperatingMode.ToString() == "Auto")
				return true;
			else
				return false; 
		}

		private int GetMode()
		{
			int mode = 0; 
			try
			{
				Num rapMode = new Num();
				rapMode = (Num)controller.Rapid.GetRapidData("T_ROB1", "module_MAIN_GOHOLO", "mode").Value;
				mode = Convert.ToInt32(rapMode.Value);
			}
			catch (Exception)
			{
				MessageBox.Show("ERROR getting mode");
				controllerData.PCSDK.ERROR = true; //jakob node - ERRO macht nichts?! 
			}
			//controller.Logoff();
			return mode;

		}

		private bool GetTorque(double[] tor) {
			//controller.Logon(UserInfo.DefaultUser);
			Num[] robTorNum = new Num[6];

		 
				for (int i = 0; i < 6; i++)
				{
					try
					{
						tor[i] = (Math.Round((Double)(Num)controller.Rapid.GetRapidData("T_DATA", "module_GETDATA", "feedbackTorque").ReadItem(i) * 100)) / 100;
					}
					catch (Exception)
					{
						MessageBox.Show("ERROR getting torque feedback");
						controllerData.PCSDK.ERROR = true;
						break;
					}

				}

			//controller.Logoff();
			return true; 
		}

		private bool GetExtTorque(double[] tor)
		{
			//controller.Logon(UserInfo.DefaultUser);
			Num[] robTorNum = new Num[6];
            try { 
				for (int i = 0; i < 6; i++)
				{
					tor[i] = (Math.Round((Double)(Num)controller.Rapid.GetRapidData("T_DATA", "module_GETDATA", "feedbackExtTorque").ReadItem(i) * 100)) / 100;
				}
			}
			catch (Exception)
			{
				MessageBox.Show("ERROR getting external torque feedback");
			}
			//controller.Logoff();
			return true;
		}

		private bool GetPosition(double[] pos)
		{
			//controller.Logon(UserInfo.DefaultUser);
			Num[] robTorNum = new Num[6];
            try { 
				for (int i = 0; i < 6; i++)
				{
					pos[i] = (Math.Round((Double)(Num)controller.Rapid.GetRapidData("T_DATA", "module_GETDATA", "feedbackPosition").ReadItem(i) * 100)) / 100;
				}
				//Console.WriteLine((Math.Round((Double)(Num)controller.Rapid.GetRapidData("T_DATA", "module_GETDATA", "feedbackPosition").ReadItem(6) * 100)) / 100);
			}
			catch (Exception)
			{
				MessageBox.Show("ERROR getting torque feedback");
			}
			//controller.Logoff();
			return true;
		}

		private void Start() {
			controller.Logon(UserInfo.DefaultUser);
			//if (AppControllerData.SelectedController.OperatingMode == ControllerOperatingMode.Auto)
			if (controller.OperatingMode == ControllerOperatingMode.Auto)
			{
				if (!controller.Rapid.ExecutionStatus.Equals(TaskExecutionStatus.Running))
				{
					using (Mastership mastercontroller = Mastership.Request(controller))//.Rapid))
					{
						try
						{
							Console.WriteLine("PCSDK\tstarting");
							Task task_DATA = controller.Rapid.GetTask("T_DATA");
							Task task_ROB1 = controller.Rapid.GetTask("T_ROB1");

							task_DATA.ResetProgramPointer();
							task_ROB1.ResetProgramPointer();
							task_DATA.Start();
							task_ROB1.Start();
							
							controller.Rapid.Start();

							mastercontroller.Release();
						}
						catch (Exception ex)
						{
							Console.WriteLine("controller\tERROR\n " + ex);
						}
						finally
						{
							mastercontroller.Release();
						}

					}
				}
			}
			else 
			{
				//MessageBox.Show("Automatic mode is required to start/stop execution from a remote client.");
			}
			controller.Logoff();
		}

		private void Stop() {
			
			controller.Logon(UserInfo.DefaultUser);
			if (!controller.Rapid.ExecutionStatus.Equals(TaskExecutionStatus.Stopped))
			{
				
				using (Mastership mastercontroller = Mastership.Request(controller))//.Rapid))
				{
					try
					{
						Num rapMod = new Num();
						rapMod.Value = 0;
						controller.Rapid.GetRapidData("T_Rob1", "module_MAIN_GOHOLO", "mode").Value = rapMod;

						Console.WriteLine("PCSDK\tstopping");
						controller.Rapid.Stop();
						Task task_DATA = controller.Rapid.GetTask("T_DATA");
						Task task_ROB1 = controller.Rapid.GetTask("T_ROB1");
						task_DATA.Stop();
						task_ROB1.Stop();
						
					}
					catch (Exception ex)
					{
						Console.WriteLine("controller\tERROR\n " + ex);
					}
					finally
					{
						mastercontroller.Release();
					}

				}
			}
			controller.Logoff();
		}

		
		private void SetHoloPath()
		{
			controller.Logon(UserInfo.DefaultUser);
			try { 
				using (Mastership mastercontroller = Mastership.Request(controller))//.Rapid))
				{
					try
					{
						/*
						Console.WriteLine("READING POSITIONS FROM MODULE");
						Num robTarNum = (Num)controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_TargetNumber").Value;
						Console.WriteLine("robTarNum:\t" + robTarNum);

						RobTarget[] robTar = new RobTarget[10];

						for (int i = 0; i < 10; i++)
							robTar[i] = (RobTarget)controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_Target").ReadItem(i);

						foreach (RobTarget tar in robTar)
							Console.WriteLine(tar.Trans + " " + tar.Rot);
						*/

						// updating pose array on controller
						Console.WriteLine("\nPCSDK\tholoPath - updating positions on " + controllerData.type + " controller");
						for(int i = 0; i < controllerData.CTRL_holoPathNumber; i++ )
						{ 
							RobTarget robTarUpd = RobTarget.Parse("[[0, 0, 0],[0, 1, 0, 0],[0, 0, 0, 0],[0, 9E+09, 9E+09, 9E+09, 9E+09, 9E+09]]"); // jakobnode external axis for YuMi
							robTarUpd.Trans.X = controllerData.CTRL_holoPathPose[i].pos[0];
							robTarUpd.Trans.Y = controllerData.CTRL_holoPathPose[i].pos[1];
							robTarUpd.Trans.Z = controllerData.CTRL_holoPathPose[i].pos[2];

							robTarUpd.Rot.Q1 = controllerData.CTRL_holoPathPose[i].ori[0];
							robTarUpd.Rot.Q2 = controllerData.CTRL_holoPathPose[i].ori[1];
							robTarUpd.Rot.Q3 = controllerData.CTRL_holoPathPose[i].ori[2];
							robTarUpd.Rot.Q4 = controllerData.CTRL_holoPathPose[i].ori[3];

							controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_Target").WriteItem(robTarUpd, i);
						}

						// updating pose number on controller
						Num robTarNum = (Num)controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_TargetNumber").Value;
						robTarNum.Value = controllerData.CTRL_holoPathNumber;
						RapidData robTarNumRap = controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_TargetNumber");
						robTarNumRap.Value = robTarNum;

						// setting startflag
						Bool holoPathExecute = (Bool)controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPathExecute").Value;
						holoPathExecute.Value = true;
						RapidData rapHoloPathExecute = controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPathExecute");
						rapHoloPathExecute.Value = holoPathExecute;
						

					}
					catch (Exception ex)
					{
						Console.WriteLine("controller\tERROR\n " + ex);
					}
					finally
					{
						mastercontroller.Release();
					}
				}
			}
			catch (Exception)
			{
				MessageBox.Show("ERROR mastership");
			}
			controller.Logoff();
		}

		public bool CheckTargetsValid(ref bool[] tarVal)
		{

			controller.Logon(UserInfo.DefaultUser);
			try
			{
				bool validationComplete = (Bool)controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "validationComplete").Value;
                if (validationComplete) 
				{
					validationComplete = false;
					string validationStr = "{\"validation\":[";
					for (int i = 0; i < controllerData.CTRL_holoPathNumber; i++)
					{
						Bool rapTarVal = (Bool)controller.Rapid.GetRapidData("T_Rob1", "module_HOLOPATH", "holoPath_TargetValid").ReadItem(i);

						tarVal[i] = rapTarVal.Value;
						Console.WriteLine("PCSDK\tholoPath - position " + (i+1) + " validation: " + tarVal[i]);
						validationStr += tarVal[i].ToString();
						if (i < (controllerData.CTRL_holoPathNumber - 1) )
							validationStr += ",";
					}
					validationStr += "]\"v\"}";
					//Console.WriteLine("validation string: " + validationStr);
					controllerData.MsgValidation = validationStr;
					controller.Logoff();
					return true; 
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("ERROR target validation " + ex);
				MessageBox.Show("ERROR target validation");
			}
			controller.Logoff();
			return false; 
		}


		private bool downloadFromRC()
		{
			
			controller.Logon(UserInfo.DefaultUser);
			Task tRobi01 = controller.Rapid.GetTask("T_ROB1");

			try
			{
				Mastership master = Mastership.Request(controller);//.Rapid))

				Module versuch = tRobi01.GetModule("module_MAIN");

				// saving file to the HOME directory of the robot
				versuch.SaveToFile(@"HOME");

				// saving file to project folder of PC
				string currentProjectPath = Path.Combine(Environment.CurrentDirectory, @"", "module_MAIN.modx");
				Console.WriteLine("PCSDK\tpath for saving: "+currentProjectPath);
				try
				{
					// Check if file exists with its full path
					if (File.Exists(currentProjectPath))
					{
						// If file found, delete it    
						File.Delete(currentProjectPath);
						Console.WriteLine("file\tFile deleted.");
					}
					else Console.WriteLine("file\tFile not found");
				}
				catch (IOException ioExp)
				{
					Console.WriteLine("PCSDK\tWIZARD error deleting locla file " + ioExp.Message);
					controller.Logoff();
					return false; 
				}
				
				
				Console.WriteLine("trying to remove");
				
				Console.WriteLine("file exists: " +controller.FileSystem.FileExists(@"module_MAIN.modx"));
				Console.WriteLine("type: " + controllerData.type);
				//Console.WriteLine("patho: " + patho); 



				//controller.FileSystem.RemoveFile(patho);



				Console.WriteLine("removed");


				controller.FileSystem.GetFile(@"module_main.modx", currentProjectPath); // remote = robot, locla = PC
				Console.WriteLine("file\t" + currentProjectPath);
				controller.FileSystem.RemoveFile(@"module_MAIN.modx");
				

				//RC.Rapid.Start();

				// Release and dispose mastership
				master.Release();
				master.Dispose();
			}
			catch (Exception ex)
			{
				Console.WriteLine("error downloading file form RC " + ex);
				controller.Logoff();
				return false; 
			}
			controller.Logoff();
			
			return true; 

		}

		private void editFile(string path)
		{
			Console.WriteLine("PCSDK\tWIZARD file edit");
			string wizPath = Path.Combine(Environment.CurrentDirectory, @"", "module_WIZARD.modx");
			try
			{
				// Check if file exists with its full path    
				if (File.Exists(wizPath))
				{
					// If file found, delete it    
					File.Delete(wizPath);
					Console.WriteLine("file\tFile deleted.");
				}
				else Console.WriteLine("file\tFile not found");
			}
			catch (IOException ioExp)
			{
				Console.WriteLine(ioExp.Message);
			}

			string text = System.IO.File.ReadAllText(path);
			text = text.Replace("module_MAIN", "module_WIZARD");
			text = text.Replace("main()", "mainWizard()");
			text = text.Replace("wi_tGripper", "tool0"); // jakobnode: using YuMi - GoFa in simulation with tool0

			// Display the file contents to the console. Variable text is a string.
			//Console.WriteLine(text);
			//Console.WriteLine(wizPath);
			File.WriteAllText(wizPath, text);
			Console.WriteLine("PCSDK\tWIZARD editing file complete");

		}
		private bool uploadTocontroller()
		{
			/*
			Controller ctrl_upload;
			while ((ctrl_upload = CreateController("virtual", "14050-Simulation")) == null)
			{
			}

			ctrl_upload.Logon(UserInfo.DefaultUser);
			*/
			controller.Logon(UserInfo.DefaultUser);
			Task taskctrl_upload = controller.Rapid.GetTask("T_ROB1");
			// Log on
			//ctrl_upload.Logon(UserInfo.DefaultUser);

            if (taskctrl_upload.ExecutionStatus.Equals(TaskExecutionStatus.Running))
            {
				MessageBox.Show("STOP TASK FIRST");
				//controller.Rapid.Stop();
				//	SetMode(command.CMD_WIZARD);
				controller.Logoff();
				return false; 
			
			}
            else 
			{ 
				try
				{
					Mastership masterctrl_upload = Mastership.Request(controller);//.Rapid))

					//Console.WriteLine("PCSDK\tWIZARD resetting program pointer");
					//taskctrl_upload.ResetProgramPointer();

					string currentProjectPath = Path.Combine(Environment.CurrentDirectory, @"", "module_MAIN.modx");
					editFile(currentProjectPath);

					string wizPath = Path.Combine(Environment.CurrentDirectory, @"", "module_WIZARD.modx");
					controller.FileSystem.PutFile(wizPath, @"module_WIZARD.modx", true);

					bool bLoadSuccess = false;
					bLoadSuccess = taskctrl_upload.LoadModuleFromFile(@"module_WIZARD.modx", RapidLoadMode.Replace);

					// True if loading succeeds without any errors, otherwise false. 
					if (!bLoadSuccess)
					{
						Console.WriteLine("PCSDK\tERROR in RAPID code");
						// Gets the available categories of the EventLog.
						foreach (EventLogCategory category in controller.EventLog.GetCategories())
						{
							if (category.Name == "Common")
							{
								if (category.Messages.Count > 0)
								{
									foreach (EventLogMessage message in category.Messages)
									{
										Console.WriteLine("Program [{1}:{2}({0})] {3} {4}",
											message.Name, message.SequenceNumber,
											message.Timestamp, message.Title, message.Body);
									}
								}
							}
						}
					}

					// setting startflag
					Bool holoSimulationExecute = (Bool)controller.Rapid.GetRapidData("T_Rob1", "module_MAIN_GOHOLO", "startSimulation").Value;
					holoSimulationExecute.Value = true;
					RapidData rapHoloSimulationExecute = controller.Rapid.GetRapidData("T_Rob1", "module_MAIN_GOHOLO", "startSimulation");
					rapHoloSimulationExecute.Value = holoSimulationExecute;

					//Console.WriteLine("\tstarting simulation");
					controller.Rapid.Start();
				
					// Release and dispose mastership
					masterctrl_upload.Release();
					masterctrl_upload.Dispose();
				}
				catch (Exception ex)
				{
					Console.WriteLine("controller\terror upload file to controller " + ex);
					controller.Logoff();
					return false; 
				}

			}
			controller.Logoff();
			return true; 

		}

		private static Controller CreateController(string type, string ctrName)
		{
			NetworkScanner scanner = new NetworkScanner();
			// .Real or .Virtual if looking for real robot or just controller 

			ControllerInfo[] controllers;

			//jakobnode for testing with 2 VC
			//controllers = scanner.GetControllers(NetworkScannerSearchCriterias.Virtual);
			
			if (type == "real")
				controllers = scanner.GetControllers(NetworkScannerSearchCriterias.Real);
			else
				controllers = scanner.GetControllers(NetworkScannerSearchCriterias.Virtual);
			

			foreach (ControllerInfo ctrl in controllers)
				Console.WriteLine("PCSDK\tavailable controllers: " + ctrl.Name);
            try { 
				if (controllers.Length > 0)
				{
					for(int i = 0; i < controllers.Length; i++) {
						if (controllers[i].Name == ctrName)
						{
							Console.WriteLine("PCSDK\tsearched controller found: " + ctrName);
							Controller dynamic = Controller.Connect(controllers[i] as ControllerInfo, ConnectionType.Standalone);
							return dynamic;
						}
					}
					
					Console.WriteLine("PCSDK\tcontroller NOT found: " + ctrName);
					return null;
				}
			}
			catch(Exception e)
			{
				Console.WriteLine("PCSDK\tERROR connecting: " + e);
			}
			return null;
		}
	}
}
