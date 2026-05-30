FROM debian:bookworm-slim

ARG LORA_VERSION=v0.15.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && curl -fsSL \
         "https://github.com/lora-db/lora/releases/download/${LORA_VERSION}/lora-server-${LORA_VERSION}-x86_64-unknown-linux-gnu.tar.gz" \
       | tar xz --strip-components=1 -C /usr/local/bin \
           "lora-server-${LORA_VERSION}-x86_64-unknown-linux-gnu/lora-server" \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 4747

ENTRYPOINT ["lora-server"]
CMD ["--host", "0.0.0.0", "--port", "4747"]
