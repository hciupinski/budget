import { requestJson } from "@/lib/http/api-client";

export function login(email: string, password: string): Promise<{ ok: boolean }> {
  return requestJson<{ ok: boolean }>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password })
  });
}
