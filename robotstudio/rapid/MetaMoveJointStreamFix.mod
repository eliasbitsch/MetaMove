MODULE MetaMoveJointStream(SYSMODULE)
    ! Pure-EGM joint streamer for the MetaMove servo bridge (.99:6511).
    ! PROC renamed main -> MetaJointMain: task T_ROB1 already has a main
    ! (module_MAIN); a second main breaks the whole program (elog 40160).
    VAR egmident   egmId;
    VAR egmstate   egmSt;
    CONST egm_minmax egmMM := [-1, 1];
    CONST jointtarget jtStart := [[0,0,0,0,90,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    VAR bool started := FALSE;

    PROC MetaJointMain()
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

            ! NOTE: no \PosCorrGain — EGMActJoint on RW7.20 rejects it (elog
            ! 40160). Joint corrections need no gain; the real "robot ignores
            ! corrections" cause was the bridge replying from the wrong source
            ! IP (UDPUC drops packets not from its RemoteAddress).
            EGMActJoint egmId
                \Tool:=tool0
                \J1:=egmMM \J2:=egmMM \J3:=egmMM
                \J4:=egmMM \J5:=egmMM \J6:=egmMM
                \LpFilter:=20 \SampleRate:=4
                \MaxSpeedDeviation:=1000;

            EGMRunJoint egmId, EGM_STOP_HOLD
                \J1 \J2 \J3 \J4 \J5 \J6
                \CondTime:=600 \RampInTime:=0.05;

            EGMReset egmId;
            WaitTime 0.1;
        ENDWHILE

        ERROR
            EGMReset egmId;
            WaitTime 0.3;
            TRYNEXT;
    ENDPROC

ENDMODULE
