FROM alpine:3.22 AS release
ARG TARGETARCH
RUN apk add --no-cache ca-certificates curl
RUN set -eu; \
    case "$TARGETARCH" in \
      amd64) checksum=ac90da87a35641be5a0ac75d49de5161ddb47d629b5ba01261b0ae9e00aea15f ;; \
      arm64) checksum=61bb88e7435919834478ddd4d405a6de6d2c227079da5e8ee9655147398819a0 ;; \
      *) echo "Unsupported MinIO Client architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    mkdir -p /out/licenses; \
    curl --fail --silent --show-error --location --retry 3 \
      "https://github.com/minio/mc/releases/download/RELEASE.2025-04-16T18-13-26Z/mc.linux-${TARGETARCH}.RELEASE.2025-04-16T18-13-26Z" \
      --output /out/mc; \
    printf '%s  /out/mc\n' "$checksum" | sha256sum -c -; \
    chmod 755 /out/mc; \
    for notice in LICENSE CREDITS NOTICE; do \
      curl --fail --silent --show-error --location --retry 3 \
        "https://raw.githubusercontent.com/minio/mc/b00526b153a31b36767991a4f5ce2cced435ee8e/${notice}" \
        --output "/out/licenses/${notice}"; \
    done

FROM alpine:3.22
LABEL org.opencontainers.image.source="https://github.com/minio/mc" \
      org.opencontainers.image.revision="b00526b153a31b36767991a4f5ce2cced435ee8e" \
      org.opencontainers.image.version="RELEASE.2025-04-16T18-13-26Z" \
      org.opencontainers.image.licenses="AGPL-3.0-or-later"
RUN apk add --no-cache ca-certificates curl
COPY --from=release /out/mc /usr/local/bin/mc
COPY --from=release /out/licenses /licenses
ENTRYPOINT ["/usr/local/bin/mc"]
