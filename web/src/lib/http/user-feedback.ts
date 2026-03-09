import { readApiErrorMessage } from "@/lib/http/api-error";

export type UserFeedback = {
  message: string;
  details?: string;
};

export function toUserFeedback(error: unknown, fallback: string): UserFeedback {
  return readApiErrorMessage(error, fallback);
}
