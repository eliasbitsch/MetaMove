MODULE MetaMoveCore (SYSMODULE)
    !
    !   MetaMoveCore — minimal EGM Pose teleop dispatcher (proven working 2026-05-07).
    !   Joint-mode wurde entfernt, kommt sauber in einer separaten Session zurück.
    !

    !=== External control (RWS-writable) ===========================
    PERS num    metaCmd        := 0;
        !  0  = idle
        !  1  = MoveJ  to metaTarget
        !  2  = MoveL  to metaTarget
        !  3  = MoveAbsJ to metaJointTarget
        !  9  = EGM Pose teleop (Unity stream → robot)
    PERS bool   metaGo         := FALSE;          ! flank-trigger for cmd 1/2/3
    PERS num    metaSpeed      := 100;            ! 1..100 percent override

    PERS robtarget   metaTarget      := [[400,0,500],[0,0,1,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    PERS jointtarget metaJointTarget := [[0,0,0,0,90,0],[9E9,9E9,9E9,9E9,9E9,9E9]];

    !=== Status (RWS-readable) =====================================
    PERS num    metaState      := 0;
    PERS string metaMsg        := "";

    !=== EGM session state =========================================
    VAR egmident   egmId;            ! Pose-mode session
    VAR egmident   egmIdJoint;       ! Joint-mode session (separate)
    VAR egmstate   egmSt;
    CONST egm_minmax egmLin := [-0.1, 0.1];
    CONST egm_minmax egmRot := [-0.1, 0.1];
    CONST egm_minmax egmJointMM := [-1, 1];     ! deg convergence for joint mode
    CONST pose       poseId := [[0,0,0],[1,0,0,0]];
    PERS wobjdata    egmWobj := [FALSE, TRUE, "", [[0,0,0],[1,0,0,0]], [[0,0,0],[1,0,0,0]]];

    CONST jointtarget jtEgmStart := [[0,0,0,0,90,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    CONST jointtarget jtHome     := [[0,0,0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];

    VAR num  cmdPrev := -1;
    VAR bool started := FALSE;

    !=== MAIN ======================================================
    PROC main()

        IF NOT started THEN
            VelSet metaSpeed, 1000;
            ConfJ \Off;
            ConfL \Off;
            SingArea \Wrist;
            metaState := 0;
            metaMsg := "MetaMoveCore ready";
            MoveAbsJ jtHome, v50, fine, tool0;
            started := TRUE;
        ENDIF

        ! leave teleop → release the right EGM session, longer wait for clean release
        IF metaCmd <> 9 AND cmdPrev = 9 THEN
            EGMReset egmId;
            WaitTime 0.5;
            metaMsg := "egm pose released";
            metaState := 0;
            cmdPrev := metaCmd;
        ENDIF
        IF metaCmd <> 10 AND cmdPrev = 10 THEN
            EGMReset egmIdJoint;
            WaitTime 0.5;
            metaMsg := "egm joint released";
            metaState := 0;
            cmdPrev := metaCmd;
        ENDIF

        TEST metaCmd
            CASE 0:
                cmdPrev := 0;

            CASE 1:
                IF metaGo THEN
                    metaGo := FALSE;
                    metaState := 1;
                    metaMsg := "MoveJ";
                    MoveJ metaTarget, v100, fine, tool0;
                    metaState := 2;
                ENDIF
                cmdPrev := 1;

            CASE 2:
                IF metaGo THEN
                    metaGo := FALSE;
                    metaState := 1;
                    metaMsg := "MoveL";
                    MoveL metaTarget, v100, fine, tool0;
                    metaState := 2;
                ENDIF
                cmdPrev := 2;

            CASE 3:
                IF metaGo THEN
                    metaGo := FALSE;
                    metaState := 1;
                    metaMsg := "MoveAbsJ";
                    MoveAbsJ metaJointTarget, v100, fine, tool0;
                    metaState := 2;
                ENDIF
                cmdPrev := 3;

            CASE 9:
                ! Stay at current pose on mode entry; EGM starts from wherever robot is.
                IF cmdPrev <> 9 THEN
                    metaState := 1;
                    cmdPrev := 9;
                ENDIF
                metaMsg := "EGM pose";
                MetaEgmCyclePose;

            CASE 10:
                IF cmdPrev <> 10 THEN
                    metaState := 1;
                    cmdPrev := 10;
                ENDIF
                metaMsg := "EGM joint";
                MetaEgmCycleJoint;

        DEFAULT:
                metaMsg := "unknown cmd " + NumToStr(metaCmd, 0);
                metaState := 3;
                cmdPrev := metaCmd;
        ENDTEST

        WaitTime 0.05;

        ERROR
            metaState := 3;
            metaMsg := "errno=" + NumToStr(ERRNO, 0);
            TRYNEXT;
    ENDPROC

    ! MetaEgmInit — called ONCE on CASE 9 entry. Force-reset any stale binding,
    ! grab fresh egmId, set up UC. EGMActPose moved to its own PROC for re-arm.
    !=== One iteration of EGM Pose mode (lsurobotics pattern) ============
    PROC MetaEgmCyclePose()
        EGMGetId egmId;
        egmSt := EGMGetState(egmId);
        IF egmSt <= EGM_STATE_CONNECTED THEN
            EGMSetupUC ROB_1, egmId, "default", "MetaMoveUC" \Pose;
        ENDIF
        EGMActPose egmId,
            \Tool:=tool0, \WObj:=egmWobj,
            poseId, EGM_FRAME_BASE,
            poseId, EGM_FRAME_BASE
            \X:=egmLin \Y:=egmLin \Z:=egmLin
            \Rx:=egmRot \Ry:=egmRot \Rz:=egmRot
            \LpFilter:=100 \SampleRate:=8
            \MaxPosDeviation:=1000 \MaxSpeedDeviation:=1000;
        EGMRunPose egmId, EGM_STOP_HOLD
            \X \Y \Z \Rx \Ry \Rz
            \CondTime:=1.0 \RampInTime:=0.5
            \RampOutTime:=0.1 \PosCorrGain:=1.0;
        ERROR
            metaMsg := "EGM pose retry errno=" + NumToStr(ERRNO, 0);
            EGMReset egmId;
            WaitTime 0.3;
            RETURN;
    ENDPROC

    !=== One iteration of EGM Joint mode (uses separate UDPUC: MetaMoveJoint) ===
    PROC MetaEgmCycleJoint()
        EGMGetId egmIdJoint;
        egmSt := EGMGetState(egmIdJoint);
        IF egmSt <= EGM_STATE_CONNECTED THEN
            EGMSetupUC ROB_1, egmIdJoint, "default", "MetaMoveJoint" \Joint;
        ENDIF
        EGMActJoint egmIdJoint
            \Tool:=tool0
            \J1:=egmJointMM \J2:=egmJointMM \J3:=egmJointMM
            \J4:=egmJointMM \J5:=egmJointMM \J6:=egmJointMM
            \LpFilter:=100 \SampleRate:=8
            \MaxSpeedDeviation:=1000;
        EGMRunJoint egmIdJoint, EGM_STOP_HOLD
            \J1 \J2 \J3 \J4 \J5 \J6
            \CondTime:=1.0 \RampInTime:=0.1;
        ERROR
            metaMsg := "EGM joint retry errno=" + NumToStr(ERRNO, 0);
            EGMReset egmIdJoint;
            WaitTime 0.3;
            RETURN;
    ENDPROC

ENDMODULE
