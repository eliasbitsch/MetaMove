MODULE CommModule
    
    VAR byte sdk_wd_counter := 0;
    VAR byte prev_wd_counter := 0;
    VAR bool sdk_better_communictation := FALSE;
    
    VAR num millis := 0;
    
    VAR num start_time := 0;
    VAR num stop_time := 0;
    VAR num delta_time := 0;
    PERS bool wd_stop := TRUE;
    
    PROC comm()
        
        ! Livesign mirror
        IF DInput(ix_live_sign)=1 THEN
            set ox_live_sign;
        ELSE
            reset ox_live_sign;
        ENDIF
        
        ! Pneumatic shutdown
        IF EStopStatus=1 THEN
            Reset Scalable_IO_0_DO1;
        ELSE
            Set Scalable_IO_0_DO1;
        ENDIF
        
        
        ! TEST
        IF sdk_better_communictation=TRUE THEN
            set ox4_0;
        ELSE
            reset ox4_0;
        ENDIF        
        millis := (GetTime(\Min)*60 + GetTime(\Sec))*1000 + GetTime(\MSec);
        
        
        !Watchdog
        IF(sdk_wd_counter <> prev_wd_counter) THEN
            !start
            start_time := millis;
            stop_time := millis;
        ELSE
            !stop
            stop_time := millis;
        ENDIF
        delta_time := stop_time-start_time;
        IF (delta_time > 500) THEN
            wd_stop:=TRUE;
        ELSE
            wd_stop:=FALSE;
        ENDIF
        prev_wd_counter := sdk_wd_counter;
        
    
        WaitTime 0.1;
    ENDPROC
ENDMODULE