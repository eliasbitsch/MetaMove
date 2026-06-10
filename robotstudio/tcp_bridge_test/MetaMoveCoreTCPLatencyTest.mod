MODULE MetaMoveCoreTCPLatencyTest (SYSMODULE)
    ! Latency probe — measures RAPID-side TCP roundtrip to Windows host.
    ! Run with Python tcp_echo_latency.py server listening on host:7000.
    !
    ! Sends 200 frames, each containing the local clock-tick in ms.
    ! Receives echo back, computes diff, accumulates min/max/avg.
    ! Result printed via TPWrite at the end.

    VAR socketdev clientSocket;
    VAR string sendBuf;
    VAR string recvBuf;
    VAR num t0;
    VAR num t1;
    VAR num latencyMs;
    VAR num sumMs := 0;
    VAR num maxMs := 0;
    VAR num minMs := 99999;
    VAR num okCount := 0;
    CONST num totalFrames := 200;
    VAR num i;

    PROC main()
        TPWrite "TCP-Latency: connecting to 127.0.0.1:7000";
        SocketCreate clientSocket;
        SocketConnect clientSocket, "127.0.0.1", 7000;
        TPWrite "TCP-Latency: connected, starting probe";

        FOR i FROM 1 TO totalFrames DO
            t0 := ClkRead(\HighRes);
            sendBuf := NumToStr(t0, 6) + "\0A";
            SocketSend clientSocket \Str:=sendBuf;
            SocketReceive clientSocket \Str:=recvBuf \Time:=2;
            t1 := ClkRead(\HighRes);
            latencyMs := (t1 - t0) * 1000;
            sumMs := sumMs + latencyMs;
            IF latencyMs > maxMs THEN maxMs := latencyMs; ENDIF
            IF latencyMs < minMs THEN minMs := latencyMs; ENDIF
            okCount := okCount + 1;
        ENDFOR

        SocketClose clientSocket;
        TPWrite "TCP-Latency results (" + NumToStr(okCount, 0) + " frames):";
        TPWrite "  avg = " + NumToStr(sumMs / okCount, 2) + " ms";
        TPWrite "  min = " + NumToStr(minMs, 2) + " ms";
        TPWrite "  max = " + NumToStr(maxMs, 2) + " ms";

        ERROR
            TPWrite "TCP-Latency ERROR " + NumToStr(ERRNO, 0);
            SocketClose clientSocket;
    ENDPROC
ENDMODULE
