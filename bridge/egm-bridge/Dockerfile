# Dependency-only image. Sources (egm-bridge scripts + egm-mock) are bind-mounted
# via docker-compose volumes, so edits are live and no rebuild is needed.
# docker compose up --build bridge    # from inside egm-bridge/
FROM python:3.12-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
        iputils-ping iproute2 net-tools \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/bridge

RUN pip install --no-cache-dir protobuf==7.34.1 roslibpy==2.0.0 requests urllib3

# egm-bridge scripts are mounted at /opt/bridge, egm-mock (with the generated
# egm_pb2) at /opt/egm-mock — both via docker-compose `volumes:` (not COPY).
ENV PYTHONUNBUFFERED=1 \
    PYTHONPATH=/opt/egm-mock

EXPOSE 6511/udp 6512/udp

# Default: identity-echo bridge. Override with `command:` in compose for servo.
ENTRYPOINT ["python", "-u"]
CMD ["egm_bridge_identity.py"]
