import { isApiError } from "@/lib/http/api-error";

export function redirectToLoginIfUnauthorized(error: unknown): boolean {
  if (!isApiError(error) || error.status !== 401) {
    return false;
  }

  if (typeof window !== "undefined") {
    window.location.assign("/login");
  }

  return true;
}
