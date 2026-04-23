MODULE Morobot_Assembly
    
    LOCAL VAR num n_gripper_change;
    LOCAL VAR num n_program_selection;
    LOCAL VAR num n_temp;
    !Möltner - Unterscheidung in: in shared WS1, WS2 und robot's WS
    !Geschwindigkeitsregelung basierend auf Stress-Level soll in shared WS1 und WS2 stattfinden
    VAR wzstationary sWS1;
    VAR wzstationary sWS2;
    VAR wzstationary rWS;
    VAR intnum timeint;
    
    !###################################################################################
    ! Vorpositionen 
    !###################################################################################
    CONST robtarget pPreDesk:=[[-91.18,463.89,479.36],[0.0292572,0.268175,-0.962905,0.00633495],[-1,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pPre1_UT1:=[[-552.09,555.38,495.64],[0.0044952,-0.902819,0.429948,-0.00651631],[0,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pPre2_UT1:=[[-392.21,204.61,842.26],[0.667079,-0.560097,-0.323818,-0.369377],[-1,0,-2,6],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pPre3_UT1:=[[-350.89,976.02,658.71],[0.0375793,0.884847,0.460146,-0.0624369],[-1,0,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pPre4_UT1:=[[-414.66,598.41,495.29],[0.207994,0.825958,0.505094,-0.139328],[0,0,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    !###################################################################################
    ! Magazinpositionen
    !###################################################################################
    CONST robtarget pUT1_Kiste:=[[-513.73,2.11,-42.42],[6.04912E-05,0.00599409,-0.999982,-3.32221E-06],[1,0,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pUT2_Kiste:=[[-825.59,-2.15,-40.12],[8.13186E-05,-0.99997,0.00769148,-0.000126513],[1,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pUT3_Kiste:=[[-823.60,415.17,-36.21],[4.32479E-05,0.693819,-0.720149,-2.03609E-05],[1,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pOT1_Kiste:=[[-184.82,672.04,-16.32],[1.03823E-05,0.00826908,-0.999966,9.72737E-05],[0,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pOT2_Kiste:=[[-566.53,587.46,-15.57],[3.49322E-05,0.00829619,-0.999966,7.69623E-05],[0,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pOT3_Kiste:=[[-818.17,668.53,-15.11],[1.88135E-05,0.693917,-0.720055,0.000111014],[0,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    
    !
    PERS robtarget pUT1_wobj:=[[55.851,-12.2689,-40.5018],[0.00136461,-0.999882,-0.0148545,0.00368171],[1,0,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    PERS robtarget pUT2_wobj:=[[49.99,0.37,-40.58],[0.00286024,-0.00197367,-0.999994,2.60985E-05],[1,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    PERS robtarget pUT3_wobj:=[[57.45,-0.31,-39.77],[0.00363183,-0.713209,-0.70094,-0.00163103],[1,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    PERS robtarget pOT1_wobj:=[[55.0443,146.419,-19.26],[0.00982639,-0.999845,-0.0138512,-0.00463016],[0,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    PERS robtarget pOT2_wobj:=[[119.725,236.213,-16.9964],[0.00709128,-0.999804,-0.0184401,0.00104506],[0,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    PERS robtarget pOT3_wobj:=[[51.5463,156.869,-19.0193],[0.00462827,-0.712394,-0.70176,-0.00259848],[0,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    PERS robtarget pFT_wobj:=[[100,200,300],[1,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    !

    CONST robtarget pFT_Kiste:=[[145.22,14.78,-22.59],[0.00288643,0.699364,-0.714756,-0.00250918],[-2,-1,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pKiste_Uebergabe_1:=[[-261.73,1111.51,-40.12],[0.00238766,0.715105,0.699005,0.00325473],[-1,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pKiste_Uebergabe_2:=[[-538.07,1171.31,-51.88],[0.000332083,0.706618,0.707595,0.000175215],[0,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pKiste_Uebergabe_3:=[[-856.33,1169.13,-49.79],[0.000492859,0.706617,0.707596,0.000120657],[0,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pKiste_Vorne_1:=[[-267.40,690.49,-57.43],[0.00294082,-0.708083,-0.706112,-0.00395136],[-1,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pKiste_Vorne_2:=[[-543.21,752.89,-56.70],[0.000269182,0.706591,0.707622,0.000227048],[0,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pKiste_Vorne_3:=[[-854.93,752.86,-56.22],[0.000210849,0.706573,0.70764,0.000221873],[0,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pKiste_Hinten_1:=[[-227.08,-85.62,-60.56],[0.00475642,0.706536,0.707659,-0.00204104],[-2,-1,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pKiste_Hinten_2:=[[-548.78,-83.94,-61.86],[0.000240667,0.70655,0.707663,9.96488E-05],[1,0,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pKiste_Hinten_3:=[[-868.12,-82.69,-61.12],[0.000273639,0.706549,0.707664,0.000195961],[1,0,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pKiste_FT:=[[-863.48,337.21,-59.05],[0.000308987,0.706558,0.707655,0.000217246],[0,0,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pAblage_KistenGreifer:=[[-1028.76,298.52,67.06],[0.702797,-0.134574,-0.685832,0.132669],[1,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pAblage_VacuumGreifer:=[[-669.69,329.92,-37.07],[0.000763729,-0.976661,0.214782,0.0012781],[0,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];


    !###################################################################################
    ! Tischpositionen
    !###################################################################################
    CONST robtarget pUT1_Tisch:=[[519.86,117.61,93.95],[0.00724997,-0.700539,-0.713575,-0.00163116],[-2,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pUT2_Tisch:=[[429.17,127.86,99.08],[0.00721143,-0.712779,-0.70135,-0.00177402],[-1,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pUT3_Tisch:=[[358.81,116.67,97.20],[0.00720976,-0.70856,-0.705611,-0.00175584],[-2,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pOT1_Tisch:=[[528.70,118.89,119.77],[0.00726881,-0.696857,-0.717171,-0.00159584],[-1,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pOT2_Tisch:=[[440.77,142.77,122.36],[0.00728003,-0.696833,-0.717195,-0.00159892],[-1,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pOT3_Tisch:=[[357.61,111.54,120.96],[0.00728625,-0.720939,-0.692959,-0.00138065],[-2,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pServoAusrichtung_0_grad:=[[537.95,397.45,63.56],[3.35746E-05,0.707472,-0.706741,-1.72099E-05],[-1,-1,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pServoAusrichtung_90_grad:=[[537.94,397.46,63.54],[1.36248E-05,1,-0.00014261,-5.07474E-05],[-1,-2,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pFT1_Tisch:=[[437.45,122.64,95.43],[2.79685E-06,0.703961,-0.710238,3.52964E-06],[-2,-1,-3,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pFT2_Tisch:=[[438.48,210.07,68.38],[0.495848,0.496503,-0.503146,0.504444],[-1,-1,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pFT3_Tisch:=[[431.80,209.37,71.80],[0.503588,0.502242,0.497441,-0.496694],[-1,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pFT4_Tisch:=[[276.64,120.19,193.66],[0.500428,0.507541,0.491185,0.50071],[-2,1,-3,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pFT5_Tisch:=[[275.98,124.56,200.68],[0.503823,-0.49404,0.50701,-0.495003],[-2,1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pServoBox:=[[236.03,-110.15,154.03],[0.00019642,0.983366,-0.00584258,-0.181541],[-2,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget pKLTBox:=[[241.64,870.37,158.31],[0.000551274,0.984047,0.000871644,-0.177906],[-1,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    CONST robtarget pFT_Kiste10:=[[-513.73,2.11,-42.42],[6.00617E-05,0.0059922,-0.999982,-3.55453E-06],[1,0,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

    PERS tooldata tool_calib:=[TRUE,[[0.63997,1.78002,136.1],[1,0,0,0]],[0.491,[0,0,0],[1,0,0,0],0,0,0]];
    PERS tooldata tool_vacuum_gripper:=[TRUE,[[0,0,151.5],[0.190809,0,0,0.981627]],[0.707,[0,0,100],[1,0,0,0],0,0,0]];
    PERS tooldata tool_servo_rotator:=[TRUE,[[-45.156,-109.017,96.5],[0.587938,0.392848,-0.587938,0.392848]],[0.707,[0,0,40],[1,0,0,0],0,0,0]];
    PERS tooldata tool_single_vacuum:=[TRUE,[[74.05,178.77,88.5],[0.5879378,-0.3928475,0.5879378,0.3928475]],[0.707,[0,0,20],[1,0,0,0],0,0,0]];
    PERS tooldata tool_box_gripper:=[TRUE,[[0,0,93.5],[0.190809,0,0,0.981627]],[1.6,[0,0,100],[1,0,0,0],0,0,0]];
    PERS wobjdata wobj_colab:=[FALSE,TRUE,"",[[-311.878,-378.558,188.656],[0.711833,-0.0027417,0.00397248,-0.702332]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_tabel:=[FALSE,TRUE,"",[[0,-1000,200],[0.707106781,0,0,-0.707106781]],[[-300,0,200],[1,0,0,0]]];
    
    !#####################################################################
    !LOADS
    !#####################################################################
    PERS loaddata load_box := [1.6,[0,0,0.1],[1,0,0,0],0,0,0];
    PERS loaddata load_ut1 := [0.025,[0,0,0],[1,0,0,0],0,0,0];
    PERS loaddata load_ut2 := [0.025,[0,0,0],[1,0,0,0],0,0,0];
    PERS loaddata load_ut3 := [0.033,[0,0,0],[1,0,0,0],0,0,0];
    PERS loaddata load_ot1 := [0.532185,[0,0,0],[1,0,0,0],0,0,0];
    PERS loaddata load_ot2 := [0.027,[0,0,0],[1,0,0,0],0,0,0];
    PERS loaddata load_ot3 := [0.032,[0,0,0],[1,0,0,0],0,0,0];
    PERS loaddata load_morobot := [0.90017,[0,0,0],[1,0,0,0],0,0,0];

    !###################################################################################
    ! Interruptroutine
    !###################################################################################
    VAR intnum sig1int_;
    VAR intnum sig2int_;
    VAR intnum sig3int_;
    VAR intnum sig4int_;
    PERS wobjdata wobj_kiste_1 := [FALSE,TRUE,"",[[-331.45,-238.696,184.427],[0.709811,0.00679859,0.0002493,0.704359]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_kiste_2 := [FALSE,TRUE,"",[[-329.304,79.0566,187.217],[0.708622,0.005673,-0.000814577,0.705565]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_kiste_3 := [FALSE,TRUE,"",[[-326.474,398.773,190.307],[0.706415,0.00585921,0.000793051,0.707773]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_kiste_4 := [FALSE,TRUE,"",[[89.613,395.483,188.078],[0.709347,0.00233215,-0.000848932,0.704854]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_kiste_5 := [FALSE,TRUE,"",[[503.775,-239.254,187.432],[0.706281,-0.00613429,-0.00100039,0.707905]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_kiste_6 := [FALSE,TRUE,"",[[503.054,74.4283,186.107],[0.709531,-0.000165703,-0.00300321,0.704668]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_kiste_7 := [FALSE,TRUE,"",[[501.845,395.59,187.567],[0.710108,0.00130832,-0.00170127,0.70409]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_kiste_8 := [FALSE,TRUE,"",[[0,0,0],[1,0,0,0]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_kiste_9 := [FALSE,TRUE,"",[[0,0,0],[1,0,0,0]],[[0,0,0],[1,0,0,0]]];
    PERS wobjdata wobj_TEST := [FALSE,TRUE,"",[[503.775,-239.254,187.432],[0.706281,-0.00613429,-0.00100039,0.707905]],[[0,0,0],[1,0,0,0]]];
    

    TRAP abort_program
        TPWrite "ABORT PROGRAM";
        Reset ox_program_running;
        StopMove;
        Reset ox_robot_moving;
        set ox_motion_stopped;
        ClearPath;
        deactivate_single_vacuum;
        deactivate_multi_vacuum;
        IDelete sig1int_;
        IDelete sig2int_;
        IDelete sig3int_;
        IDelete sig4int_;
        
        ExitCycle;
        !StorePath;
        !MoveJ pPreDesk,v300,z200,tool0\WObj:=wobj_colab;
        !RestoPath;
        !ResetPPMoved;
        !SystemStopAction \Stop;
        !Load \Dynamic, diskhome \File:="MainModule.MODX";
        
    ENDTRAP

    TRAP stop_motion
        TPWrite "pause motion";
        StopMove;
        Reset ox_robot_moving;
        set ox_motion_stopped;
    ENDTRAP

    TRAP restart_motion
        TPWrite "restart motion";
        StartMove;
        Reset ox_motion_stopped;
        set ox_robot_moving;
    ENDTRAP

    !TRAP set_speed
    ! #### Diese TRAP routine entspricht nicht mehr dem Original von Herrn Mölzer, hierfür BACKUP-RAPID code verwenden!
    !    VelSet AInput(ib_speed_ovrd), 100;
    !    TPWrite"set speed to = "\Num:=AInput(ib_speed_ovrd);
    !    SpeedRefresh AInput(ib_speed_ovrd);
    !    TPWrite"current speed = "\Num:=CSpeedOverride(\CTask);
    !    SetAO ob_speed_ovrd,CSpeedOverride(\CTask);
    !ENDTRAP
    
    TRAP cyclicSpeedRefresh
        IF ox_insWS1 = 1 AND ox_insWS2 = 0 AND ox_inrWS = 0 THEN !in WZ sWZ1 (aktiver gemeinsamer Arbeitsraum von Cobot und Mensch)
            !Geschwindigkeit entsprechend "Stress-Level"
            TPWrite"in sWZ1 set speed to = "\Num:=AInput(ib_speed_ovrd);
            SpeedRefresh AInput(ib_speed_ovrd);
            TPWrite"current speed = "\Num:=CSpeedOverride(\CTask);
            SetAO ob_speed_ovrd,CSpeedOverride(\CTask);
        ELSEIF ox_insWS1 = 0 AND ox_insWS2 = 1 AND ox_inrWS = 0 THEN !in WZ sWZ2 (passiver gemeinsamer Arbeitsraum von Cobot und Mensch)
            !80% speed
            TPWrite"in sWZ2 set speed to = 1";
            SpeedRefresh 1;
            TPWrite"current speed = "\Num:=CSpeedOverride(\CTask);
            SetAO ob_speed_ovrd,CSpeedOverride(\CTask);
        ELSEIF ox_insWS1 = 0 AND ox_insWS2 = 0 AND ox_inrWS = 1 THEN 
            !100% speed
            TPWrite"in rWZ set speed to = 100";
            SpeedRefresh 100;
            TPWrite"current speed = "\Num:=CSpeedOverride(\CTask);
            SetAO ob_speed_ovrd,CSpeedOverride(\CTask);
        ENDIF
    ENDTRAP
    
    !###################################################################################
    ! Power-ON Workscone-definition 
    !###################################################################################
    PROC my_power_on()
        VAR shapedata sWS1Volume;
        VAR shapedata sWS2Volume;
        VAR shapedata rWSVolume;
        
        CONST pos sWS1Low := [-589.00, -1000.00,  61.00];
        CONST pos sWS1Up := [319.00, -390.00, 901.00];
        
        CONST pos sWS2Low := [319.00, -1000.00, 61.00];
        CONST pos sWS2Up := [751.00, -390.00, 901.00];
        
        CONST pos rWSLow := [-589.00, -390.00, 61.00];
        CONST pos rWSUp := [751.00, 644.00, 901.00];
        
        !define boxes between corners
        WZBoxDef \Inside, sWS1Volume, sWS1Low, sWS1Up;
        WZBoxDef \Inside, sWS2Volume, sWS2Low, sWS2Up;
        WZBoxDef \Inside, rWSVolume, rWSLow, rWSUp;
        
        !define and enable supervision of boxes
        WZDOSet \Stat, rWS, \Inside, rWSVolume, ox_inrWS, 1;
        WZDOSet \Stat, sWS1, \Inside, sWS1Volume, ox_insWS1, 1;
        WZDOSet \Stat, sWS2, \Inside, sWS2Volume, ox_insWS2, 1;
    ENDPROC
    
    !###################################################################################
    ! Hauptprogramm
    !###################################################################################
    PROC main_morobot_assembly()
        VAR num pnCounter:=0;
        VAR num loop_cnt:=0;
        
        VAR num x_offs;
        VAR num y_offs;
        VAR num z_offs;
        
        CONNECT sig1int_ WITH abort_program;
        ISignalDI ix_abort_program,1,sig1int_;

        CONNECT sig2int_ WITH stop_motion;
        ISignalDI ix_pause_motion,1,sig2int_;

        CONNECT sig3int_ WITH restart_motion;
        ISignalDI ix_restart_motion,1,sig3int_;

        ! ### Bei Verwendung der TRAP-routine, folgende 2 Zeilen und TRAP-routine "set_speed" auskommentieren
        !CONNECT sig4int_ WITH set_speed;
        !ISignalDI ix_set_speed,1,sig4int_;
        
        CONNECT timeint WITH cyclicSpeedRefresh;        
        ITimer 0.5, timeint;
        
        !calcNewPositions;
        TPErase;

        
        n_program_selection:=20;
		WaitDI ix_start_program, 0;
		SetAO ob_status, 10;
		Reset ox_ack_start_program;
        IF DInput(ix_abort_program)=1 THEN
            TPWrite "abort";
        ENDIF
		WaitDI ix_start_program, 1;
        
        AccSet 1, 1;

        ! Loop for state machine
        WHILE TRUE DO            
            ! STATE MACHINE
            IF n_program_selection=20 THEN   ! IDLE STEP - Wait for program start
                SetAO ob_status,20;
                WaitDI ix_start_program,1;                  ! wait for start bit
                n_program_selection:=AInput(ib_prog_num);   ! read the program number
                IF ib_Gehause_Position=1 THEN
                    x_offs:=0;
                    y_offs:=0;
                elseIF ib_Gehause_Position=2 THEN
                    x_offs:=130;
                    y_offs:=0;
                elseIF ib_Gehause_Position=3 THEN
                    x_offs:=0;
                    y_offs:=170;
                elseIF ib_Gehause_Position=4 THEN
                    x_offs:=130;
                    y_offs:=170;
                ELSE
                    TPWrite "Wrong value";
                ENDIF
                set ox_ack_start_program;                   ! set ack bit
                
                set ox_program_running;
                !user_ack;
                
            ELSEIF n_program_selection>=120 and n_program_selection<=129 THEN
                SetAO ob_status, 120;
                IF n_program_selection=125 THEN
                    IF AInput(ib_greifer_typ)=1 THEN
                        n_program_selection:=123;
                    ELSEIF AInput(ib_greifer_typ)=2 THEN
                        n_program_selection:=121;
                    ENDIF
                ENDIF

                IF n_program_selection=121 and AInput(ib_greifer_typ)=2 THEN
                    place_vacuum_gripper;
                ELSEIF n_program_selection=122 and AInput(ib_greifer_typ)=0 THEN
                    pick_vacuum_gripper;
                ELSEIF n_program_selection=122 and AInput(ib_greifer_typ)=1 THEN
                    place_box_gripper;
                    pick_vacuum_gripper;
                ELSEIF n_program_selection=123 and AInput(ib_greifer_typ)=1 THEN
                    place_box_gripper;
                ELSEIF n_program_selection=124 and AInput(ib_greifer_typ)=0 THEN
                    pick_box_gripper;
                ELSEIF n_program_selection=124 and AInput(ib_greifer_typ)=2 THEN
                    place_vacuum_gripper;
                    pick_box_gripper;
                ENDIF
                handshake;
                
            ELSEIF n_program_selection=30 THEN  ! pick and place axis A1
                SetAO ob_status, 30;
                z_offs:=(2-ib_Offset_Number)*16;
                pnp_housing \xOT:=false \xUT:=true \iAxis:=1 \kiste:=ib_Magazin_Position \x_offset:=x_offs \y_offset:=y_offs \z_offset:=-z_offs;
                handshake;
            ELSEIF n_program_selection=31 THEN  ! pick and place axis A2
                SetAO ob_status, 31;
                z_offs:=(2-ib_Offset_Number)*16;
                pnp_housing \xOT:=false \xUT:=true \iAxis:=2 \kiste:=ib_Magazin_Position \x_offset:=x_offs \y_offset:=y_offs \z_offset:=-z_offs;
                handshake;
            ELSEIF n_program_selection=32 THEN  ! pick and place axis A3
                SetAO ob_status, 32;
                z_offs:=(2-ib_Offset_Number)*16;
                pnp_housing \xOT:=false \xUT:=true \iAxis:=3 \kiste:=ib_Magazin_Position \x_offset:=x_offs \y_offset:=y_offs \z_offset:=-z_offs;
                handshake;
            
            elseif n_program_selection=40 THEN  ! align servo
                SetAO ob_status, 40;
                Servo;
                handshake;
            elseif n_program_selection=41 THEN  ! align servo
                SetAO ob_status, 41;
                Servo;
                handshake;
            elseif n_program_selection=42 THEN  ! align servo
                SetAO ob_status, 42;
                Servo;
                handshake;
                
            ELSEIF n_program_selection=50 THEN  ! pick and place axis B1
                SetAO ob_status, 50;
                z_offs:=(2-ib_Offset_Number)*20.9;
                pnp_housing \xOT:=true \xUT:=false \iAxis:=1 \kiste:=ib_Magazin_Position \x_offset:=x_offs \y_offset:=y_offs \z_offset:=-z_offs;
                handshake;
            ELSEIF n_program_selection=51 THEN  ! pick and place axis B2
                SetAO ob_status, 51;
                z_offs:=(2-ib_Offset_Number)*20.9;
                pnp_housing \xOT:=true \xUT:=false \iAxis:=2 \kiste:=ib_Magazin_Position \x_offset:=x_offs \y_offset:=y_offs \z_offset:=-z_offs;
                handshake;
            ELSEIF n_program_selection=52 THEN  ! pick and place axis B3
                SetAO ob_status, 52;
                z_offs:=(2-ib_Offset_Number)*20.9;
                pnp_housing \xOT:=true \xUT:=false \iAxis:=3 \kiste:=ib_Magazin_Position \x_offset:=x_offs \y_offset:=y_offs \z_offset:=-z_offs;
                handshake;
            
            ELSEIF n_program_selection=60 THEN  ! move morobot from slot 1 to slot 2
                SetAO ob_status, 60;
                move_robot_to_pos_2;
                handshake;
            ELSEIF n_program_selection=61 THEN  ! reorient morobot in slot 2
                SetAO ob_status, 61;
                move_robot_to_pos_3;
                handshake;
            ELSEIF n_program_selection=62 THEN  ! move morobot from slot 2 to slot 3
                SetAO ob_status, 62;
                move_robot_to_pos_4;
                handshake;
            ELSEIF n_program_selection=63 THEN  ! reorient morobot in slot 3
                SetAO ob_status, 63;
                move_robot_to_pos_5;
                handshake;
            ELSEIF n_program_selection=64 THEN  ! move morobot from slot 3 to box
                SetAO ob_status, 64;
                move_robot_to_box;
                handshake;
                
            ELSEIF n_program_selection=70 THEN  ! move morobot from slot 3 to slot 2
                SetAO ob_status, 70;
                move_robot_from_pos_4_to_3;
                handshake;
           ELSEIF n_program_selection=71 THEN  ! reorient morobot in slot 2
                SetAO ob_status, 71;
                move_robot_from_pos_3_to_2;
                handshake;
           ELSEIF n_program_selection=72 THEN  ! move morobot from slot 2 to slot 1
                SetAO ob_status, 72;
                move_robot_from_pos_2_to_1;
                handshake;
           
            ELSEIF n_program_selection=220 THEN ! move servobox from magazin to table
                getServoBox;
            ELSEIF n_program_selection=230 THEN

            ELSEIF n_program_selection=240 THEN ! move servobox from table to magazin
                removeServoBox;
            ELSEIF n_program_selection=250 THEN

            ELSE
                TPWrite "Wrong number";
            ENDIF

        ENDWHILE
    ENDPROC

    !###################################################################################
    ! Unterprogramme
    !###################################################################################

    PROC handshake()
	    
		SetAO ob_status, 255;
		!WaitAI ib_prog_num, 99;
		WaitDI ix_start_program,0;
		reset ox_ack_start_program;
        n_program_selection:=20;
    ENDPROC
    
    ! Roboter in Position 2 einlegen
    PROC move_robot_to_pos_2()

        Movej offs(pFT1_Tisch,0,100,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT1_Tisch,0,0,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT1_Tisch,0,0,10),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT1_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        activate_multi_vacuum;
        MoveL offs(pFT1_Tisch,0,0,10),v20,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT1_Tisch,0,0,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;

        Movel offs(pFT2_Tisch,0,0,250),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT2_Tisch,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        deactivate_multi_vacuum;
        MoveL offs(pFT2_Tisch,0,20,0),v20,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,-300,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
    ENDPROC
    
    ! Roboter von Position 2 in 1 einlegen
    PROC move_robot_from_pos_2_to_1()
        MoveL offs(pFT2_Tisch,-300,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,20,0),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        activate_multi_vacuum;
        Movel offs(pFT2_Tisch,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT2_Tisch,0,0,250),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT1_Tisch,0,0,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT1_Tisch,0,0,10),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT1_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        deactivate_multi_vacuum;
        MoveL offs(pFT1_Tisch,0,0,10),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT1_Tisch,0,0,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        Movej offs(pFT1_Tisch,0,100,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;   
        MoveJ pPreDesk,v300,z200,tool0\WObj:=wobj_colab;
    ENDPROC

    ! Roboter in Position 3 einlegen
    PROC move_robot_to_pos_3()
        
        Movej offs(pFT2_Tisch,-300,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT2_Tisch,0,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,20,0),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        activate_multi_vacuum;
        MoveL offs(pFT2_Tisch,0,0,35),v20,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,0,250),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveAbsJ [[-77.8837,39.1412,-15.7825,-85.9471,100.618,45.1965],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;

        MoveL offs(pFT3_Tisch,0,0,250),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        deactivate_multi_vacuum;
        MoveL offs(pFT3_Tisch,0,20,0),v20,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,-300,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
    ENDPROC
    
    ! Roboter von Position 3 in 2 einlegen
    PROC move_robot_from_pos_3_to_2()
        MoveL offs(pFT3_Tisch,-300,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,20,0),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        activate_multi_vacuum;
        MoveL offs(pFT3_Tisch,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,250),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveAbsJ [[-77.8837,39.1412,-15.7825,-85.9471,100.618,45.1965],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,0,250),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT2_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        deactivate_multi_vacuum;
        MoveL offs(pFT2_Tisch,0,20,0),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT2_Tisch,0,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        Movej offs(pFT2_Tisch,-300,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
    ENDPROC
    
    ! Roboter in Position 4 einlegen
    PROC move_robot_to_pos_4()
        
        Movej offs(pFT3_Tisch,-300,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT3_Tisch,0,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,20,0),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        activate_multi_vacuum;
        MoveL offs(pFT3_Tisch,0,0,35),v20,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        !MoveAbsJ [[-77.8837,39.1412,-15.7825,-85.9471,100.618,45.1965],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;

        MoveL offs(pFT4_Tisch,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        deactivate_multi_vacuum;
        MoveL offs(pFT4_Tisch,-60,0,0),v20,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,-60,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        
    ENDPROC
    
    ! Roboter von Position 4 in Position 3 einlegen
    PROC move_robot_from_pos_4_to_3()
        MoveJ pPreDesk,v300,z200,tool0\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,-60,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,-60,0,0),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        activate_multi_vacuum;
        MoveL offs(pFT4_Tisch,0,0,35),v20,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveAbsJ [[-77.0655,36.1288,-24.1066,-88.0325,101.9,-55.2686],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT3_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        deactivate_multi_vacuum;
        MoveL offs(pFT3_Tisch,0,20,0),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT3_Tisch,0,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        Movej offs(pFT3_Tisch,-300,20,150),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
    ENDPROC
    
    ! Roboter in Position 5 einlegen
    PROC move_robot_to_pos_5()
        
        Movej offs(pFT4_Tisch,-60,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT4_Tisch,-60,0,0),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        activate_multi_vacuum;
        MoveL offs(pFT4_Tisch,0,0,35),v20,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT4_Tisch,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
		MoveJ [[276.64,122.32,547.10],[0.099431,-0.695311,0.107117,-0.703692],[-2,1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]], v200, z50, tool_vacuum_gripper\WObj:=wobj_colab;
        !MoveAbsJ [[-77.8837,39.1412,-15.7825,-85.9471,100.618,45.1965],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;

        MoveL offs(pFT5_Tisch,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT5_Tisch,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT5_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        deactivate_multi_vacuum;
        MoveL offs(pFT5_Tisch,-60,0,0),v20,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT5_Tisch,-60,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        
    ENDPROC
    
    ! Roboter in Box einlegen
    PROC move_robot_to_box()
        Movej offs(pFT5_Tisch,-60,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        Movel offs(pFT5_Tisch,-60,0,0),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT5_Tisch,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        activate_multi_vacuum;
        MoveL offs(pFT5_Tisch,0,0,35),v20,z10,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL offs(pFT5_Tisch,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_colab;
        
        MoveL offs(pFT_Kiste,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_kiste_1;
        MoveL offs(pFT_Kiste,0,0,35),v200,z10,tool_vacuum_gripper\WObj:=wobj_kiste_1;
        MoveL offs(pFT_Kiste,0,0,0),v20,fine,tool_vacuum_gripper\WObj:=wobj_kiste_1;
        deactivate_multi_vacuum;
        MoveL offs(pFT_Kiste,0,0,350),v200,z50,tool_vacuum_gripper\WObj:=wobj_kiste_1;
        
    ENDPROC



    PROC user_ack()
        TPWrite "Waiting for user ack";
        setdo ox_request_user_ack,1;
        WaitDI ix_user_ack,1;
        setdo ox_request_user_ack,0;
        WaitDI ix_user_ack,0;
    ENDPROC
    
   

    ! Gehaeuse holen und einlegen
    PROC pnp_housing(\bool xOT, \bool xUT, \num iAxis, \num kiste, \num x_offset, \num y_offset, \num z_offset)
       
         IF kiste=1 THEN
            wobj_TEST := wobj_kiste_1;
        ELSEIF kiste=2 THEN
            wobj_TEST := wobj_kiste_2;
            !TPWrite "wobjk2 uframe pos = " \Pos:=wobj_kiste_2.uframe.trans;
            !TPWrite "wobjk2 uframe rot = " \Orient:=wobj_kiste_2.uframe.rot;
            !TPWrite "wobjk2 oframe pos = " \Pos:=wobj_kiste_2.oframe.trans;
            !TPWrite "wobjk2 oframe rot = " \Orient:=wobj_kiste_2.oframe.rot;
            
        ELSEIF kiste=3 THEN
            wobj_TEST := wobj_kiste_3;
        ELSEIF kiste=4 THEN
            wobj_TEST := wobj_kiste_4;
        ELSEIF kiste=5 THEN
            wobj_TEST := wobj_kiste_5;
        ELSEIF kiste=6 THEN
            wobj_TEST := wobj_kiste_6;
        ELSEIF kiste=7 THEN
            wobj_TEST := wobj_kiste_7;
        ELSE
            TPWrite "WRONG WOBJ";
            user_ack;
        ENDIF
        !TPWrite "wobj_temp uframe pos = " \Pos:=wobj_TEST.uframe.trans;
        !TPWrite "wobj_temp uframe rot = " \Orient:=wobj_TEST.uframe.rot;
        !TPWrite "wobj_temp oframe pos = " \Pos:=wobj_TEST.oframe.trans;
        !TPWrite "wobj_temp oframe rot = " \Orient:=wobj_TEST.oframe.rot;
        
        MoveJ pPreDesk,v300,z200,tool0\WObj:=wobj_colab;
        MoveJ pPre1_UT1,v500,z200,tool0\WObj:=wobj_colab;
        
        IF xUT AND xOT=FALSE then
            pick_housing \iAxis:=iAxis \z_offset:=z_offset \x_offset:=x_offset \y_offset:=y_offset \kiste:=kiste \xOT:=xOT \xUT:=xUT;
        ELSEIF xOT AND xUT=FALSE THEN
            pick_housing \iAxis:=iAxis \z_offset:=z_offset \x_offset:=x_offset \y_offset:=-y_offset \kiste:=kiste \xOT:=xOT \xUT:=xUT;
        endif
        
        MoveJ pPre1_UT1,v500,z200,tool0\WObj:=wobj_colab;
        MoveJ pPreDesk,v300,z200,tool0\WObj:=wobj_colab;
        
        !user_ack;
        place_housing \iAxis:=iAxis \posTemp:=pUT1_Tisch \z_offset:=0 \xOT:=xOT \xUT:=xUT;
        MoveJ pPreDesk,v300,z200,tool0\WObj:=wobj_colab;
    ENDPROC

   

    ! Gehaeuse aufnehmen
    PROC pick_housing(\num iAxis,\num z_offset,\num x_offset,\num y_offset, \num kiste,  \bool xOT, \bool xUT)
        VAR robtarget posTemp;
        !TPWrite "axis = " \Num:=iAxis;
        !TPWrite "xoff = " \Num:=x_offset;
        !TPWrite "yoff = " \Num:=y_offset;
        !TPWrite "zoffs = " \Num:=z_offset;
        !TPWrite "kiste = " \Num:=kiste;
        !TPWrite "ut = " \Bool:=xUT;
        !TPWrite "ot = " \Bool:=xOT;
        
        IF xUT and xOT=FALSE then
            IF iAxis=1 THEN
                posTemp:=pUT1_wobj;
            ELSEIF iAxis=2 THEN
                posTemp:=pUT2_wobj;
            ELSEIF iAxis=3 THEN
                posTemp:=pUT3_wobj;
            ELSE
                TPWrite "Wrong Axis";
            ENDIF
        ELSEIF xOT and xUT=false THEN
            IF iAxis=1 THEN
                posTemp:=pOT1_wobj;
            ELSEIF iAxis=2 THEN
                posTemp:=pOT2_wobj;
            ELSEIF iAxis=3 THEN
                posTemp:=pOT3_wobj;
            ELSE
                TPWrite "Wrong Axis";
            ENDIF
        endif
        !TPWrite "pos = " \Pos:=posTemp.trans;        
        !TPWrite "rot = " \Orient:=posTemp.rot;     
        !TPWrite "pos = " \Pos:=wobj_TEST.uframe.trans;
        !TPWrite "rot = " \Orient:=wobj_TEST.uframe.rot;
        
        Movej offs(posTemp,x_offset,y_offset,100+z_offset),v500,z20,tool_single_vacuum \WObj:=wobj_TEST;
        MoveL offs(posTemp,x_offset,y_offset,15+z_offset),v500,z5,tool_single_vacuum\WObj:=wobj_TEST;
        MoveL offs(posTemp,x_offset,y_offset,0+z_offset),v10,fine,tool_single_vacuum\WObj:=wobj_TEST;
        activate_single_vacuum;
        MoveL offs(posTemp,x_offset,y_offset,30+z_offset),v10,z5,tool_single_vacuum\WObj:=wobj_TEST;
        MoveL offs(posTemp,x_offset,y_offset,100+z_offset),v500,z20,tool_single_vacuum\WObj:=wobj_TEST;
    ENDPROC

    ! Gehaeuse ablegen
    PROC place_housing(\num iAxis, \robtarget posTemp,\num z_offset, \bool xOT, \bool xUT)
        IF xUT and xOT=FALSE then
            IF iAxis=1 THEN
                posTemp:=pUT1_Tisch;
            ELSEIF iAxis=2 THEN
                posTemp:=pUT2_Tisch;
            ELSEIF iAxis=3 THEN
                posTemp:=pUT3_Tisch;
            ELSE
                TPWrite "Wrong Axis";
            ENDIF
        ELSEIF xOT and xUT=false THEN
            IF iAxis=1 THEN
                posTemp:=pOT1_Tisch;
            ELSEIF iAxis=2 THEN
                posTemp:=pOT2_Tisch;
            ELSEIF iAxis=3 THEN
                posTemp:=pOT3_Tisch;
            ELSE
                TPWrite "Wrong Axis";
            ENDIF
        endif
        Movej offs(posTemp,0,200,100+z_offset),v200,z20,tool_single_vacuum\WObj:=wobj_colab;
        Movej offs(posTemp,0,0,100+z_offset),v200,z20,tool_single_vacuum\WObj:=wobj_colab;
        MoveL offs(posTemp,0,0,35+z_offset),v200,z5,tool_single_vacuum\WObj:=wobj_colab;
        MoveL offs(posTemp,0,0,5+z_offset),v50,z5,tool_single_vacuum\WObj:=wobj_colab;
        MoveL offs(posTemp,0,0,0+z_offset),v10,fine,tool_single_vacuum\WObj:=wobj_colab;
        deactivate_single_vacuum;
        MoveL offs(posTemp,0,0,100+z_offset),v200,z20,tool_single_vacuum\WObj:=wobj_colab;
        MoveL offs(posTemp,0,200,100+z_offset),v200,z20,tool_single_vacuum\WObj:=wobj_colab;
    endproc

    ! Servo ausrichten
    PROC Servo()
        !user_ack;
        Movej offs(pServoAusrichtung_0_grad,0,0,150),v200,z100,tool_servo_rotator\WObj:=wobj_colab;
        MoveL offs(pServoAusrichtung_0_grad,0,0,10),v200,z10,tool_servo_rotator\WObj:=wobj_colab;
        MoveL offs(pServoAusrichtung_0_grad,0,0,-10),v20,fine,tool_servo_rotator\WObj:=wobj_colab;
        VelSet 10,10;
        MoveL offs(pServoAusrichtung_90_grad,0,0,-10),v5,fine,tool_servo_rotator\WObj:=wobj_colab;
        VelSet 100,300;
        MoveL offs(pServoAusrichtung_90_grad,0,0,20),v50,z20,tool_servo_rotator\WObj:=wobj_colab;
        Movej offs(pServoAusrichtung_90_grad,0,0,150),v200,z100,tool_servo_rotator\WObj:=wobj_colab;
    ENDPROC

    ! Greiferverriegeln
    PROC gripper_lock()
        WaitTime(0.5);
        reset ox_single_vac_greifer_oeffnen;
        reset ox_multi_vac_greifer_schliessen;
        set ox_kupplung_schliessen;
        reset ox_kupplung_oeffnen;
        WaitTime(0.5);
    ENDPROC

    ! Greifer entriegeln
    PROC gripper_unlock()
        WaitTime(0.5);
        reset ox_single_vac_greifer_oeffnen;
        reset ox_multi_vac_greifer_schliessen;
        WaitTime(0.5);
        set ox_kupplung_oeffnen;
        reset ox_kupplung_schliessen;
        WaitTime(0.5);
    ENDPROC

    ! Boxgreifer aufnehmen
    PROC pick_box_gripper()
        MoveAbsJ [[93.2885,3.48128,10.9841,5.23713,-7.20273,98.5318],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z20,tool_box_gripper\WObj:=wobj_colab;
        MoveAbsJ [[91.5021,7.96955,13.5233,-92.373,5.69663,21.2911],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z20,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pAblage_KistenGreifer,40,0,300),v200,z20,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_KistenGreifer,40,0,0),v200,z20,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_KistenGreifer,0,0,0),v20,fine,tool0\WObj:=wobj_colab;
        gripper_lock;
        set_gripper_typ\typ:=1;
        MoveL offs(pAblage_KistenGreifer,0,0,20),v20,fine,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_KistenGreifer,0,0,300),v200,z20,tool0\WObj:=wobj_colab;
        MoveAbsJ [[91.5021,7.96955,13.5233,-92.373,5.69663,21.2911],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z20,tool_box_gripper\WObj:=wobj_colab;
        MoveAbsJ [[93.2885,3.48128,10.9841,5.23713,-7.20273,98.5318],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z20,tool_box_gripper\WObj:=wobj_colab;
    ENDPROC

    PROC set_gripper_typ(\num typ)
        !VAR num in_typ;
        !VAR num out_typ;
        !in_typ:=AInput(ib_greifer_typ);
        !out_typ:=AOutput(ob_greifer_typ);
        SetAO ob_greifer_typ,typ;
        set ox_greifer_typ_setzen;
        WHILE AInput(ib_greifer_typ)<>AOutput(ob_greifer_typ) DO

            WaitTime 0.01;
        ENDWHILE
        reset ox_greifer_typ_setzen;
    ENDPROC

    ! Boxgreifer ablegen
    PROC place_box_gripper()
        MoveJ pPreDesk,v300,z200,tool0\WObj:=wobj_colab;
        MoveAbsJ [[93.2885,3.48128,10.9841,5.23713,-7.20273,98.5318],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z20,tool_box_gripper\WObj:=wobj_colab;
        MoveAbsJ [[91.5021,7.96955,13.5233,-92.373,5.69663,21.2911],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z20,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pAblage_KistenGreifer,0,0,300),v200,fine,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_KistenGreifer,0,0,20),v200,fine,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_KistenGreifer,0,0,0),v20,fine,tool0\WObj:=wobj_colab;
        gripper_unlock;
        set_gripper_typ\typ:=0;
        MoveL offs(pAblage_KistenGreifer,40,0,0),v20,fine,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_KistenGreifer,40,0,300),v200,fine,tool0\WObj:=wobj_colab;
        MoveAbsJ [[91.5021,7.96955,13.5233,-92.373,5.69663,21.2911],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z20,tool_box_gripper\WObj:=wobj_colab;
        MoveAbsJ [[93.2885,3.48128,10.9841,5.23713,-7.20273,98.5318],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]\NoEOffs,v200,z20,tool_box_gripper\WObj:=wobj_colab;
    ENDPROC

    ! Vacuumgreifer aufnehmem
    PROC pick_vacuum_gripper()
        MoveL offs(pAblage_VacuumGreifer,0,0,300),v200,z20,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_VacuumGreifer,0,0,30),v200,z20,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_VacuumGreifer,0,0,0),v20,fine,tool0\WObj:=wobj_colab;
        gripper_lock;
        set_gripper_typ\typ:=2;

        MoveL offs(pAblage_VacuumGreifer,0,0,30),v20,fine,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_VacuumGreifer,0,0,300),v200,z20,tool0\WObj:=wobj_colab;
    ENDPROC

    ! Vacuumgreifer ablegen
    PROC place_vacuum_gripper()
        MoveJ pPreDesk,v300,z200,tool0\WObj:=wobj_colab;
        MoveJ offs(pAblage_VacuumGreifer,0,0,500),v200,z20,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_VacuumGreifer,0,0,30),v200,z20,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_VacuumGreifer,0,0,0),v20,fine,tool0\WObj:=wobj_colab;
        gripper_unlock;
        set_gripper_typ\typ:=0;
        MoveL offs(pAblage_VacuumGreifer,0,0,30),v20,fine,tool0\WObj:=wobj_colab;
        MoveL offs(pAblage_VacuumGreifer,0,0,500),v200,z20,tool0\WObj:=wobj_colab;
    ENDPROC

    ! Kiste aufnehmen
    PROC pick_box(\robtarget pTempPosistion,\num z_offset,\bool magazin)
        !pick box
        openBoxGripper;
        MoveL Offs(pTempPosistion,-5,0,z_offset),v400,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL Offs(pTempPosistion,-5,0,30),v150,z10,tool_box_gripper\WObj:=wobj_colab;
        MoveL Offs(pTempPosistion,-5,0,0),v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pTempPosistion,v20,fine,tool_box_gripper\WObj:=wobj_colab;
        closeBoxGripper;
        MoveL Offs(pTempPosistion,0,0,z_offset),v150,fine,tool_box_gripper\WObj:=wobj_colab;
    ENDPROC

    ! Kiste ablegen
    PROC place_box(\robtarget pTempPosistion,\num z_offset,\bool magazin)
        !place box
        MoveL Offs(pTempPosistion,0,0,z_offset),v400,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL Offs(pTempPosistion,0,0,30),v150,z10,tool_box_gripper\WObj:=wobj_colab;
        MoveL pTempPosistion,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        closeBoxGripper;
        MoveL Offs(pTempPosistion,-5,0,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL Offs(pTempPosistion,-5,0,z_offset),v150,fine,tool_box_gripper\WObj:=wobj_colab;
    ENDPROC

    ! 
    PROC move_box()
        pick_box\pTempPosistion:=pKiste_Uebergabe_2\z_offset:=200\magazin:=true;
        place_box\pTempPosistion:=pKiste_Vorne_1\z_offset:=200\magazin:=true;
    ENDPROC

    ! Servobox von Magazin holenn und auf Tisch ablegen
    PROC getServoBox()
        MoveL offs(pKiste_Vorne_1,0,0,300),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,-5,0,200),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,-5,0,10),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,-5,0,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,0,0,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        closeBoxGripper;
        MoveL offs(pKiste_Uebergabe_1,0,0,10),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,0,0,200),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Vorne_1,0,0,300),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pPreDesk,v100,fine,tool0\WObj:=wobj_colab;
        user_ack;
        MoveL offs(pServoBox,0,0,160),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pServoBox,0,0,10),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pServoBox,0,0,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        openBoxGripper;
        MoveL offs(pServoBox,0,5,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pServoBox,0,5,10),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pServoBox,0,5,160),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pPreDesk,v100,fine,tool0\WObj:=wobj_colab;
        MoveL offs(pKiste_Vorne_1,0,0,300),v100,fine,tool_box_gripper\WObj:=wobj_colab;
    ENDPROC

    ! Servobox von Tisch holen und im Magazin ablegen
    PROC removeServoBox()
        MoveL offs(pKiste_Vorne_1,0,0,300),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pPreDesk,v100,fine,tool0\WObj:=wobj_colab;
        user_ack;
        MoveL offs(pServoBox,0,5,160),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pServoBox,0,5,10),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pServoBox,0,5,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pServoBox,0,0,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        closeBoxGripper;
        MoveL offs(pServoBox,0,0,10),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pServoBox,0,0,160),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pPreDesk,v100,fine,tool0\WObj:=wobj_colab;
        MoveL offs(pKiste_Vorne_1,0,0,300),v100,fine,tool_box_gripper\WObj:=wobj_colab;

        MoveL offs(pKiste_Uebergabe_1,0,0,200),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,0,0,10),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,0,0,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        openBoxGripper;
        MoveL offs(pKiste_Uebergabe_1,-5,0,0),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,-5,0,10),v20,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Uebergabe_1,-5,0,200),v100,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL offs(pKiste_Vorne_1,0,0,300),v100,fine,tool_box_gripper\WObj:=wobj_colab;

    ENDPROC

    ! Boxgreifer oeffnen
    PROC openBoxGripper()
        WaitTime(0.5);
        set ox_single_vac_greifer_oeffnen;
        reset ox_multi_vac_greifer_schliessen;
        WaitTime(0.5);
    ENDPROC

    ! Boxgreifer schliessen
    PROC closeBoxGripper()
        WaitTime(0.5);
        reset ox_single_vac_greifer_oeffnen;
        set ox_multi_vac_greifer_schliessen;
        WaitTime(0.5);
    ENDPROC

    ! Einzelvakuum einschalten
    PROC activate_single_vacuum()
        WaitTime(0.5);
        set ox_single_vac_greifer_oeffnen;
        reset ox_multi_vac_greifer_schliessen;
        WaitTime(0.5);
    ENDPROC

    ! Einzelvakuum ausschalten
    PROC deactivate_single_vacuum()
        WaitTime(0.5);
        reset ox_single_vac_greifer_oeffnen;
        reset ox_multi_vac_greifer_schliessen;
        WaitTime(0.5);
    ENDPROC

    ! Multivakuum einschalten
    PROC activate_multi_vacuum()
        WaitTime(0.5);
        reset ox_single_vac_greifer_oeffnen;
        set ox_multi_vac_greifer_schliessen;
        WaitTime(0.5);
    ENDPROC

    ! Multivakuum ausschalten
    PROC deactivate_multi_vacuum()
        WaitTime(0.5);
        reset ox_single_vac_greifer_oeffnen;
        reset ox_multi_vac_greifer_schliessen;
        WaitTime(0.5);
    ENDPROC


    !#############################################################################################
    ! Positionen zum Teachen
    !#############################################################################################
    PROC teach_box()
        MoveL pKiste_FT,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Hinten_1,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Hinten_2,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Hinten_3,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Vorne_1,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Vorne_2,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Vorne_3,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Uebergabe_1,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Uebergabe_2,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKiste_Uebergabe_3,v50,fine,tool_box_gripper\WObj:=wobj_colab;

        MoveL pServoBox,v50,fine,tool_box_gripper\WObj:=wobj_colab;
        MoveL pKLTBox,v50,fine,tool_box_gripper\WObj:=wobj_colab;

    ENDPROC

    PROC teach_pre_pos()
        MoveL pPreDesk,v100,fine,tool0\WObj:=wobj_colab;
    endproc

    PROC teach_gripper()
        MoveL pAblage_KistenGreifer,v50,fine,tool0\WObj:=wobj_colab;
        MoveL pAblage_VacuumGreifer,v50,fine,tool0\WObj:=wobj_colab;

    ENDPROC

    PROC teach_parts()
        movel pUT1_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pUT2_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pUT3_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT1_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT2_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT3_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pFT_Kiste,v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        MoveL pFT_Kiste10,v50,fine,tool_single_vacuum\WObj:=wobj_colab;

        movel pUT1_Tisch,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pUT2_Tisch,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pUT3_Tisch,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT1_Tisch,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT2_Tisch,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT3_Tisch,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pFT1_Tisch,v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        movel pFT2_Tisch,v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        movel pFT3_Tisch,v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        movel pFT4_Tisch,v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        movel pFT5_Tisch,v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;

        movel pServoAusrichtung_0_grad,v50,fine,tool_servo_rotator\WObj:=wobj_colab;
        movel pServoAusrichtung_90_grad,v50,fine,tool_servo_rotator\WObj:=wobj_colab;

        !################################################################
        !OFFSET
        !################################################################
        movel offs(pUT1_Kiste,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pUT2_Kiste,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pUT3_Kiste,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pOT1_Kiste,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pOT2_Kiste,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pOT3_Kiste,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pFT_Kiste,0,0,100),v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;

        movel offs(pUT1_Tisch,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pUT2_Tisch,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pUT3_Tisch,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pOT1_Tisch,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pOT2_Tisch,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pOT3_Tisch,0,0,100),v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel offs(pFT1_Tisch,0,0,100),v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        movel offs(pFT2_Tisch,0,0,100),v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        movel offs(pFT3_Tisch,0,0,100),v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        movel offs(pFT4_Tisch,0,0,100),v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        movel offs(pFT5_Tisch,0,0,100),v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;

        movel offs(pServoAusrichtung_0_grad,0,0,100),v50,fine,tool_servo_rotator\WObj:=wobj_colab;

    ENDPROC
    
    PROC teach_in_wobj()
        
        movel pUT1_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pUT2_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pUT3_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT1_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT2_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pOT3_Kiste,v50,fine,tool_single_vacuum\WObj:=wobj_colab;
        movel pFT_Kiste,v50,fine,tool_vacuum_gripper\WObj:=wobj_colab;
        
        
        MoveL pOT1_wobj,v50,fine,tool_single_vacuum\WObj:=wobj_kiste_7;
        MoveL pOT2_wobj,v50,fine,tool_single_vacuum\WObj:=wobj_kiste_7;
        MoveL pOT3_wobj,v50,fine,tool_single_vacuum\WObj:=wobj_kiste_7;
        MoveL pUT1_wobj,v50,fine,tool_single_vacuum\WObj:=wobj_kiste_7;
        MoveL pUT2_wobj,v50,fine,tool_single_vacuum\WObj:=wobj_kiste_7;
        MoveL pUT3_wobj,v50,fine,tool_single_vacuum\WObj:=wobj_kiste_7;
        MoveL pFT_wobj,v50,fine,tool_vacuum_gripper\WObj:=wobj_kiste_1;
    ENDPROC
    
    PROC calcNewPositions()
        pOT1_wobj := TranslateRobtarget(pOT1_Kiste,tool_single_vacuum,tool_single_vacuum,wobj_colab,wobj_kiste_5);
        pOT2_wobj := TranslateRobtarget(pOT2_Kiste,tool_single_vacuum,tool_single_vacuum,wobj_colab,wobj_kiste_6);
        pOT3_wobj := TranslateRobtarget(pOT3_Kiste,tool_single_vacuum,tool_single_vacuum,wobj_colab,wobj_kiste_7);
        pUT1_wobj := TranslateRobtarget(pUT1_Kiste,tool_single_vacuum,tool_single_vacuum,wobj_colab,wobj_kiste_2);
        pUT2_wobj := TranslateRobtarget(pUT2_Kiste,tool_single_vacuum,tool_single_vacuum,wobj_colab,wobj_kiste_3);
        pUT3_wobj := TranslateRobtarget(pUT3_Kiste,tool_single_vacuum,tool_single_vacuum,wobj_colab,wobj_kiste_4);
        
    ENDPROC
    
    FUNC robtarget TranslateRobtarget(robtarget rSource, PERS tooldata tSource, PERS tooldata tTarget, PERS wobjdata wSource, PERS wobjdata wTarget)
      var jointtarget jBuffer;
      var robtarget   rTarget;
    
      jBuffer := CalcJointT(rSource, tSource \wobj:=wSource);
      rTarget := CalcRobT(jBuffer, tTarget \wobj:=wTarget);
    
      RETURN rTarget;
    ENDFUNC
    
    

ENDMODULE