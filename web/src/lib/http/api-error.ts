export class ApiError extends Error {
  readonly status: number;
  readonly details?: string;
  readonly payload?: unknown;

  constructor(message: string, options: { status: number; details?: string; payload?: unknown }) {
    super(message);
    this.name = "ApiError";
    this.status = options.status;
    this.details = options.details;
    this.payload = options.payload;
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

export function readApiErrorMessage(error: unknown, fallback: string): { message: string; details?: string } {
  if (isApiError(error)) {
    return {
      message: error.message || fallback,
      details: error.details
    };
  }

  if (error instanceof Error && error.message.trim()) {
    return { message: error.message.trim() };
  }

  return { message: fallback };
}
