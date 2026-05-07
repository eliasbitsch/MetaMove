MODULE MetaMoveDispatcher (SYSMODULE)
    !
    !   MetaMove Dispatcher — Mode-based demo + teleop dispatcher.
    !   Adapted from GoHolo's module_MAIN_GOHOLO pattern (Jakob Hörbst 2021,
    !   extended Alex Korn 2022), updated for MetaMove's 6 demo scenarios
    !   plus EGM teleop (Pinch-to-Move).
    !
    !   Mode semantics:
    !     0   idle — robot holds position, waits
    !     1   chess       — pick and place, single piece
    !     2   stone_sort  — 6 polyhedra → 6 target boxes
    !     3   framing     — two-step assembly
    !     4   mug         — fragile handling, reduced speed
    !     5   pins        — precision, N pins on map
    !     6   bigstone    — payload, torque-overlay showcase
    !     9   egm_teleop  — Unity Pinch-to-Move via EGM
    !     90  egm_teleop sub-state (running the EGMRunPose loop)
    !     99  error
    !
    !   External control (PERS, RWS-writable from Unity/Jarvis):
    !     metaMode    — set to one of the above
    !     metaStart   — set TRUE to begin the selected demo
    !     metaAbort   — set TRUE to abort current motion
    !     metaSpeed   — 10..100 speed override
    !
    !   Status (PERS, RWS-readable):
    !     metaState   — 0=idle 1=running 2=done 3=error
    !     metaStep    — sub-step within current demo
    !     metaMsg     — last status message for HUD
    !
    !   Uses UDPUC host "MetaMoveUC" (RemoteAddress=192.168.125.99:6511) for
    !   EGM. ROB_Michi exists in parallel (same endpoint) and is left untouched.
    !   Unity listens on the Windows alias-IP 192.168.125.99 on port 6511.
    !

    !=== External control (write via RWS) ===================================
    PERS num    metaMode       := 0;
    PERS bool   metaStart      := FALSE;
    PERS bool   metaAbort      := FALSE;
    PERS num    metaSpeed      := 50;        ! percent

    !=== Status (read via RWS) ==============================================
    PERS num    metaState      := 0;         ! 0=idle 1=running 2=done 3=error
    PERS num    metaStep       := 0;
    PERS string metaMsg        := "";

    !=== LED control (Asi1Led RGB + Period). When metaLedOverride=TRUE the
    !    dispatcher writes RGB to the Asi GO signals each loop tick. Set FALSE
    !    to release control back to T_GOFA_LED (system-state colouring).
    PERS bool   metaLedOverride := FALSE;
    PERS num    metaLedR        := 0;        ! 0..255
    PERS num    metaLedG        := 0;        ! 0..255
    PERS num    metaLedB        := 0;        ! 0..255
    PERS num    metaLedPeriod   := 0;        ! 0=solid, >0=blink period

    !=== Unity-supplied targets (set via RWS before metaStart) ==============
    PERS robtarget metaPickTarget  := [[400,0,500],[0,1,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    PERS robtarget metaPlaceTarget := [[300,200,500],[0,1,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    PERS num    metaPinIndex    := 0;
    PERS num    metaStoneClass  := 1;

    !=== EGM identifiers ====================================================
    VAR egmident   egmMetaId;
    VAR egmstate   egmMetaSt;
    CONST pose     poseZero := [[0,0,0],[1,0,0,0]];
    CONST egm_minmax egmLim := [-5, 5];

    !=== Mode-change TRAP (from GoHolo pattern) =============================
    VAR intnum modePrev;
    VAR num    modePrevTerminate := -1;
    VAR bool   bStartup := TRUE;

    !=== Startup pose =======================================================
    CONST jointtarget pInitStartup := [[0,0,0,0,0,0],[0,0,0,0,0,0]];
    CONST jointtarget pInitEGM     := [[0,0,0,0,90,0],[0,0,0,0,0,0]];

    !=========================================================================
    ! MAIN — the dispatcher loop
    !=========================================================================
    PROC main()

        ! First-run init
        IF bStartup THEN
            Startup;
            bStartup := FALSE;
        ENDIF

        ! External mode change via RWS?
        IF metaMode <> modePrevTerminate AND ValidMode(metaMode) THEN
            Modehandler;
        ENDIF

        ! Dispatch
        TEST metaMode
            CASE 0:
                ! idle — do nothing
                IF metaStart THEN
                    ! operator wants to start but has mode=0 → noop
                    metaStart := FALSE;
                    metaMsg := "mode=0 idle, set metaMode first";
                ENDIF

            CASE 1:
                IF metaStart THEN
                    metaStart := FALSE;
                    RunDemoChess;
                ENDIF

            CASE 2:
                IF metaStart THEN
                    metaStart := FALSE;
                    RunDemoStoneSort;
                ENDIF

            CASE 3:
                IF metaStart THEN
                    metaStart := FALSE;
                    RunDemoFraming;
                ENDIF

            CASE 4:
                IF metaStart THEN
                    metaStart := FALSE;
                    RunDemoMug;
                ENDIF

            CASE 5:
                IF metaStart THEN
                    metaStart := FALSE;
                    RunDemoPins;
                ENDIF

            CASE 6:
                IF metaStart THEN
                    metaStart := FALSE;
                    RunDemoBigStone;
                ENDIF

            CASE 9:
                ! teleop entry — init EGM, then move to sub-state 90.
                ! Pre-set modePrevTerminate to 90 so the next main() iteration
                ! does NOT call Modehandler (which would EGMStop the session
                ! we just set up).
                MoveAbsJ pInitEGM, v100, fine, tool0;
                MetaEGM_Init;
                DeleteTrap;
                modePrevTerminate := 90;
                metaMode := 90;

            CASE 90:
                ! EGM pose-mode running; TRAP catches external mode change
                MetaEGM_RunPose;
        DEFAULT:
                metaMsg := "unknown mode " + NumToStr(metaMode, 0);
                metaState := 3;
        ENDTEST

        UpdateLED;
        WaitTime 0.05;

        ERROR
            metaState := 3;
            metaMsg := "main trap errno=" + NumToStr(ERRNO, 0);
            TRYNEXT;
    ENDPROC

    PROC UpdateLED()
        IF metaLedOverride THEN
            SetGO Asi1LedRed,    metaLedR;
            SetGO Asi1LedGreen,  metaLedG;
            SetGO Asi1LedBlue,   metaLedB;
            SetGO Asi1LedPeriod, metaLedPeriod;
        ENDIF
        ERROR
            ! ignore IO write conflicts — T_GOFA_LED may temporarily own
            TRYNEXT;
    ENDPROC

    !=========================================================================
    ! Startup — runs once at program start
    !=========================================================================
    PROC Startup()
        SetSpeed metaSpeed;
        ConfJ \Off;
        ConfL \Off;

        metaMode := 0;
        metaState := 0;
        metaStep := 0;
        metaMsg := "MetaMove ready";
        modePrevTerminate := -1;

        DeleteTrap;
        AddTrap;

        MoveAbsJ pInitStartup, v200, fine, tool0;
    ENDPROC

    !=========================================================================
    ! Modehandler — clean transition between modes (from GoHolo pattern)
    !=========================================================================
    PROC Modehandler()
        ! Cleanup if leaving EGM mode
        IF modePrevTerminate = 9 OR modePrevTerminate = 90 THEN
            MetaEGM_Stop;
        ENDIF

        metaState := 0;
        metaStep := 0;

        ! Re-arm TRAP for next change
        DeleteTrap;
        AddTrap;

        modePrevTerminate := metaMode;
        metaMsg := "mode=" + NumToStr(metaMode, 0);
    ENDPROC

    !=========================================================================
    ! Demo procedures
    ! Each: set metaState=1 on entry, =2 on clean exit, =3 on error.
    ! Unity fills metaPickTarget / metaPlaceTarget via RWS before metaStart.
    !=========================================================================
    PROC RunDemoChess()
        metaState := 1;
        metaStep := 1; ! approach
        MoveJ Offs(metaPickTarget, 0, 0, 100), v200, z20, tool0;
        metaStep := 2; ! descend
        MoveL metaPickTarget, v100, fine, tool0;
        metaStep := 3; ! grip — TODO: SetDO ox_multi_vac_greifer_schliessen when IO restored
        WaitTime 0.3;
        metaStep := 4; ! retract
        MoveL Offs(metaPickTarget, 0, 0, 100), v100, z20, tool0;
        metaStep := 5; ! move to place
        MoveJ Offs(metaPlaceTarget, 0, 0, 100), v200, z20, tool0;
        metaStep := 6; ! place
        MoveL metaPlaceTarget, v100, fine, tool0;
        ! TODO: SetDO ox_multi_vac_greifer_oeffnen when IO restored
        WaitTime 0.3;
        metaStep := 7; ! retract
        MoveL Offs(metaPlaceTarget, 0, 0, 100), v100, z20, tool0;
        metaStep := 0;
        metaState := 2;
    ENDPROC

    PROC RunDemoStoneSort()
        metaState := 1;
        metaStep := 1;
        ! TODO: loop per metaStoneClass — Unity sets class, we pick+place.
        WaitTime 0.5;
        metaStep := 0;
        metaState := 2;
    ENDPROC

    PROC RunDemoFraming()
        metaState := 1;
        metaStep := 1;
        ! TODO: 2-step assembly — pick picture, align over frame, insert.
        WaitTime 0.5;
        metaStep := 0;
        metaState := 2;
    ENDPROC

    PROC RunDemoMug()
        ! Hard speed cap for fragile item
        VelSet 30, 300;
        metaState := 1;
        metaStep := 1;
        ! TODO: mug pick + serving-table place
        WaitTime 0.5;
        VelSet 100, 1000;
        metaStep := 0;
        metaState := 2;
    ENDPROC

    PROC RunDemoPins()
        metaState := 1;
        metaStep := 1;
        ! TODO: iterate metaPinIndex, precision place
        WaitTime 0.5;
        metaStep := 0;
        metaState := 2;
    ENDPROC

    PROC RunDemoBigStone()
        metaState := 1;
        metaStep := 1;
        ! TODO: payload showcase, Unity monitors torques via EGM feedback
        WaitTime 0.5;
        metaStep := 0;
        metaState := 2;
    ENDPROC

    !=========================================================================
    ! EGM teleop — "MetaMoveUC" UDPUC device
    ! Kept separate from GoHolo's module_EGM so both can coexist.
    !=========================================================================
    PROC MetaEGM_Init()
        EGMGetId egmMetaId;
        egmMetaSt := EGMGetState(egmMetaId);
        IF egmMetaSt <= EGM_STATE_CONNECTED THEN
            ! Use MetaMoveUC on port 6512 (avoids stale-socket conflicts on
            ! Windows port 6511 where ROB_Michi was historically directed).
            EGMSetupUC ROB_1, egmMetaId, "default", "MetaMoveUC" \Pose;
        ENDIF

        EGMActPose egmMetaId
            \StreamStart
            \Tool:=tool0 \WObj:=wobj0,
            poseZero, EGM_FRAME_WOBJ,
            poseZero, EGM_FRAME_WOBJ
            \x:=egmLim \y:=egmLim \z:=egmLim
            \rx:=egmLim \ry:=egmLim \rz:=egmLim
            \MaxPosDeviation:=1000
            \MaxSpeedDeviation:=50;

        metaState := 1;
        metaMsg := "EGM active";
    ENDPROC

    PROC MetaEGM_RunPose()
        EGMRunPose egmMetaId, EGM_STOP_HOLD
            \x \y \z \Rx \Ry \Rz
            \RampInTime:=0.1;

        ERROR
            metaState := 3;
            IF ERRNO = ERR_UDPUC_COMM THEN
                metaMsg := "EGM: UDP timeout";
                RETURN;
            ELSEIF ERRNO = ERR_ROBLIMIT THEN
                metaMsg := "EGM: joint out of range";
                TRYNEXT;
            ELSE
                metaMsg := "EGM errno=" + NumToStr(ERRNO, 0);
                STOP;
            ENDIF
    ENDPROC

    PROC MetaEGM_Stop()
        EGMGetId egmMetaId;
        egmMetaSt := EGMGetState(egmMetaId);
        IF egmMetaSt > EGM_STATE_CONNECTED THEN
            EGMStop egmMetaId, EGM_STOP_HOLD;
        ENDIF
        EGMReset egmMetaId;
    ENDPROC

    !=========================================================================
    ! Mode-change TRAP — lets external RWS writes interrupt current motion.
    ! From GoHolo pattern.
    !=========================================================================
    PROC DeleteTrap()
        IDelete modePrev;
    ENDPROC

    PROC AddTrap()
        CONNECT modePrev WITH metaModeChangedTrap;
        IPers metaMode, modePrev;
    ENDPROC

    TRAP metaModeChangedTrap
        IF ValidMode(metaMode) THEN
            ExitCycle;
        ENDIF
    ENDTRAP

    !=========================================================================
    ! Helpers
    !=========================================================================
    FUNC bool ValidMode(num m)
        RETURN m = 0 OR (m >= 1 AND m <= 6) OR m = 9 OR m = 90;
    ENDFUNC

    PROC SetSpeed(num pct)
        IF pct < 10 THEN pct := 10; ENDIF
        IF pct > 100 THEN pct := 100; ENDIF
        VelSet pct, 1000;
        metaSpeed := pct;
    ENDPROC

ENDMODULE
