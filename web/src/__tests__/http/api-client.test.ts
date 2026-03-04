import { afterEach, describe, expect, it, vi } from "vitest";
import { requestJson } from "@/lib/http/api-client";
import { ApiError } from "@/lib/http/api-error";

describe("api-client", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("returns parsed JSON for successful responses", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ ok: true }), {
          status: 200,
          headers: { "Content-Type": "application/json" }
        })
      )
    );

    const result = await requestJson<{ ok: boolean }>("/api/example", { method: "GET" });
    expect(result.ok).toBe(true);
  });

  it("throws ApiError with backend message for failed responses", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ error: "Too many attempts" }), {
          status: 429,
          headers: { "Content-Type": "application/json" }
        })
      )
    );

    await expect(requestJson("/api/example", { method: "POST" })).rejects.toMatchObject<ApiError>({
      status: 429,
      message: "Too many attempts"
    });
  });

  it("throws ApiError with status 0 when network request fails", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new Error("offline"))
    );

    await expect(requestJson("/api/example", { method: "GET" })).rejects.toMatchObject<ApiError>({
      status: 0,
      message: "Network request failed."
    });
  });
});
