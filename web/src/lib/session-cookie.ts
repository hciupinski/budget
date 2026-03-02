const LOCAL_HOST_PATTERN = /^(localhost|127\.0\.0\.1|\[::1\])(?::\d+)?$/i;

function firstHeaderValue(value: string | null): string | null {
  if (!value) {
    return null;
  }

  const first = value.split(",")[0]?.trim();
  return first || null;
}

export function resolveRequestHost(request: Request): string {
  const fromForwarded = firstHeaderValue(request.headers.get("x-forwarded-host"));
  const fromHost = firstHeaderValue(request.headers.get("host"));

  if (fromForwarded) {
    return fromForwarded;
  }

  if (fromHost) {
    return fromHost;
  }

  return new URL(request.url).host;
}

export function resolveRequestProtocol(request: Request): "http" | "https" {
  const host = resolveRequestHost(request);
  if (LOCAL_HOST_PATTERN.test(host)) {
    return "http";
  }

  const forwardedProto = firstHeaderValue(request.headers.get("x-forwarded-proto"));

  if (forwardedProto === "https") {
    return "https";
  }

  if (forwardedProto === "http") {
    return "http";
  }

  const origin = firstHeaderValue(request.headers.get("origin"));
  if (origin) {
    try {
      const protocol = new URL(origin).protocol.replace(":", "");
      return protocol === "https" ? "https" : "http";
    } catch {
      return "http";
    }
  }

  return new URL(request.url).protocol === "https:" ? "https" : "http";
}

export function shouldUseSecureCookie(request: Request): boolean {
  return resolveRequestProtocol(request) === "https";
}

export function resolveRedirectOrigin(request: Request): string {
  const originHeader = firstHeaderValue(request.headers.get("origin"));
  if (originHeader) {
    return originHeader;
  }

  const host = resolveRequestHost(request);
  const protocol = resolveRequestProtocol(request);
  return `${protocol}://${host}`;
}
