MODULE module_mr19m010_copy
    
!    VAR robtarget act_position:=[[100,200,300],[1,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
!    VAR robtarget delta_position:=[[100,200,300],[1,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
!    VAR robtarget last_delta_position:=[[100,200,300],[1,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
!    VAR robtarget new_position:=[[100,200,300],[1,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    
!    CONST robtarget pos_start:=[[55.53,-748.28,193.37],[0.0001245,-0.705503,0.708707,0.000958071],[-1,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    CONST robtarget pos_dest:=[[346.89,-865.63,178.82],[9.08197E-05,-0.705465,0.708744,0.000875866],[-1,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    
    
    
!    TASK PERS tooldata myTool := [TRUE,[[0,0,138],[0.19509,0,0,0.980785]],[0.465,[0,0,50],[1,0,0,0],0,0,0]];

!    VAR num toggle := 0;
    
!    VAR bool aux := FALSE;
!    VAR num linear_distance := 50;
!    VAR num angular_distance_rx := 5;
!    VAR num angular_distance_ry := 5;
!    VAR num angular_distance_rz := 90;
!    VAR bool aux_leadthrough := FALSE;
!    VAR num last_btn_lead_through := 0;
!    VAR num btn_lead_through := 0;
    
!    CONST speeddata v_default := [5, 3, 5, 3];
!    CONST speeddata v_rz := [5, 10, 5, 10];
!    VAR speeddata v_norm := v_default;
    
!    VAR num angle_rad := 0;
!    VAR num angle_deg_encoder :=0;
!    VAR num angle_deg_axis :=0;
!    VAR num angle_deg :=0;
    
!    VAR num dx:=0;
!    VAR num dy:=0;
!    VAR num dz:=0;
!    !
!    VAR num drx:=0;
!    VAR num dry:=0;
!    VAR num drz:=0;
    
!    VAR jointtarget joints;
    
!    VAR num last_angle_deg_encoder:=0;
!    VAR bool aux_angle_deg_encoder:=false;
!    TASK PERS loaddata teach_tool := [0.39,[0,0,-260],[1,0,0,0],0,0,0];
    
!    ! PC SDK TEST
!    VAR bool rapid_bool := TRUE;
!    VAR num rapid_num := 13;
    
    
    
!    PROC main_mr19m010_copy()   
        
!        !SpyStart "HOME:/spy.log";
!        WHILE TRUE DO
            
!            toggle := DInput(ix_live_sign);
!            btn_lead_through := DInput(ix3_4);
!            act_position := CRobT(\Tool:=myTool \WObj:=wobj0);
            
!            !delta_position.trans := act_position.trans - pos_dest.trans;
!            !IF abs(delta_position.trans.x-last_delta_position.trans.x) > 0.01 or abs(delta_position.trans.y-last_delta_position.trans.y) > 0.01 or abs(delta_position.trans.z-last_delta_position.trans.z) > 0.01 then 
!            !    TPWrite "dx = "+ValToStr(delta_position.trans.x)+" | dy = "+ValToStr(delta_position.trans.y)+" | dz = "+ValToStr(delta_position.trans.z);
!            !ENDIF
!            !last_delta_position.trans := delta_position.trans;
                        
            
!            IF btn_lead_through = 1 and aux_leadthrough = FALSE THEN
!                WaitTime 0.5;
!                SetLeadThrough \On;
!                aux_leadthrough:=TRUE;
                
!            ELSEIF btn_lead_through = 1 AND aux_leadthrough = TRUE THEN
!                SetLeadThrough \Off;
!                aux_leadthrough:=FALSE;
!            ENDIF
!            WaitDI ix3_4, 0;
            
            
!            angle_rad := AInput(id_angle)/1000/1000;
!            angle_deg_encoder := angle_rad*180/pi;
            
!            IF Abs(angle_deg_encoder-last_angle_deg_encoder) > 2 THEN
!                aux_angle_deg_encoder:=TRUE;
!            ENDIF
!            last_angle_deg_encoder:=angle_deg_encoder;
            
!            joints := CJointT();
            
!            angle_deg := angle_deg_encoder + (joints.robax.rax_6 - 22.5);
            
!            IF DInput(ix_move_y_n)=1 THEN
!                dx := linear_distance*cos(-angle_deg);
!                dy := linear_distance*sin(-angle_deg);
!                dz := 0;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_x_n)=1 THEN
!                dy := linear_distance*cos(angle_deg);
!                dx := linear_distance*sin(angle_deg);
!                dz := 0;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_y_p)=1 THEN
!                dx := -linear_distance*cos(-angle_deg);
!                dy := -linear_distance*sin(-angle_deg);
!                dz := 0;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_x_p)=1 THEN
!                dy := -linear_distance*cos(angle_deg);
!                dx := -linear_distance*sin(angle_deg);
!                dz := 0;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_z_p) = 1 THEN
!                dx := 0;
!                dy := 0;
!                dz := -linear_distance;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_z_n) = 1 THEN
!                dx := 0;
!                dy := 0;
!                dz := linear_distance;
!                v_norm:=v_default;
                
                
!            ELSEIF DInput(ix_move_ry_n)=1 THEN
!                drx := angular_distance_rx*cos(-angle_deg);
!                dry := angular_distance_ry*sin(-angle_deg);
!                drz := 0;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_rx_n)=1 THEN
!                dry := angular_distance_ry*cos(angle_deg);
!                drx := angular_distance_rx*sin(angle_deg);
!                drz := 0;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_ry_p)=1 THEN
!                drx := -angular_distance_rx*cos(-angle_deg);
!                dry := -angular_distance_ry*sin(-angle_deg);
!                drz := 0;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_rx_p)=1 THEN
!                dry := -angular_distance_ry*cos(angle_deg);
!                drx := -angular_distance_rx*sin(angle_deg);
!                drz := 0;
!                v_norm:=v_default;
!            ELSEIF DInput(ix_move_rz_p) = 1 THEN
!                drx := 0;
!                dry := 0;
!                drz := -angular_distance_rz;
!                v_norm:=v_rz;
!            ELSEIF DInput(ix_move_rz_n) = 1 THEN
!                drx := 0;
!                dry := 0;
!                drz := angular_distance_rz;
!                v_norm:=v_rz;
!            ENDIF
            
!            IF aux_leadthrough = FALSE THEN
!                IF (DInput(ix_move_x_p) = 1 or DInput(ix_move_x_n) = 1 or DInput(ix_move_y_p) = 1 or DInput(ix_move_y_n) = 1 or DInput(ix_move_z_p) = 1 or DInput(ix_move_z_n) = 1) THEN
!                    IF aux = FALSE THEN
!                        aux:=TRUE;
!                        StartMove;
!                        MoveL \Conc, RelTool(act_position, dx, dy, dz), v_norm, fine, myTool\WObj:=wobj0;
!                    ENDIF
                
!                ELSEIF (DInput(ix_move_rx_p) = 1 or DInput(ix_move_rx_n) = 1 or DInput(ix_move_ry_p) = 1 or DInput(ix_move_ry_n) = 1 or DInput(ix_move_rz_p) = 1 or DInput(ix_move_rz_n) = 1) THEN
!                    IF aux = FALSE then
!                        aux:=TRUE;
!                        StartMove;
!                        MoveL \Conc, RelTool(act_position, 0, 0, 0 \Rx:=drx \Ry:=dry \Rz:=drz), v_norm, fine, myTool\WObj:=wobj0;
!                    ENDIF
!                ELSE
!                    aux_angle_deg_encoder := FALSE;
!                    aux := false;        
!                    StopMove;
!                    ClearPath;
!                    !StopMoveReset;    
!                ENDIF                  
!            ENDIF
        
!        ENDWHILE
!        SpyStop;
!    ENDPROC
    
ENDMODULE