import { ApiError } from "@/lib/http/api-error";

type JsonPrimitive = string | number | boolean | null;
type JsonValue = JsonPrimitive | JsonValue[] | { [key: string]: JsonValue };

function extractMessage(payload: unknown, fallback: string): { message: string; details?: string } {
  if (payload && typeof payload === "object") {
    const data = payload as Record<string, unknown>;

    const messageCandidate =
      (typeof data.error === "string" && data.error.trim()) ||
      (typeof data.message === "string" && data.message.trim()) ||
      "";

    const detailsCandidate = typeof data.details === "string" && data.details.trim() ? data.details.trim() : undefined;

    if (messageCandidate) {
      return {
        message: messageCandidate,
        details: detailsCandidate
      };
    }
  }

  if (typeof payload === "string" && payload.trim()) {
    return { message: payload.trim() };
  }

  return { message: fallback };
}

async function parseResponseBody(response: Response): Promise<unknown> {
  const contentType = response.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    try {
      return (await response.json()) as JsonValue;
    } catch {
      return null;
    }
  }

  try {
    const text = await response.text();
    return text;
  } catch {
    return null;
  }
}

async function execute(path: string, init: RequestInit): Promise<unknown> {
  const headers = new Headers(init.headers);

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  let response: Response;

  try {
    response = await fetch(path, {
      ...init,
      cache: init.cache ?? "no-store",
      headers
    });
  } catch {
    throw new ApiError("Network request failed.", {
      status: 0,
      details: "Could not reach the server."
    });
  }

  const payload = await parseResponseBody(response);

  if (!response.ok) {
    const extracted = extractMessage(payload, `Request failed with status ${response.status}.`);
    throw new ApiError(extracted.message, {
      status: response.status,
      details: extracted.details,
      payload
    });
  }

  return payload;
}

export async function requestJson<TResponse>(path: string, init: RequestInit = {}): Promise<TResponse> {
  const payload = await execute(path, init);
  return payload as TResponse;
}

export async function requestVoid(path: string, init: RequestInit = {}): Promise<void> {
  await execute(path, init);
}

export async function requestText(path: string, init: RequestInit = {}): Promise<string> {
  const payload = await execute(path, init);
  return typeof payload === "string" ? payload : JSON.stringify(payload);
}
