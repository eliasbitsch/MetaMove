MODULE module_EGM (SYSMODULE)
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

!  PERS tooldata tEGM:=[TRUE,[[0,0,0],[1,0,0,0]],[0.001,[0,0,0.001],[1,0,0,0],0,0,0]];
!    PERS tooldata tool_calib:=[TRUE,[[0.63997,1.78002,136.1],[1,0,0,0]],[0.491,[0,0,0],[1,0,0,0],0,0,0]];
! 22 degree
!    PERS tooldata tool_vacuum_gripper:=[TRUE,[[0,0,151.5],[0.190809,0,0,0.981627]],[0.707,[0,0,100],[1,0,0,0],0,0,0]];
! 22.5 degree
!    PERS tooldata tool_vacuum_gripper_goholo:=[TRUE,[[0,0,151.5],[0.1950903,0,0,0.9807853]],[0.707,[0,0,100],[1,0,0,0],0,0,0]];
!    PERS tooldata tool_servo_rotator:=[TRUE,[[-45.156,-109.017,96.5],[0.587938,0.392848,-0.587938,0.392848]],[0.707,[0,0,40],[1,0,0,0],0,0,0]];
!    PERS tooldata tool_single_vacuum:=[TRUE,[[74.05,178.77,88.5],[0.5879378,-0.3928475,0.5879378,0.3928475]],[0.707,[0,0,20],[1,0,0,0],0,0,0]];
!    PERS tooldata tool_box_gripper:=[TRUE,[[0,0,93.5],[0.190809,0,0,0.981627]],[1.6,[0,0,100],[1,0,0,0],0,0,0]];

!    VAR egmident egmID1;
!    VAR egmstate egmSt1;

!    VAR egmident egmID_stream;
!    VAR egmstate egmStStream;
    
!    ! CONST egm_minmax egm_minmax_joint1:=[-0.1,0.1];
!    ! Konvergenzkriterien AM EGM Seite 53
!    CONST egm_minmax egm_minmax_lin1:=[-2,2]; ! in mm 
!    CONST egm_minmax egm_minmax_rot1:=[-2,2]; ! in ° 
!    CONST pose posecor0:=[[0,0,0],[1,0,0,0]];
    
    
!    ! was machen die beiden?! 
!    CONST pose posecor:=posecor0;![[1200,400,900], [0,0,1,0]];
!    CONST pose posesens:=posecor0;![[12.3313,-0.108707,416.142],[0.903899,-0.00320735,0.427666,0.00765917]];


!! GoHolo Setting kann verwendet werden für Fehler - Application Manuale EGM Seite 40

!    PROC EGM_Init()
!     !   tEGM:=CTool();

!        EGMGetId egmID1;
!        egmSt1:=EGMGetState(egmID1);
!        IF egmSt1<=EGM_STATE_CONNECTED THEN
!            EGMSetupUC ROB_1, egmID1, "ROB_Michi", "UCdevice" \Pose;
!        ENDIF
    
!        EGMActPose egmID1\StreamStart\Tool:=tool_vacuum_gripper\WObj:=wobj0,posecor,EGM_FRAME_WOBJ,posecor,EGM_FRAME_WOBJ
!        \x:=egm_minmax_lin1\y:=egm_minmax_lin1\z:=egm_minmax_lin1\rx:=egm_minmax_rot1\ry:=egm_minmax_rot1\rz:=egm_minmax_rot1
!        \MaxPosDeviation:=1000\MaxSpeedDeviation:=200; ! was 2x100
        
!    ENDPROC
    
!    PROC EGM_RunPose()
        
!       ! SingArea \LockAxis4;
!       ConfJ \Off;
!       ConfL \Off; 
!        EGMRunPose egmID1,EGM_STOP_HOLD\x\y\z\Rx\Ry\Rz\RampInTime:=0.05;


!         ERROR 
!            IF ERRNO=ERR_ROBLIMIT THEN
!                TPWrite "Joint out of range error";
!                !EGMReset egmID1;
!            ELSEIF ERRNO=ERR_UDPUC_COMM THEN 

!                RETURN;
!            ELSE 
!                STOP;
!            ENDIF 
            
!    ENDPROC

!    PROC EGM_Reset()
!        EGMReset egmID_stream; 
!        EGMReset egmID1;
!    ENDPROC
    
!    PROC EGM_StreamStart()
!        EGMGetId egmID_stream;
!        egmStStream := EGMGetState(egmID_stream);
!        IF egmStStream<=EGM_STATE_CONNECTED THEN
!            EGMSetupUC ROB_1,egmID_stream,"default","UCstream"\Pose;
!            EGMStreamStart egmID_stream;
!        ENDIF
!    ENDPROC
    
!    PROC EGM_StreamStop()
!        EGMGetId egmID_stream;
!        egmStStream := EGMGetState(egmID_stream);
!        IF egmStStream<=EGM_STATE_CONNECTED THEN
!            EGMStreamStop egmID_stream;
!        ENDIF
!        EGMReset egmID_stream; 
!    ENDPROC
  
ENDMODULE