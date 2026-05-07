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
    VAR egmident   egmId;
    VAR egmstate   egmSt;
    CONST egm_minmax egmLin := [-0.1, 0.1];
    CONST egm_minmax egmRot := [-0.1, 0.1];
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

        ! leave teleop → release EGM session
        IF metaCmd <> 9 AND cmdPrev = 9 THEN
            EGMReset egmId;
            metaMsg := "egm released";
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
                IF cmdPrev <> 9 THEN
                    MoveAbsJ jtEgmStart, v20, fine, tool0;
                    MetaEgmInit;            ! force-reset + SetupUC, ONCE per entry
                    metaState := 1;
                    cmdPrev := 9;
                ENDIF
                metaMsg := "EGM pose";
                MetaEgmActPose;             ! re-arm every iteration
                MetaEgmRun;                  ! RunPose with CondTime

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
    PROC MetaEgmInit()
        EGMGetId egmId;
        EGMReset egmId;
        WaitTime 0.3;
        EGMGetId egmId;
        EGMSetupUC ROB_1, egmId, "default", "MetaMoveUC" \Pose;
    ENDPROC

    PROC MetaEgmActPose()
        EGMActPose egmId,
            \Tool:=tool0,
            \WObj:=egmWobj,
            poseId, EGM_FRAME_BASE,
            poseId, EGM_FRAME_BASE
            \X:=egmLin \Y:=egmLin \Z:=egmLin
            \Rx:=egmRot \Ry:=egmRot \Rz:=egmRot
            \LpFilter:=100
            \SampleRate:=8
            \MaxPosDeviation:=1000
            \MaxSpeedDeviation:=1000;
    ENDPROC

    PROC MetaEgmRun()
        EGMRunPose egmId, EGM_STOP_HOLD
            \X \Y \Z \Rx \Ry \Rz
            \CondTime:=1.0
            \RampInTime:=0.1
            \RampOutTime:=0.1
            \PosCorrGain:=1.0;
        ERROR
            IF ERRNO = ERR_UDPUC_COMM THEN
                metaMsg := "EGM UDP timeout";
                RETURN;
            ELSEIF ERRNO = ERR_ROBLIMIT THEN
                metaMsg := "EGM joint limit";
                TRYNEXT;
            ELSE
                metaMsg := "EGM err=" + NumToStr(ERRNO, 0);
                STOP;
            ENDIF
    ENDPROC

ENDMODULE
