MODULE module_mr22m012
    
    !**************************************************************************************************************************************************
    ! Description:
    ! 	This is an example procedure.
    ! 	It is not very useful but shows how to use parameters.
    !
    !Parameters:
    !	\switch doThis|doThat	-	Pair of optional mutual exclusive switches that controls if "this" or "that" should be done by this procedure.
    !
    !	INOUT numRepeats		-	The number of repetitions wanted. The actual number is returned.
    !
    !	num datalist{*}			-	A one dimensional array of num that describes some data.
    !
    !**************************************************************************************************************************************************
!    LOCAL CONST robtarget pos_1:=[[78.74,-908.12,340.00],[0.0122479,-0.32855,0.944271,-0.0160604],[-1,0,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pos_2:=[[83.13,-819.60,660.00],[0.00694297,0.343346,-0.939083,-0.0137378],[-1,-1,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pos_3 := [[533.85,-805.62,340.00],[0.0110369,0.561668,-0.826934,0.0242411],[-1,-1,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pos_4 := [[520.48,-660.07,660.00],[0.0203613,-0.591544,0.805987,-0.00684278],[-1,0,-2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
     
!    !###################################################################################
!    ! Tischpositionen
!    !###################################################################################
!    LOCAL CONST robtarget pUT1_Tisch:=[[519.86,117.61,93.95],[0.00724997,-0.700539,-0.713575,-0.00163116],[-2,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pUT2_Tisch:=[[429.17,127.86,99.08],[0.00721143,-0.712779,-0.70135,-0.00177402],[-1,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pUT3_Tisch:=[[358.81,116.67,97.20],[0.00720976,-0.70856,-0.705611,-0.00175584],[-2,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pOT1_Tisch:=[[528.70,118.89,119.77],[0.00726881,-0.696857,-0.717171,-0.00159584],[-1,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pOT2_Tisch:=[[440.77,142.77,122.36],[0.00728003,-0.696833,-0.717195,-0.00159892],[-1,-2,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pOT3_Tisch:=[[357.61,111.54,120.96],[0.00728625,-0.720939,-0.692959,-0.00138065],[-2,-1,0,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    
!    LOCAL CONST robtarget pServoAusrichtung_0_grad:=[[537.99,397.57,52.64],[1.52037E-05,0.707497,-0.706717,-1.77496E-05],[-1,-1,2,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pServoAusrichtung_90_grad:=[[537.94,397.46,63.54],[1.36248E-05,1,-0.00014261,-5.07474E-05],[-1,-2,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];

!    LOCAL CONST robtarget pFT1_Tisch:=[[437.45,122.66,93.36],[2.7306E-06,0.703962,-0.710238,4.04644E-06],[-2,-1,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pFT2_Tisch:=[[438.48,210.07,68.38],[0.495848,0.496503,-0.503146,0.504444],[-1,-1,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pFT3_Tisch:=[[431.81,209.43,65.21],[0.503596,0.502247,0.497433,-0.496689],[-1,-1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pFT4_Tisch:=[[276.70,123.07,189.74],[0.500445,0.507519,0.491183,0.500717],[-2,1,-3,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
!    LOCAL CONST robtarget pFT5_Tisch:=[[275.96,122.15,195.23],[0.503841,-0.494034,0.506984,-0.495017],[-2,1,-1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    
!    LOCAL CONST robtarget pPreposition:=[[58.23,-706.12,710.40],[4.0845E-06,0.983873,-0.178867,-4.62033E-06],[-1,-1,1,0],[9E+09,9E+09,9E+09,9E+09,9E+09,9E+09]];
    
    
    

!    !PERS tooldata tool_vacuum_gripper:=[TRUE,[[0,0,151.5],[0.190809,0,0,0.981627]],[0.707,[0,0,100],[1,0,0,0],0,0,0]];
!    !PERS tooldata tool_servo_rotator:=[TRUE,[[-45.156,-109.017,96.5],[0.587938,0.392848,-0.587938,0.392848]],[0.707,[0,0,40],[1,0,0,0],0,0,0]];
!    !PERS tooldata tool_single_vacuum:=[TRUE,[[74.05,178.77,88.5],[0.5879378,-0.3928475,0.5879378,0.3928475]],[0.707,[0,0,20],[1,0,0,0],0,0,0]];
    
!    !PERS wobjdata wobj_colab:=[FALSE,TRUE,"",[[-311.878,-378.558,188.656],[0.711833,-0.0027417,0.00397248,-0.702332]],[[0,0,0],[1,0,0,0]]];
    
!    LOCAL CONST speeddata my_speed := [ 80, 50, 1, 1 ];

    
    
!    PROC main_mr22m012()
        
        
!        test_program;
        
        
!        WaitTime 1;
        
!    ENDPROC
    
!    PROC test_positions()
        
!        MoveL pos_1, v2000 , z0, tool0\WObj:=wobj0;
!        MoveL pos_2, v2000, z0, tool0\WObj:=wobj0;
!		MoveL pos_3, v2000, z0, tool0\WObj:=wobj0;
!        MoveL pos_4, v2000, z0, tool0\WObj:=wobj0;
!		WaitTime 1;
        
!    ENDPROC
    
!    PROC test_program()
            
!        !movej pPreposition, my_speed, fine, tool0, \Wobj:=wobj0;
!        !test_gehaeuse;
        
!        !movej pPreposition, my_speed, fine, tool0, \Wobj:=wobj0;
!        !test_morobot;
        
!        movej pPreposition, my_speed, fine, tool0, \Wobj:=wobj0;
!        test_servo;
        
        
!    ENDPROC
    
!    PROC test_gehaeuse()
!        Movej offs(pUT1_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pUT1_Tisch,0,0,0),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pUT1_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
        
!        Movej offs(pUT2_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pUT2_Tisch,0,0,0),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pUT2_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
        
!        Movej offs(pUT3_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pUT3_Tisch,0,0,0),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pUT3_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
        
!        Movej offs(pOT1_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pOT1_Tisch,0,0,0),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pOT1_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
        
!        Movej offs(pOT2_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pOT2_Tisch,0,0,0),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pOT2_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
        
!        Movej offs(pOT3_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pOT3_Tisch,0,0,0),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
!        MoveL offs(pOT3_Tisch,0,0,50),my_speed,fine,tool_single_vacuum \WObj:=wobj_colab;
        
!    ENDPROC
    
!    PROC test_morobot()
!        movej pPreposition, my_speed, fine, tool0, \Wobj:=wobj0;
        
!        Movel offs(pFT1_Tisch,0,0,200),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT1_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        activate_multi_vacuum;
!        MoveL offs(pFT1_Tisch,0,0,200),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        Movel offs(pFT2_Tisch,0,0,150),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT2_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        deactivate_multi_vacuum;
!        MoveL offs(pFT2_Tisch,0,50,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        Movel offs(pFT2_Tisch,0,50,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT2_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        activate_multi_vacuum;
!        MoveL offs(pFT2_Tisch,0,0,250),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        Movej offs(pFT3_Tisch,0,0,250),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT3_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        deactivate_multi_vacuum;
!        MoveL offs(pFT3_Tisch,0,50,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        Movel offs(pFT3_Tisch,0,50,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT3_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        activate_multi_vacuum;
!        MoveL offs(pFT3_Tisch,0,0,150),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        movej pPreposition, my_speed, fine, tool0, \Wobj:=wobj0;
        
!        Movej offs(pFT4_Tisch,0,0,300),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT4_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        deactivate_multi_vacuum;
!        MoveL offs(pFT4_Tisch,-50,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        Movej offs(pFT4_Tisch,-50,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT4_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        activate_multi_vacuum;
!        MoveL offs(pFT4_Tisch,0,0,300),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        Movej offs(pFT5_Tisch,0,0,300),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT5_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        deactivate_multi_vacuum;
!        MoveL offs(pFT5_Tisch,-50,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        Movej offs(pFT5_Tisch,-50,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT5_Tisch,0,0,0),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        activate_multi_vacuum;
!        MoveL offs(pFT5_Tisch,0,0,300),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        movej pPreposition, my_speed, fine, tool0, \Wobj:=wobj0;
        
!        Movel offs(pFT1_Tisch,0,0,200),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        MoveL offs(pFT1_Tisch,0,0,35),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
!        deactivate_multi_vacuum;
!        MoveL offs(pFT1_Tisch,0,0,200),my_speed,fine,tool_vacuum_gripper \WObj:=wobj_colab;
        
!        movej pPreposition, my_speed, fine, tool0, \Wobj:=wobj0;
        
!    ENDPROC
    
!    PROC test_servo()
!        Movel offs(pServoAusrichtung_0_grad,0,0,200),my_speed,fine,tool_servo_rotator \WObj:=wobj_colab;
!        MoveL offs(pServoAusrichtung_0_grad,0,0,0),my_speed,fine,tool_servo_rotator\WObj:=wobj_colab;
!        WaitTime 1;
!        MoveL offs(pServoAusrichtung_0_grad,0,0,50),my_speed,fine,tool_servo_rotator \WObj:=wobj_colab;
!    ENDPROC
    
    
    
ENDMODULE