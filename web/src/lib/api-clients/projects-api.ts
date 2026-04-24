import { requestJson, requestVoid } from "@/lib/http/api-client";
import type {
  ProjectAttachment,
  ProjectDetailResponse,
  ProjectsListResponse,
  ProjectAttachmentKind
} from "@/lib/budget-types";

type CompletionSource = "AUTO" | "MANUAL";
type CompletionStatus = "ACTIVE" | "DONE";

const PROJECTS_PATH = "/api/budget/projects";

export type CreateProjectPayload = {
  name: string;
  description?: string;
  currency: "PLN" | "USD" | "EUR";
  sortOrder?: number;
};

export type UpdateProjectPayload = Partial<CreateProjectPayload>;

export type CreateMilestonePayload = {
  name: string;
  sortOrder?: number;
};

export type UpdateMilestonePayload = {
  name?: string;
  sortOrder?: number;
};

export type UpdateCompletionPayload = {
  source: CompletionSource;
  status?: CompletionStatus;
};

export type CreateStepPayload = {
  name: string;
  sortOrder?: number;
};

export type UpdateStepPayload = {
  name?: string;
  sortOrder?: number;
};

export type CreateItemPayload = {
  name: string;
  plannedAmount: number;
  manualAdjustment?: number;
  isDone?: boolean;
  sortOrder?: number;
};

export type UpdateItemPayload = {
  name?: string;
  plannedAmount?: number;
  manualAdjustment?: number;
  isDone?: boolean;
  sortOrder?: number;
};

export type CreatePaymentPayload = {
  amount: number;
  paymentDate?: string;
  note?: string;
};

export type UpdatePaymentPayload = {
  amount?: number;
  paymentDate?: string;
  note?: string;
};

function projectPath(projectId: string): string {
  return `${PROJECTS_PATH}/${projectId}`;
}

function milestonePath(projectId: string, milestoneId: string): string {
  return `${projectPath(projectId)}/milestones/${milestoneId}`;
}

function stepPath(projectId: string, milestoneId: string, stepId: string): string {
  return `${milestonePath(projectId, milestoneId)}/steps/${stepId}`;
}

function itemPath(projectId: string, milestoneId: string, stepId: string, itemId: string): string {
  return `${stepPath(projectId, milestoneId, stepId)}/items/${itemId}`;
}

function paymentPath(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  paymentId: string
): string {
  return `${itemPath(projectId, milestoneId, stepId, itemId)}/payments/${paymentId}`;
}

function attachmentPath(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  attachmentId: string
): string {
  return `${itemPath(projectId, milestoneId, stepId, itemId)}/attachments/${attachmentId}`;
}

export function getProjects(): Promise<ProjectsListResponse> {
  return requestJson<ProjectsListResponse>(PROJECTS_PATH, {
    method: "GET"
  });
}

export function getProject(projectId: string): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(projectPath(projectId), {
    method: "GET"
  });
}

export function createProject(payload: CreateProjectPayload): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(PROJECTS_PATH, {
    method: "POST",
    body: JSON.stringify({
      name: payload.name,
      description: payload.description ?? "",
      currency: payload.currency,
      sortOrder: payload.sortOrder ?? 0
    })
  });
}

