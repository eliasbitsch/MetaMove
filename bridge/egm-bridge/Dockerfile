# Build context = ai-services/ (parent dir), so we can pull egm_pb2 from egm-mock/.
# docker compose up --build bridge    # from inside egm-bridge/
FROM python:3.12-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
        iputils-ping iproute2 net-tools \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/bridge

# protobuf-generated egm_pb2 + the bridge scripts share a parent dir context
COPY egm-mock/egm_pb2.py /opt/egm-mock/egm_pb2.py
COPY egm-mock/egm.proto /opt/egm-mock/egm.proto
COPY egm-bridge/egm_bridge_identity.py \
     egm-bridge/egm_bridge_servo.py \
     egm-bridge/teleop_keyboard.py \
     egm-bridge/sm_egm_pulser.py \
     ./

RUN pip install --no-cache-dir protobuf==7.34.1 roslibpy==2.0.0 requests urllib3

ENV PYTHONUNBUFFERED=1 \
    PYTHONPATH=/opt/egm-mock

EXPOSE 6511/udp 6512/udp

# Default: identity-echo bridge. Override with `command:` in compose for servo.
ENTRYPOINT ["python", "-u"]
CMD ["egm_bridge_identity.py"]
