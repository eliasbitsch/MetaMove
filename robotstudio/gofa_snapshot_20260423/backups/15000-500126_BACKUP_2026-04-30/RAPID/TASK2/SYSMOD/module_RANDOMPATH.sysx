MODULE module_RANDOMPATH (SYSMODULE)
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
!       ---------------------------------   
!                  Extension   
!       ---------------------------------
!
!       Copyright (C)   2022 Alexander Korn
!       author:         Alexander Korn
!       email:          alexander-korn@gmx.at
!       year:           2022
!

    
    ! RANDOM PATH
    CONST jointtarget InitrandomPath  :=[[0,0,0,0,90,22.5],[0,0,0,0,0,0]];
    CONST robtarget randomPathHome:=[[550.00,-0.00,566.49],[2.11941E-06,1,-6.63772E-06,-8.6943E-06],[-1,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget randomPathTarget00:=[[670.89,537.57,421.22],[4.60072E-05,1,2.10888E-05,-5.64627E-05],[0,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget randomPathTarget10:=[[670.86,537.54,261.11],[9.14039E-05,1,8.80138E-06,-0.000132455],[0,0,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget randomPathTarget20:=[[432.21,-68.45,559.53],[0.705549,0.708661,8.86349E-05,-2.97752E-05],[0,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget randomPathTarget30:=[[432.19,-67.78,425.97],[0.705545,0.708665,0.000174384,-2.43819E-05],[0,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget randomPathTarget40:=[[199.98,-338.10,566.44],[3.40095E-05,-1,9.19609E-05,5.08987E-05],[-1,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget randomPathTarget50:=[[54.42,-819.87,569.53],[7.71121E-05,-0.699362,6.61144E-06,0.714767],[-1,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget randomPathTarget60:=[[49.61,-819.87,349.57],[8.40966E-05,-0.699358,-2.13306E-05,0.714772],[-1,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    CONST robtarget randomPathTarget70:=[[-39.40,-819.89,349.52],[0.00012433,-0.699359,-6.78646E-06,0.714771],[-1,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
     
    PERS num zone                       := 0; 
    VAR num zonePrev;
    VAR bool bZoneStartup               := TRUE; 
    VAR num changeSpeed                 := 100;
    
    PERS num vel                        := 1;
    VAR num velPrev;
    VAR speeddata randomPath_Speed      := v100;
    
     
    !
    !   RANDOM PATH
    !
    PROC randomPath()
        !VAR speeddata randomPath_Speed := v100;
        
        ! Startup procedure
        IF bZoneStartup THEN
            ZoneStartup;
            bZoneStartup := FALSE; 
        ENDIF
        
        
        ConfJ \On;
        ConfL \On;
        MoveJ randomPathHome, randomPath_Speed, fine, tool_vacuum_gripper\WObj:=wobj0;
        MoveJ randomPathTarget00, randomPath_Speed, z20, tool_vacuum_gripper\WObj:=wobj0;
        MoveL randomPathTarget10, randomPath_Speed, fine, tool_vacuum_gripper\WObj:=wobj0;
        MoveL randomPathTarget00, randomPath_Speed, z20, tool_vacuum_gripper\WObj:=wobj0;
        
        MoveJ randomPathTarget20, randomPath_Speed, z20, tool_vacuum_gripper\WObj:=wobj0;
        MoveL randomPathTarget30, randomPath_Speed, fine, tool_vacuum_gripper\WObj:=wobj0;
        MoveL randomPathTarget20, randomPath_Speed, z20, tool_vacuum_gripper\WObj:=wobj0;
        
        MoveJ randomPathTarget40, randomPath_Speed, z20, tool_vacuum_gripper\WObj:=wobj0;
        MoveJ randomPathTarget50, randomPath_Speed, z20, tool_vacuum_gripper\WObj:=wobj0;
        MoveL randomPathTarget60, randomPath_Speed, fine, tool_vacuum_gripper\WObj:=wobj0;
        MoveL randomPathTarget70, randomPath_Speed, fine, tool_vacuum_gripper\WObj:=wobj0;
		MoveL randomPathTarget60, randomPath_Speed, fine, tool_vacuum_gripper\WObj:=wobj0;
		MoveJ randomPathTarget50, randomPath_Speed, z20, tool_vacuum_gripper\WObj:=wobj0;
		MoveJ randomPathTarget40, randomPath_Speed, z20, tool_vacuum_gripper\WObj:=wobj0;
             
    ENDPROC
    
    
    !
    !   STARTUP
    !
    PROC ZoneStartup()
        zone := 0; 
        !zonePrevTerminate := -1; ! just any number to make a difference
        !modeChanged := TRUE;
        
        vel := 1;
        randomPath_Speed := v100;
                
        ! INIT TRAP
        DeleteZoneTrap;
        AddZoneTrap;
        
        DeleteVelTrap;
        AddVelTrap;
        
        MoveAbsJ InitrandomPath, v100, fine, tool_vacuum_gripper\WObj:=wobj0;
        
    ENDPROC
    
    
    
    !
    ! TRAP (called when mode is changed)
    !
    PROC DeleteZoneTrap()
        IDelete zonePrev;
    ENDPROC

    PROC AddZoneTrap()
        CONNECT zonePrev WITH zoneChangedTrap;
        IPers zone, zonePrev;
    ENDPROC

    TRAP zoneChangedTrap
        IF zone = 0 THEN 
            changeSpeed := 100;
            SpeedRefresh changeSpeed;
        ENDIF
        
        IF zone = 1 THEN 
            changeSpeed := 20;
            SpeedRefresh changeSpeed;
        ENDIF
        
        IF zone = 2 THEN 
            changeSpeed := 0;
            SpeedRefresh changeSpeed;
        ENDIF
        
    ENDTRAP
    
    !
    ! TRAP (called when mode is changed)
    !
    PROC DeleteVelTrap()
        IDelete velPrev;
    ENDPROC

    PROC AddVelTrap()
        CONNECT velPrev WITH velChangedTrap;
        IPers vel, velPrev;
    ENDPROC

    TRAP velChangedTrap
        IF vel = 1 THEN 
            randomPath_Speed := v100;
        ELSEIF vel = 2 THEN
            randomPath_Speed := v200;
        ELSEIF vel = 3 THEN
            randomPath_Speed := v300;
        ELSEIF vel = 4 THEN
            randomPath_Speed := v400;
        ELSEIF vel = 5 THEN
            randomPath_Speed := v500;
        ELSEIF vel = 6 THEN
            randomPath_Speed := v600;
        ENDIF
        
    ENDTRAP
    
    
ENDMODULE