MODULE MetaMoveJointStream(SYSMODULE)
    !
    !   MetaMoveJointStream — pure-EGM Joint-Mode dispatcher.
    !
    !   Replaces ABB SM Add-In's TRobMain/TRobEGM stack so that the controller
    !   stays in EGMRunJoint forever and follows external joint targets at the
    !   robot's full velocity envelope, instead of the SM-default
    !   MaxSpeedDeviation cap (~20 deg/s).
    !
    !   Pairs with the Windows-side EGM bridge that translates moveit_servo
    !   commands (rosbridge → /servo_node/commands) into EgmSensor packets on
    !   port 6511 (UDPUC device "ROB_1" → 192.168.125.99:6511).
    !

    VAR egmident   egmId;
    VAR egmstate   egmSt;
    CONST egm_minmax egmMM := [-1, 1];          ! deg convergence (loose)
    CONST jointtarget jtStart := [[0,0,0,0,90,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    VAR bool started := FALSE;

    PROC main()
        IF NOT started THEN
            VelSet 100, 1000;
            ConfJ \Off;
            ConfL \Off;
            SingArea \LockAxis4;
            MoveAbsJ jtStart, v50, fine, tool0;
            started := TRUE;
        ENDIF

        WHILE TRUE DO
            EGMGetId egmId;
            egmSt := EGMGetState(egmId);
            IF egmSt <= EGM_STATE_CONNECTED THEN
                EGMSetupUC ROB_1, egmId, "default", "ROB_1" \Joint;
            ENDIF

            EGMActJoint egmId
                \Tool:=tool0
                \J1:=egmMM \J2:=egmMM \J3:=egmMM
                \J4:=egmMM \J5:=egmMM \J6:=egmMM
                \LpFilter:=20 \SampleRate:=4
                \MaxSpeedDeviation:=1000;

            EGMRunJoint egmId, EGM_STOP_HOLD
                \J1 \J2 \J3 \J4 \J5 \J6
                \CondTime:=600 \RampInTime:=0.05;

            ! If EGMRunJoint returns (timeout, deviation, etc.) just loop and
            ! re-arm. No external IO trigger needed.
            EGMReset egmId;
            WaitTime 0.1;
        ENDWHILE

        ERROR
            EGMReset egmId;
            WaitTime 0.3;
            TRYNEXT;
    ENDPROC

ENDMODULE
