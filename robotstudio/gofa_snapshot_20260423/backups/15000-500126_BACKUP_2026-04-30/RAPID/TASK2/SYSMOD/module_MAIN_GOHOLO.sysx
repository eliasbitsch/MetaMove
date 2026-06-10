MODULE module_MAIN_GOHOLO (SYSMODULE)
!    
!       ---------------------------------   
!                   GoHolo    
!       ---------------------------------
!
!       Copyright (C)   2021 Jakob Hörbst
!       author:         Jakob Hörbst
!       email:          jakob@hoerbst.net
!       year:           2021
!


    ! INITIAL POSITIONS
    !CONST jointtarget pInitEGM          :=  [[0,0,25,0,-25,0], [0,0,0,0,0,0]];
    !CONST jointtarget pInitEGM          :=  [[0,0,0,0,90,22.5], [0,0,0,0,0,0]];
!    CONST jointtarget pInitEGM          :=  [[0,1.67,0.31,0,88.02,22.5], [0,0,0,0,0,0]];
!    CONST jointtarget pInitStartup      :=  [[0,0,0,0,0,0], [0,0,0,0,0,0]];   
!    CONST robtarget pHome               :=  [[-200,100,300],[0,1,0,0],[2,0,0,0],[135,9E+09,9E+09,9E+09,9E+09,9E+09]];
    
!    ! JOINT CONTROL
!    PERS jointtarget jointControlTarget :=  [[0,0,0,0,0,0],[0,0,0,0,0,0]];
!    VAR jointtarget jointControlTarDef  := [[0,0,0,0,0,0],[0,0,0,0,0,0]];
!    PERS bool actJointControlTarget     :=  FALSE; 

!    ! COMANDS
!    PERS num mode                       :=  4; 
!    VAR num modePrev;
!    VAR num modePrevTerminate           :=  0; 
!    VAR bool bStartup                   :=  TRUE; 
!    VAR bool modeChanged                :=  FALSE; 
    
!    ! TERMINATING TARGET FOR EGM
!    VAR robtarget tar; 
    
!    PERS bool startSimulation := FALSE;
!    CONST jointtarget sync_position := [[0,0,0,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]]; 

!    !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
!    !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!  MAIN  !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
!    !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
!    PROC mainGoHolo()
    
!        ! Startup procedure
!        IF bStartup THEN
!            Startup;
!            bStartup := FALSE; 
!        ENDIF
        
!        ! Check if mode changed (externally)
!        IF mode <> modePrevTerminate AND (mode >= 0 AND mode <= 5) THEN 
!            Modehandler;
!        ENDIF

!        ! RUN SELECTED MODE
!        TEST mode
!            CASE 0: ! DO NOTHING
!!                MoveAbsJ pInitStartup, v100, fine, tool0;
                
!            CASE 1: ! RANDOM PATH
!                EGM_StreamStart;
!                SingArea \Off;
!                randomPath;
                
!            CASE 2: ! EGM 
!                MoveAbsJ pInitEGM, v100, fine, tool_vacuum_gripper; 
!                EGM_Init;
!                DeleteTrap;
!                mode := 20; 
!            CASE 20:
!                EGM_RunPose;
                
!            CASE 3: ! JOINT CONTROL
!                IF actJointControlTarget THEN
!                    JointControl;
!                    actJointControlTarget := FALSE; 
!                ENDIF

!            CASE 4: ! HOLO PATHS
!                SingArea \Off;
!                !IF NOT RobOS() 
!                    holoPath;

!            CASE 5: ! WIZARD SIMULATION
!                mode := 50; 
!               ! STOP; 
!            CASE 50: 
!                IF startSimulation THEN 
!                    ConfJ \On;
!                    ConfL \On; 
!                    mainWizard;
!                    startSimulation := FALSE; 
!                    mode := 0; 
!                    ConfJ \Off;
!                    ConfL \Off;
                    
!                ENDIF 
!        ENDTEST

!    ENDPROC
    
!    !
!    !   STARTUP
!    !
!    PROC Startup()
!        mode := 0; 
!        modePrevTerminate := -1; ! just any number to make a difference
!        modeChanged := TRUE; 
!        !MotionSup \TuneValue:=50; ! default value
        
!        holoPathExecute := FALSE; 
!        actJointControlTarget := FALSE; 
        
!        jointControlTarget := jointControlTarDef;
        
!        EGM_StreamStart;
        
!        ! INIT TRAP
!        DeleteTrap;
!        AddTrap;
        
!        MoveAbsJ pInitStartup, v100, fine, tool0;
!    ENDPROC
    
!    !
!    !   ModeHanlder
!    !
!    PROC Modehandler()
!        ! If prev mode was EGM terminate EGM 
!        IF modePrevTerminate = 2 THEN 
!            SingArea \Off;
!            tar := CRobT(\Tool:=tool0, \WObj:=wobj0); 
!                 MoveJ tar, v100, fine, tool0;
!        ENDIF
        
!        ! stop and reset EGM 
!        EGM_StreamStop;
!        WaitTime 0.05;
!        EGM_Reset;
!        WaitTime 0.05;
        
!        IF mode <> 2 THEN 
!            EGM_StreamStart;
!            DeleteTrap;
!            AddTrap;
!        ENDIF

!        modePrevTerminate := mode; 
!    ENDPROC
 
!    !
!    !   JOINT CONTROL
!    !    
!    PROC JointControl()
!        MoveAbsJ jointControlTarget, v200, z0, tool0;
!    ENDPROC
    
!    !
!    ! TRAP (called when mode is changed)
!    !
!    PROC DeleteTrap()
!        IDelete modePrev;
!    ENDPROC

!    PROC AddTrap()
!        CONNECT modePrev WITH modeChangedTrap;
!        IPers mode, modePrev;
!    ENDPROC

!    TRAP modeChangedTrap
!        IF mode >= 0 AND mode <= 5 THEN 
!            !modeChanged := TRUE;
!            ExitCycle;
!        ENDIF
!    ENDTRAP


ENDMODULE