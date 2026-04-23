MODULE module_HOLOPATH (SYSMODULE)
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


    PERS jointtarget HP_startPos := [[0,0,0,0,0,0],[0,9E+09,9E+09,9E+09,9E+09,9E+09]];
    PERS robtarget holoPath_Target{10}:=[
        [[549.8, -7.9, 655.8],[-0.707, 0, -0.707, 0],[0, 0, 0, 0],[0,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[610, 39.6, 871.5],[-0.766, -0.053, -0.64, -0.018],[0, 0, 0, 0],[0,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[15.6, 547.8, 180.5],[0.357, -0.653, -0.59, -0.313],[0, 0, 0, 0],[0,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[573.6, 0, 689.9],[-0.707, 0, -0.707, 0],[0, 0, 0, 0],[0,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[0, 0, 0],[0, 1, 0, 0],[0, 0, 0, 1],[135,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[0, 0, 0],[0, 1, 0, 0],[0, 0, 0, 1],[135,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[0, 0, 0],[0, 1, 0, 0],[0, 0, 0, 1],[135,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[0, 0, 0],[0, 1, 0, 0],[0, 0, 0, 1],[135,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[0, 0, 0],[0, 1, 0, 0],[0, 0, 0, 1],[135,9E+09,9E+09,9E+09,9E+09,9E+09]],
        [[0, 0, 0],[0, 1, 0, 0],[0, 0, 0, 1],[135,9E+09,9E+09,9E+09,9E+09,9E+09]]
        ];
    PERS bool holoPath_TargetValid{10} := [
            TRUE, 
            TRUE, 
            TRUE,
            TRUE,
            TRUE,
            TRUE,
            TRUE,
            TRUE,
            TRUE,
            TRUE
        ];    
    PERS bool validationComplete := TRUE; 
    PERS bool simulationComplete := TRUE;     
        
    VAR speeddata holoPath_Speed := v100;
    PERS num holoPath_TargetNumber := 1; 
    PERS bool holoPathExecute := FALSE; 
    
    PROC holoPath()
        VAR num duration; ! in s
        VAR clock chrono; 
        
        MoveAbsJ HP_startPos, holoPath_Speed, fine, tool0\WObj:=wobj0;
        
        IF holoPathExecute THEN
            simulationComplete := FALSE; 
            validationComplete := FALSE; 
                 
            ClkReset chrono;
            ClkStart chrono;
            
            ! check if the targets are valid and start movement if so
            IF checkTarget() THEN
                validationComplete := TRUE; 
                ClkStop chrono; 
                duration := ClkRead(chrono); ! in s 
            
                !ConfJ\Off;
                IF holoPath_TargetNumber > 0 THEN 
                    FOR holoPath_i FROM 1 TO holoPath_TargetNumber DO 
                        IF holoPath_i = 1 THEN 
                            MoveJ holoPath_Target{1}, holoPath_Speed, fine, tool0\WObj:=wobj0;
                        ELSE
                            MoveJ holoPath_Target{holoPath_i}, holoPath_Speed, fine, tool0\WObj:=wobj0;
                        ENDIF
                    ENDFOR
                ENDIF
                WaitRob \InPos;
                simulationComplete := TRUE; 
                !ConfJ\On;
            ELSE 
                 simulationComplete := FALSE; 
                 validationComplete := TRUE; 
            ENDIF
            
        ENDIF
        holoPathExecute := FALSE; 
    ENDPROC
    
    ! checking if the targets from the MR-application are reachable in any configuration
    FUNC bool checkTarget()
        
        VAR robtarget rTar;
        VAR jointtarget jTar; 
        VAR errnum errValidation; 

        VAR num conf1; 
        
        ! reset validation
        FOR i FROM 1 TO holoPath_TargetNumber DO holoPath_TargetValid{i} := FALSE; ENDFOR
        FOR i FROM holoPath_TargetNumber+1 TO 10 DO holoPath_TargetValid{i} := TRUE; ENDFOR
        
        ! cf1 axis1: from -2 to 1
        ! cf4 axis4: from -2 to 1
        ! cf6 axis6: from -2 to 2
        ! cfx config: from 0 to 7
        
            IF holoPath_TargetNumber > 0 THEN 
                FOR checkTarget_i FROM 1 TO holoPath_TargetNumber DO 
                    rTar := holoPath_Target{checkTarget_i};
                    
                    FOR cf1 FROM -2 TO 1 DO 
                        rTar.robconf.cf1 := cf1;
                        
                        FOR cf4 FROM -2 TO 1 DO 
                            rTar.robconf.cf4 := cf4; 
                            
                            FOR cf6 FROM -2 TO 1 DO 
                                rTar.robconf.cf6 := cf6; 
                                
                                FOR cfx FROM 0 TO 7 DO 
                                    rTar.robconf.cfx := cfx; 
                                    
                                    jTar := CalcJointT(rTar, tool0 \ErrorNumber:=errValidation);

                                    IF NOT(errValidation = ERR_ROBLIMIT OR errValidation = ERR_OUTSIDE_REACH) THEN 
                                        holoPath_TargetValid{checkTarget_i} := TRUE; 
                                        GOTO tarValBreak; ! abort criteria 
                                    ENDIF
                                
                                ! END cfx
                                ENDFOR
                                
                                
                            ! END cf6    
                            ENDFOR 
                        
                        ! END cf4    
                        ENDFOR 
                    
                    ! END cf1    
                    ENDFOR
                    tarValBreak: 
                    
                ! END list of targets
                ENDFOR
                
            ENDIF

        FOR i FROM 1 TO 10 DO 
            IF holoPath_TargetValid{i} = FALSE THEN 
                RETURN FALSE; 
            ENDIF
        ENDFOR 
        
        RETURN TRUE;
        
    ENDFUNC
    
ENDMODULE