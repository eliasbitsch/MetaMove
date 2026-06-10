MODULE MetaMoveCorePers (SYSMODULE)
    ! Sim-path RAPID loop driven by RWS PERS-variable writes.
    ! Pair with metamove_bridge servo_bridge:=true — Python writes jTarget,
    ! this loop reads + moves the robot.
    !
    ! Latency expectation: ~25-50ms total (RWS REST POST + RAPID scheduler).
    ! Not for realtime Quest hand-teleop — see EGM path for that.

    PERS jointtarget jTarget := [[0,0,0,0,30,0],[9E9,9E9,9E9,9E9,9E9,9E9]];
    PERS jointtarget jTargetLast := [[0,0,0,0,30,0],[9E9,9E9,9E9,9E9,9E9,9E9]];

    VAR speeddata vServo := [200, 50, 5000, 1000];

    PROC main()
        ConfL\Off;
        ConfJ\Off;
        SingArea\Wrist;

        WHILE TRUE DO
            IF NOT jointEqual(jTarget, jTargetLast) THEN
                jTargetLast := jTarget;
                MoveAbsJ jTarget, vServo, z10, tool0;
            ELSE
                WaitTime 0.005;
            ENDIF
        ENDWHILE

        ERROR
            TPWrite "MetaMoveCorePers ERROR " + NumToStr(ERRNO, 0);
            WaitTime 0.5;
            RETRY;
    ENDPROC

    FUNC bool jointEqual(jointtarget a, jointtarget b)
        RETURN abs(a.robax.rax_1 - b.robax.rax_1) < 0.001
            AND abs(a.robax.rax_2 - b.robax.rax_2) < 0.001
            AND abs(a.robax.rax_3 - b.robax.rax_3) < 0.001
            AND abs(a.robax.rax_4 - b.robax.rax_4) < 0.001
            AND abs(a.robax.rax_5 - b.robax.rax_5) < 0.001
            AND abs(a.robax.rax_6 - b.robax.rax_6) < 0.001;
    ENDFUNC
ENDMODULE