export function updateProject(projectId: string, payload: UpdateProjectPayload): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(projectPath(projectId), {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export function archiveProject(projectId: string): Promise<void> {
  return requestVoid(`${projectPath(projectId)}/archive`, {
    method: "POST"
  });
}

export function createMilestone(
  projectId: string,
  payload: CreateMilestonePayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(`${projectPath(projectId)}/milestones`, {
    method: "POST",
    body: JSON.stringify({
      name: payload.name,
      sortOrder: payload.sortOrder ?? 0
    })
  });
}

export function updateMilestone(
  projectId: string,
  milestoneId: string,
  payload: UpdateMilestonePayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(milestonePath(projectId, milestoneId), {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export function updateMilestoneCompletion(
  projectId: string,
  milestoneId: string,
  payload: UpdateCompletionPayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(`${milestonePath(projectId, milestoneId)}/completion`, {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export function archiveMilestone(projectId: string, milestoneId: string): Promise<void> {
  return deleteMilestone(projectId, milestoneId);
}

export function deleteMilestone(projectId: string, milestoneId: string): Promise<void> {
  return requestVoid(milestonePath(projectId, milestoneId), {
    method: "DELETE"
  });
}

export function createStep(
  projectId: string,
  milestoneId: string,
  payload: CreateStepPayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(`${milestonePath(projectId, milestoneId)}/steps`, {
    method: "POST",
    body: JSON.stringify({
      name: payload.name,
      sortOrder: payload.sortOrder ?? 0
    })
  });
}

export function updateStep(
  projectId: string,
  milestoneId: string,
  stepId: string,
  payload: UpdateStepPayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(stepPath(projectId, milestoneId, stepId), {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export function updateStepCompletion(
  projectId: string,
  milestoneId: string,
  stepId: string,
  payload: UpdateCompletionPayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(`${stepPath(projectId, milestoneId, stepId)}/completion`, {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export function archiveStep(projectId: string, milestoneId: string, stepId: string): Promise<void> {
  return deleteStep(projectId, milestoneId, stepId);
}

export function deleteStep(projectId: string, milestoneId: string, stepId: string): Promise<void> {
  return requestVoid(stepPath(projectId, milestoneId, stepId), {
    method: "DELETE"
  });
}

export function createItem(
  projectId: string,
  milestoneId: string,
  stepId: string,
  payload: CreateItemPayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(`${stepPath(projectId, milestoneId, stepId)}/items`, {
    method: "POST",
    body: JSON.stringify({
      name: payload.name,
      plannedAmount: payload.plannedAmount,
      manualAdjustment: payload.manualAdjustment ?? 0,
      isDone: payload.isDone ?? false,
      sortOrder: payload.sortOrder ?? 0
    })
  });
}

export function updateItem(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  payload: UpdateItemPayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(itemPath(projectId, milestoneId, stepId, itemId), {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export function archiveItem(projectId: string, milestoneId: string, stepId: string, itemId: string): Promise<void> {
  return deleteItem(projectId, milestoneId, stepId, itemId);
}

export function deleteItem(projectId: string, milestoneId: string, stepId: string, itemId: string): Promise<void> {
  return requestVoid(itemPath(projectId, milestoneId, stepId, itemId), {
    method: "DELETE"
  });
}

export function createPayment(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  payload: CreatePaymentPayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(`${itemPath(projectId, milestoneId, stepId, itemId)}/payments`, {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function updatePayment(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  paymentId: string,
  payload: UpdatePaymentPayload
): Promise<ProjectDetailResponse> {
  return requestJson<ProjectDetailResponse>(paymentPath(projectId, milestoneId, stepId, itemId, paymentId), {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export function archivePayment(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  paymentId: string
): Promise<void> {
  return deletePayment(projectId, milestoneId, stepId, itemId, paymentId);
}

export function deletePayment(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  paymentId: string
): Promise<void> {
  return requestVoid(paymentPath(projectId, milestoneId, stepId, itemId, paymentId), {
    method: "DELETE"
  });
}

export async function uploadAttachment(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  options: {
    kind: ProjectAttachmentKind;
    file: File;
    paymentId?: string;
  }
): Promise<ProjectAttachment> {
  const form = new FormData();
  form.set("kind", options.kind);
  if (options.paymentId) {
    form.set("paymentId", options.paymentId);
  }
  form.set("file", options.file);

  const response = await fetch(`${itemPath(projectId, milestoneId, stepId, itemId)}/attachments`, {
    method: "POST",
    body: form,
    cache: "no-store"
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => ({ error: "Attachment upload failed." }));
    const message = typeof payload?.error === "string" ? payload.error : "Attachment upload failed.";
    throw new Error(message);
  }

  return (await response.json()) as ProjectAttachment;
}

export function removeAttachment(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  attachmentId: string
): Promise<ProjectAttachment> {
  return requestJson<ProjectAttachment>(`${attachmentPath(projectId, milestoneId, stepId, itemId, attachmentId)}/remove`, {
    method: "POST"
  });
}

export function resolveAttachmentDownloadUrl(
  projectId: string,
  milestoneId: string,
  stepId: string,
  itemId: string,
  attachmentId: string
): string {
  return `${attachmentPath(projectId, milestoneId, stepId, itemId, attachmentId)}/download`;
}
