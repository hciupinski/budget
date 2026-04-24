"use client";

import { useEffect, useMemo, useState, type ReactElement } from "react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { PaperclipIcon, PlusIcon, TrashIcon } from "@/components/budget/icons";
import {
  archiveProject,
  createItem,
  createMilestone,
  createPayment,
  createProject,
  createStep,
  deleteItem,
  deleteMilestone,
  deletePayment,
  deleteStep,
  getProject,
  getProjects,
  removeAttachment,
  resolveAttachmentDownloadUrl,
  updateItem,
  updateMilestoneCompletion,
  updateStepCompletion,
  uploadAttachment
} from "@/lib/api-clients/projects-api";
import {
  formatCurrency,
  useCurrencySetting,
  type CurrencyCode
} from "@/lib/currency-settings";
import type {
  ProjectAttachmentKind,
  ProjectDetailResponse,
  ProjectItem,
  ProjectMilestone,
  ProjectStep,
  ProjectSummary
} from "@/lib/budget-types";
import { redirectToLoginIfUnauthorized } from "@/lib/http/auth-redirect";
import { toUserFeedback, type UserFeedback } from "@/lib/http/user-feedback";

const CURRENCIES: CurrencyCode[] = ["PLN", "USD", "EUR"];
const ATTACHMENT_KINDS: ProjectAttachmentKind[] = ["RECEIPT", "AGREEMENT", "DOCUMENT"];

type DraftStep = {
  name: string;
  sortOrder: string;
};

type DraftItem = {
  name: string;
  plannedAmount: string;
  manualAdjustment: string;
  sortOrder: string;
};

type DraftPayment = {
  amount: string;
  note: string;
};

type AttachmentModalState = {
  milestoneId: string;
  stepId: string;
  itemId: string;
};

function statusTagClass(status: "ACTIVE" | "DONE"): string {
  return status === "DONE"
    ? "border-[#bce6cf] bg-[#effaf4] text-[#0f7a3c]"
    : "border-[#cbd5e1] bg-[#f8fafc] text-[#334155]";
}

function varianceClass(variance: number): string {
  if (variance > 0) {
    return "text-[#b42318]";
  }

  if (variance < 0) {
    return "text-[#027a48]";
  }

  return "text-[#344054]";
}

function splitByCompletion<TItem extends { completionStatus?: "ACTIVE" | "DONE"; isDone?: boolean }>(
  items: TItem[]
): { active: TItem[]; completed: TItem[] } {
  const active: TItem[] = [];
  const completed: TItem[] = [];

  for (const item of items) {
    const isCompleted = item.completionStatus
      ? item.completionStatus === "DONE"
      : item.isDone === true;

    if (isCompleted) {
      completed.push(item);
    } else {
      active.push(item);
    }
  }

  return { active, completed };
}

function hierarchyCellClass(level: 0 | 1 | 2 | 3): string {
  if (level === 0) {
    return "pl-4";
  }

  if (level === 1) {
    return "pl-8";
  }

  if (level === 2) {
    return "pl-12";
  }

  return "pl-16";
}

export function ProjectsView() {
  useCurrencySetting();
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null);
  const [project, setProject] = useState<ProjectDetailResponse | null>(null);
  const [loadingList, setLoadingList] = useState<boolean>(true);
  const [loadingDetail, setLoadingDetail] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);
  const [message, setMessage] = useState<UserFeedback | null>(null);
  const [isProjectNavCollapsed, setIsProjectNavCollapsed] = useState<boolean>(false);

  const [projectName, setProjectName] = useState<string>("");
  const [projectDescription, setProjectDescription] = useState<string>("");
  const [projectCurrency, setProjectCurrency] = useState<CurrencyCode>("USD");

  const [milestoneName, setMilestoneName] = useState<string>("");
  const [milestoneSortOrder, setMilestoneSortOrder] = useState<string>("0");

  const [stepDrafts, setStepDrafts] = useState<Record<string, DraftStep>>({});
  const [itemDrafts, setItemDrafts] = useState<Record<string, DraftItem>>({});
  const [paymentDrafts, setPaymentDrafts] = useState<Record<string, DraftPayment>>({});
  const [attachmentKindDrafts, setAttachmentKindDrafts] = useState<Record<string, ProjectAttachmentKind>>({});

  const [expandedMilestones, setExpandedMilestones] = useState<Record<string, boolean>>({});
  const [expandedSteps, setExpandedSteps] = useState<Record<string, boolean>>({});
  const [expandedItems, setExpandedItems] = useState<Record<string, boolean>>({});

  const [showCompletedMilestones, setShowCompletedMilestones] = useState<boolean>(false);
  const [showCompletedStepsByMilestone, setShowCompletedStepsByMilestone] = useState<Record<string, boolean>>({});
  const [showCompletedItemsByStep, setShowCompletedItemsByStep] = useState<Record<string, boolean>>({});

  const [showStepCreatorByMilestone, setShowStepCreatorByMilestone] = useState<Record<string, boolean>>({});
  const [showItemCreatorByStep, setShowItemCreatorByStep] = useState<Record<string, boolean>>({});

  const [attachmentModal, setAttachmentModal] = useState<AttachmentModalState | null>(null);
  const [attachmentModalFile, setAttachmentModalFile] = useState<File | null>(null);

  useEffect(() => {
    void refreshProjects();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!selectedProjectId) {
      setProject(null);
      return;
    }

    void loadProject(selectedProjectId);
  }, [selectedProjectId]);

  const orderedMilestones = useMemo(() => {
    return project?.milestones ?? [];
  }, [project]);

  const activeMilestones = useMemo(() => splitByCompletion(orderedMilestones).active, [orderedMilestones]);
  const completedMilestones = useMemo(() => splitByCompletion(orderedMilestones).completed, [orderedMilestones]);

  const modalContext = useMemo(() => {
    if (!project || !attachmentModal) {
      return null;
    }

    const milestone = project.milestones.find((candidate) => candidate.id === attachmentModal.milestoneId);
    const step = milestone?.steps.find((candidate) => candidate.id === attachmentModal.stepId);
    const item = step?.items.find((candidate) => candidate.id === attachmentModal.itemId);

    if (!milestone || !step || !item) {
      return null;
    }

    return {
      milestone,
      step,
      item
    };
  }, [attachmentModal, project]);

  async function refreshProjects(preferredProjectId?: string): Promise<void> {
    setLoadingList(true);
    try {
      const payload = await getProjects();
      setProjects(payload.projects);
      setMessage(null);

      if (payload.projects.length === 0) {
        setSelectedProjectId(null);
        setProject(null);
        return;
      }

      const current = preferredProjectId ?? selectedProjectId;
      if (current && payload.projects.some((candidate) => candidate.id === current)) {
        setSelectedProjectId(current);
        return;
      }

      setSelectedProjectId(payload.projects[0].id);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to load projects."));
    } finally {
      setLoadingList(false);
    }
  }

  async function loadProject(projectId: string): Promise<void> {
    setLoadingDetail(true);
    try {
      const payload = await getProject(projectId);
      setProject(payload);
      setMessage(null);

      setExpandedMilestones((current) => {
        const next: Record<string, boolean> = { ...current };
        for (const milestone of payload.milestones) {
          if (next[milestone.id] === undefined) {
            next[milestone.id] = milestone.completionStatus === "ACTIVE";
          }
        }

        return next;
      });

      setExpandedSteps((current) => {
        const next: Record<string, boolean> = { ...current };
        for (const milestone of payload.milestones) {
          for (const step of milestone.steps) {
            if (next[step.id] === undefined) {
              next[step.id] = step.completionStatus === "ACTIVE";
            }
          }
        }

        return next;
      });
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setProject(null);
      setMessage(toUserFeedback(error, "Unable to load project details."));
    } finally {
      setLoadingDetail(false);
    }
  }

  async function applyProjectMutation(
    action: () => Promise<ProjectDetailResponse>,
    fallbackMessage: string
  ): Promise<void> {
    setSaving(true);
    try {
      const next = await action();
      setProject(next);
      setSelectedProjectId(next.id);
      setMessage(null);
      await refreshProjects(next.id);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, fallbackMessage));
    } finally {
      setSaving(false);
    }
  }

  async function onCreateProject(): Promise<void> {
    if (!projectName.trim()) {
      setMessage({ message: "Project name is required." });
      return;
    }

    await applyProjectMutation(
      () =>
        createProject({
          name: projectName.trim(),
          description: projectDescription.trim(),
          currency: projectCurrency,
          sortOrder: 0
        }),
      "Unable to create project."
    );

    setProjectName("");
    setProjectDescription("");
  }

  async function onArchiveProject(projectId: string): Promise<void> {
    setSaving(true);
    try {
      await archiveProject(projectId);
      setProject(null);
      await refreshProjects();
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to archive project."));
    } finally {
      setSaving(false);
    }
  }

  async function onAddMilestone(): Promise<void> {
    if (!project || !milestoneName.trim()) {
      return;
    }

    const sortOrder = Number.parseInt(milestoneSortOrder, 10);

    await applyProjectMutation(
      () =>
        createMilestone(project.id, {
          name: milestoneName.trim(),
          sortOrder: Number.isFinite(sortOrder) ? sortOrder : 0
        }),
      "Unable to add milestone."
    );

    setMilestoneName("");
    setMilestoneSortOrder("0");
  }

  async function onToggleMilestoneDone(milestone: ProjectMilestone): Promise<void> {
    if (!project) {
      return;
    }

    const nextStatus = milestone.completionStatus === "DONE" ? "ACTIVE" : "DONE";
    await applyProjectMutation(
      () =>
        updateMilestoneCompletion(project.id, milestone.id, {
          source: "MANUAL",
          status: nextStatus
        }),
      "Unable to update milestone completion."
    );
  }

  async function onDeleteMilestone(milestone: ProjectMilestone): Promise<void> {
    if (!project) {
      return;
    }

    setSaving(true);
    try {
      await deleteMilestone(project.id, milestone.id);
      await loadProject(project.id);
      await refreshProjects(project.id);
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to delete milestone."));
    } finally {
      setSaving(false);
    }
  }

  function readStepDraft(milestoneId: string): DraftStep {
    return stepDrafts[milestoneId] ?? {
      name: "",
      sortOrder: "0"
    };
  }

  function readItemDraft(stepId: string): DraftItem {
    return itemDrafts[stepId] ?? {
      name: "",
      plannedAmount: "",
      manualAdjustment: "",
      sortOrder: ""
    };
  }

  function readPaymentDraft(itemId: string): DraftPayment {
    return paymentDrafts[itemId] ?? {
      amount: "0",
      note: ""
    };
  }

  async function onAddStep(milestone: ProjectMilestone): Promise<void> {
    if (!project) {
      return;
    }

    const draft = readStepDraft(milestone.id);
    if (!draft.name.trim()) {
      return;
    }

    const sortOrder = Number.parseInt(draft.sortOrder, 10);

    await applyProjectMutation(
      () =>
        createStep(project.id, milestone.id, {
          name: draft.name.trim(),
          sortOrder: Number.isFinite(sortOrder) ? sortOrder : 0
        }),
      "Unable to add step."
    );

    setStepDrafts((current) => ({
      ...current,
      [milestone.id]: {
        name: "",
        sortOrder: "0"
      }
    }));
    setShowStepCreatorByMilestone((current) => ({
      ...current,
      [milestone.id]: false
    }));
    setExpandedMilestones((current) => ({
      ...current,
      [milestone.id]: true
    }));
  }

  async function onToggleStepDone(milestone: ProjectMilestone, step: ProjectStep): Promise<void> {
    if (!project) {
      return;
    }

    const nextStatus = step.completionStatus === "DONE" ? "ACTIVE" : "DONE";

    await applyProjectMutation(
      () =>
        updateStepCompletion(project.id, milestone.id, step.id, {
          source: "MANUAL",
          status: nextStatus
        }),
      "Unable to update step completion."
    );
  }

  async function onDeleteStep(milestone: ProjectMilestone, step: ProjectStep): Promise<void> {
    if (!project) {
      return;
    }

    setSaving(true);
    try {
      await deleteStep(project.id, milestone.id, step.id);
      await loadProject(project.id);
      await refreshProjects(project.id);
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to delete step."));
    } finally {
      setSaving(false);
    }
  }

  async function onAddItem(milestone: ProjectMilestone, step: ProjectStep): Promise<void> {
    if (!project) {
      return;
    }

    const draft = readItemDraft(step.id);
    if (!draft.name.trim()) {
      return;
    }

    const plannedAmount = Number.parseFloat(draft.plannedAmount);
    const manualAdjustment = Number.parseFloat(draft.manualAdjustment);
    const sortOrder = Number.parseInt(draft.sortOrder, 10);

    await applyProjectMutation(
      () =>
        createItem(project.id, milestone.id, step.id, {
          name: draft.name.trim(),
          plannedAmount: Number.isFinite(plannedAmount) ? plannedAmount : 0,
          manualAdjustment: Number.isFinite(manualAdjustment) ? manualAdjustment : 0,
          sortOrder: Number.isFinite(sortOrder) ? sortOrder : 0,
          isDone: false
        }),
      "Unable to add item."
    );

    setItemDrafts((current) => ({
      ...current,
      [step.id]: {
        name: "",
        plannedAmount: "",
        manualAdjustment: "",
        sortOrder: ""
      }
    }));
    setShowItemCreatorByStep((current) => ({
      ...current,
      [step.id]: false
    }));
    setExpandedSteps((current) => ({
      ...current,
      [step.id]: true
    }));
  }

  async function onToggleItemDone(milestone: ProjectMilestone, step: ProjectStep, item: ProjectItem): Promise<void> {
    if (!project) {
      return;
    }

    await applyProjectMutation(
      () =>
        updateItem(project.id, milestone.id, step.id, item.id, {
          isDone: !item.isDone
        }),
      "Unable to update item status."
    );
  }

  async function onDeleteItem(milestone: ProjectMilestone, step: ProjectStep, item: ProjectItem): Promise<void> {
    if (!project) {
      return;
    }

    setSaving(true);
    try {
      await deleteItem(project.id, milestone.id, step.id, item.id);
      await loadProject(project.id);
      await refreshProjects(project.id);
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to delete item."));
    } finally {
      setSaving(false);
    }
  }

  async function onAddPayment(milestone: ProjectMilestone, step: ProjectStep, item: ProjectItem): Promise<void> {
    if (!project) {
      return;
    }

    const draft = readPaymentDraft(item.id);
    const amount = Number.parseFloat(draft.amount);
    if (!Number.isFinite(amount) || amount <= 0) {
      setMessage({ message: "Payment amount must be greater than 0." });
      return;
    }

    await applyProjectMutation(
      () =>
        createPayment(project.id, milestone.id, step.id, item.id, {
          amount,
          note: draft.note.trim()
        }),
      "Unable to add payment."
    );

    setPaymentDrafts((current) => ({
      ...current,
      [item.id]: {
        amount: "0",
        note: ""
      }
    }));
  }

  async function onDeletePayment(milestone: ProjectMilestone, step: ProjectStep, item: ProjectItem, paymentId: string): Promise<void> {
    if (!project) {
      return;
    }

    setSaving(true);
    try {
      await deletePayment(project.id, milestone.id, step.id, item.id, paymentId);
      await loadProject(project.id);
      await refreshProjects(project.id);
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to delete payment."));
    } finally {
      setSaving(false);
    }
  }

  async function onUploadAttachment(
    milestone: ProjectMilestone,
    step: ProjectStep,
    item: ProjectItem,
    file: File
  ): Promise<void> {
    if (!project) {
      return;
    }

    const kind = attachmentKindDrafts[item.id] ?? "RECEIPT";

    setSaving(true);
    try {
      await uploadAttachment(project.id, milestone.id, step.id, item.id, {
        kind,
        file
      });

      await loadProject(project.id);
      await refreshProjects(project.id);
      setAttachmentModalFile(null);
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to upload attachment."));
    } finally {
      setSaving(false);
    }
  }

  async function onRemoveAttachment(
    milestone: ProjectMilestone,
    step: ProjectStep,
    item: ProjectItem,
    attachmentId: string
  ): Promise<void> {
    if (!project) {
      return;
    }

    setSaving(true);
    try {
      await removeAttachment(project.id, milestone.id, step.id, item.id, attachmentId);
      await loadProject(project.id);
      await refreshProjects(project.id);
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to remove attachment."));
    } finally {
      setSaving(false);
    }
  }

  function toggleMilestoneExpansion(milestoneId: string): void {
    setExpandedMilestones((current) => ({
      ...current,
      [milestoneId]: !current[milestoneId]
    }));
  }

  function toggleStepExpansion(stepId: string): void {
    setExpandedSteps((current) => ({
      ...current,
      [stepId]: !current[stepId]
    }));
  }

  function toggleItemDetails(itemId: string): void {
    setExpandedItems((current) => ({
      ...current,
      [itemId]: !current[itemId]
    }));
  }

  function openAttachmentModal(milestoneId: string, stepId: string, itemId: string): void {
    setAttachmentModal({
      milestoneId,
      stepId,
      itemId
    });
    setAttachmentModalFile(null);
    setExpandedItems((current) => ({
      ...current,
      [itemId]: true
    }));
  }

  function closeAttachmentModal(): void {
    setAttachmentModal(null);
    setAttachmentModalFile(null);
  }

  async function onUploadFromModal(): Promise<void> {
    if (!modalContext || !attachmentModalFile) {
      return;
    }

    await onUploadAttachment(modalContext.milestone, modalContext.step, modalContext.item, attachmentModalFile);
  }

  function renderHierarchyLabel(options: {
    level: 0 | 1 | 2 | 3;
    text: string;
    canExpand?: boolean;
    expanded?: boolean;
    onToggle?: () => void;
    subtitle?: string;
  }) {
    const { level, text, canExpand, expanded, onToggle, subtitle } = options;

    return (
      <div className={`flex flex-col gap-0.5 ${hierarchyCellClass(level)}`}>
        <div className="flex items-center gap-2">
          {canExpand ? (
            <button
              type="button"
              className="grid h-6 w-6 place-items-center rounded border border-[#d0d5dd] text-xs text-[#344054]"
              onClick={onToggle}
              aria-label={expanded ? "Collapse row" : "Expand row"}
              title={expanded ? "Collapse" : "Expand"}
            >
              {expanded ? "▾" : "▸"}
            </button>
          ) : (
            <span className="inline-block h-6 w-6" />
          )}
          <span className="text-sm font-medium text-[#111827] md:text-base">{text}</span>
        </div>
        {subtitle ? <span className="pl-8 text-xs text-[#667085]">{subtitle}</span> : null}
      </div>
    );
  }

  function renderMilestoneRows(milestone: ProjectMilestone): ReactElement[] {
    const rows: ReactElement[] = [];
    const isExpanded = expandedMilestones[milestone.id] ?? false;
    const { active: activeSteps, completed: completedSteps } = splitByCompletion(milestone.steps);
    const showCompletedSteps = showCompletedStepsByMilestone[milestone.id] ?? false;

    rows.push(
      <tr key={`milestone-${milestone.id}`} className="border-b border-[#d7dde6] bg-[#f8fafc]">
        <td className="py-3 align-top">
          {renderHierarchyLabel({
            level: 1,
            text: milestone.name,
            canExpand: milestone.steps.length > 0,
            expanded: isExpanded,
            onToggle: () => toggleMilestoneExpansion(milestone.id)
          })}
        </td>
        <td className="px-2 py-3 text-right text-sm text-[#344054]">{formatCurrency(milestone.totals.planned, project!.currency)}</td>
        <td className="px-2 py-3 text-right text-sm text-[#344054]">{formatCurrency(milestone.totals.actual, project!.currency)}</td>
        <td className={`px-2 py-3 text-right text-sm ${varianceClass(milestone.totals.variance)}`}>{formatCurrency(milestone.totals.variance, project!.currency)}</td>
        <td className="px-2 py-3 text-center text-sm text-[#475467]">{milestone.completion.openItems}</td>
        <td className="px-2 py-3 text-center">
          <button
            type="button"
            disabled={saving}
            onClick={() => void onToggleMilestoneDone(milestone)}
            className={`rounded-lg border px-2.5 py-1 text-xs font-medium ${statusTagClass(milestone.completionStatus)}`}
          >
            {milestone.completionStatus}
          </button>
        </td>
        <td className="px-2 py-3 text-sm text-[#667085]">
          {milestone.completion.doneItems} / {milestone.completion.doneItems + milestone.completion.openItems} items done
        </td>
        <td className="px-2 py-3">
          <div className="flex flex-wrap justify-end gap-2">
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-8 w-8 rounded-md p-0"
              disabled={saving}
              aria-label="New step"
              title="New step"
              onClick={() =>
                setShowStepCreatorByMilestone((current) => ({
                  ...current,
                  [milestone.id]: !current[milestone.id]
                }))
              }
            >
              <PlusIcon size={14} />
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-8 w-8 rounded-md border-[#efc8d1] bg-[#fff5f7] p-0 text-[#b42342] hover:bg-[#ffeef3]"
              disabled={saving}
              aria-label="Delete milestone"
              title="Delete milestone"
              onClick={() => void onDeleteMilestone(milestone)}
            >
              <TrashIcon size={14} />
            </Button>
          </div>
        </td>
      </tr>
    );

    if (!isExpanded) {
      return rows;
    }

    if (showStepCreatorByMilestone[milestone.id]) {
      const stepDraft = readStepDraft(milestone.id);

      rows.push(
        <tr key={`milestone-${milestone.id}-step-creator`} className="border-b border-[#e4e7ec] bg-[#fcfdff]">
          <td colSpan={8} className="py-2">
            <div className="ml-12 flex flex-wrap items-center gap-2 rounded-lg border border-[#d8dee9] bg-white p-2">
              <Input
                className="h-9 min-w-[200px] flex-1"
                value={stepDraft.name}
                onChange={(event) =>
                  setStepDrafts((current) => ({
                    ...current,
                    [milestone.id]: {
                      ...stepDraft,
                      name: event.target.value
                    }
                  }))
                }
                placeholder="Step name"
              />
              <Input
                className="h-9 w-[90px]"
                type="number"
                value={stepDraft.sortOrder}
                onChange={(event) =>
                  setStepDrafts((current) => ({
                    ...current,
                    [milestone.id]: {
                      ...stepDraft,
                      sortOrder: event.target.value
                    }
                  }))
                }
                placeholder="Sort"
              />
              <Button type="button" className="h-9" disabled={saving} onClick={() => void onAddStep(milestone)}>
                Save step
              </Button>
            </div>
          </td>
        </tr>
      );
    }

    for (const step of activeSteps) {
      rows.push(...renderStepRows(milestone, step));
    }

    if (completedSteps.length > 0) {
      rows.push(
        <tr key={`milestone-${milestone.id}-completed-steps-toggle`} className="border-b border-[#e4e7ec] bg-[#f9fafb]">
          <td colSpan={8} className="py-2">
            <button
              type="button"
              className="ml-12 text-xs font-medium text-[#475467] underline"
              onClick={() =>
                setShowCompletedStepsByMilestone((current) => ({
                  ...current,
                  [milestone.id]: !showCompletedSteps
                }))
              }
            >
              {showCompletedSteps
                ? "Hide completed steps"
                : `Show completed steps (${completedSteps.length})`}
            </button>
          </td>
        </tr>
      );

      if (showCompletedSteps) {
        for (const step of completedSteps) {
          rows.push(...renderStepRows(milestone, step));
        }
      }
    }

    return rows;
  }

  function renderStepRows(milestone: ProjectMilestone, step: ProjectStep): ReactElement[] {
    const rows: ReactElement[] = [];
    const isExpanded = expandedSteps[step.id] ?? false;
    const { active: activeItems, completed: completedItems } = splitByCompletion(step.items);
    const showCompletedItems = showCompletedItemsByStep[step.id] ?? false;

    rows.push(
      <tr key={`step-${step.id}`} className="border-b border-[#e4e7ec] bg-white">
        <td className="py-3 align-top">
          {renderHierarchyLabel({
            level: 2,
            text: step.name,
            canExpand: step.items.length > 0,
            expanded: isExpanded,
            onToggle: () => toggleStepExpansion(step.id)
          })}
        </td>
        <td className="px-2 py-3 text-right text-sm text-[#344054]">{formatCurrency(step.totals.planned, project!.currency)}</td>
        <td className="px-2 py-3 text-right text-sm text-[#344054]">{formatCurrency(step.totals.actual, project!.currency)}</td>
        <td className={`px-2 py-3 text-right text-sm ${varianceClass(step.totals.variance)}`}>{formatCurrency(step.totals.variance, project!.currency)}</td>
        <td className="px-2 py-3 text-center text-sm text-[#475467]">{step.completion.openItems}</td>
        <td className="px-2 py-3 text-center">
          <button
            type="button"
            disabled={saving}
            onClick={() => void onToggleStepDone(milestone, step)}
            className={`rounded-lg border px-2.5 py-1 text-xs font-medium ${statusTagClass(step.completionStatus)}`}
          >
            {step.completionStatus}
          </button>
        </td>
        <td className="px-2 py-3 text-sm text-[#667085]">
          {step.items.length} items / {step.items.reduce((acc, item) => acc + item.attachments.length, 0)} docs
        </td>
        <td className="px-2 py-3">
          <div className="flex flex-wrap justify-end gap-2">
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-8 w-8 rounded-md p-0"
              disabled={saving}
              aria-label="New item"
              title="New item"
              onClick={() =>
                setShowItemCreatorByStep((current) => ({
                  ...current,
                  [step.id]: !current[step.id]
                }))
              }
            >
              <PlusIcon size={14} />
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-8 w-8 rounded-md border-[#efc8d1] bg-[#fff5f7] p-0 text-[#b42342] hover:bg-[#ffeef3]"
              disabled={saving}
              aria-label="Delete step"
              title="Delete step"
              onClick={() => void onDeleteStep(milestone, step)}
            >
              <TrashIcon size={14} />
            </Button>
          </div>
        </td>
      </tr>
    );

    if (!isExpanded) {
      return rows;
    }

    if (showItemCreatorByStep[step.id]) {
      const itemDraft = readItemDraft(step.id);

      rows.push(
        <tr key={`step-${step.id}-item-creator`} className="border-b border-[#e4e7ec] bg-[#fcfdff]">
          <td colSpan={8} className="py-2">
            <div className="ml-16 flex flex-wrap items-center gap-2 rounded-lg border border-[#d8dee9] bg-white p-2">
              <Input
                className="h-9 min-w-[220px] flex-1"
                value={itemDraft.name}
                onChange={(event) =>
                  setItemDrafts((current) => ({
                    ...current,
                    [step.id]: {
                      ...itemDraft,
                      name: event.target.value
                    }
                  }))
                }
                placeholder="Item name"
              />
              <Input
                className="h-9 w-[110px]"
                type="number"
                step="0.01"
                value={itemDraft.plannedAmount}
                onChange={(event) =>
                  setItemDrafts((current) => ({
                    ...current,
                    [step.id]: {
                      ...itemDraft,
                      plannedAmount: event.target.value
                    }
                  }))
                }
                placeholder="Planned"
              />
              <Input
                className="h-9 w-[120px]"
                type="number"
                step="0.01"
                value={itemDraft.manualAdjustment}
                onChange={(event) =>
                  setItemDrafts((current) => ({
                    ...current,
                    [step.id]: {
                      ...itemDraft,
                      manualAdjustment: event.target.value
                    }
                  }))
                }
                placeholder="Adjustment"
              />
              <Input
                className="h-9 w-[90px]"
                type="number"
                value={itemDraft.sortOrder}
                onChange={(event) =>
                  setItemDrafts((current) => ({
                    ...current,
                    [step.id]: {
                      ...itemDraft,
                      sortOrder: event.target.value
                    }
                  }))
                }
                placeholder="Sort"
              />
              <Button type="button" className="h-9" disabled={saving} onClick={() => void onAddItem(milestone, step)}>
                Save item
              </Button>
            </div>
          </td>
        </tr>
      );
    }

    for (const item of activeItems) {
      rows.push(...renderItemRows(milestone, step, item));
    }

    if (completedItems.length > 0) {
      rows.push(
        <tr key={`step-${step.id}-completed-items-toggle`} className="border-b border-[#e4e7ec] bg-[#f9fafb]">
          <td colSpan={8} className="py-2">
            <button
              type="button"
              className="ml-16 text-xs font-medium text-[#475467] underline"
              onClick={() =>
                setShowCompletedItemsByStep((current) => ({
                  ...current,
                  [step.id]: !showCompletedItems
                }))
              }
            >
              {showCompletedItems
                ? "Hide completed items"
                : `Show completed items (${completedItems.length})`}
            </button>
          </td>
        </tr>
      );

      if (showCompletedItems) {
        for (const item of completedItems) {
          rows.push(...renderItemRows(milestone, step, item));
        }
      }
    }

    return rows;
  }

  function renderItemRows(milestone: ProjectMilestone, step: ProjectStep, item: ProjectItem): ReactElement[] {
    const rows: ReactElement[] = [];
    const isDetailsOpen = expandedItems[item.id] ?? false;

    rows.push(
      <tr key={`item-${item.id}`} className="border-b border-[#e4e7ec] bg-[#fcfdff]">
        <td className="py-3 align-top">
          {renderHierarchyLabel({
            level: 3,
            text: item.name,
            canExpand: true,
            expanded: isDetailsOpen,
            onToggle: () => toggleItemDetails(item.id),
            subtitle: `${item.payments.length} payments | ${item.attachments.length} docs`
          })}
        </td>
        <td className="px-2 py-3 text-right text-sm text-[#344054]">{formatCurrency(item.plannedAmount, project!.currency)}</td>
        <td className="px-2 py-3 text-right text-sm text-[#344054]">{formatCurrency(item.actualAmount, project!.currency)}</td>
        <td className={`px-2 py-3 text-right text-sm ${varianceClass(item.variance)}`}>{formatCurrency(item.variance, project!.currency)}</td>
        <td className="px-2 py-3 text-center text-sm text-[#475467]">{item.isDone ? 0 : 1}</td>
        <td className="px-2 py-3 text-center">
          <button
            type="button"
            disabled={saving}
            onClick={() => void onToggleItemDone(milestone, step, item)}
            className={`rounded-lg border px-2.5 py-1 text-xs font-medium ${statusTagClass(item.isDone ? "DONE" : "ACTIVE")}`}
          >
            {item.isDone ? "DONE" : "ACTIVE"}
          </button>
        </td>
        <td className="px-2 py-3">
          <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-[#475467]">
            <span>{item.payments.length} payments</span>
            <span>{item.attachments.length} docs</span>
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-7"
              onClick={() => openAttachmentModal(milestone.id, step.id, item.id)}
              disabled={saving}
            >
              Attachments
            </Button>
          </div>
        </td>
        <td className="px-2 py-3">
          <div className="flex flex-wrap justify-end gap-2">
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-8 w-8 rounded-md p-0"
              aria-label={isDetailsOpen ? "Hide payments and attachments" : "Show payments and attachments"}
              title={isDetailsOpen ? "Hide payments and attachments" : "Show payments and attachments"}
              onClick={() => toggleItemDetails(item.id)}
            >
              <PaperclipIcon size={14} />
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-8 w-8 rounded-md border-[#efc8d1] bg-[#fff5f7] p-0 text-[#b42342] hover:bg-[#ffeef3]"
              disabled={saving}
              aria-label="Delete item"
              title="Delete item"
              onClick={() => void onDeleteItem(milestone, step, item)}
            >
              <TrashIcon size={14} />
            </Button>
          </div>
        </td>
      </tr>
    );

    if (!isDetailsOpen) {
      return rows;
    }

    const paymentDraft = readPaymentDraft(item.id);

    rows.push(
      <tr key={`item-${item.id}-details`} className="border-b border-[#e4e7ec] bg-white">
        <td colSpan={8} className="py-3">
          <div className="ml-16 rounded-xl border border-[#d8dee9] bg-[#f9fbff] p-3">
            <h4 className="text-sm font-semibold text-[#111827]">Payments & Attachments</h4>
            <p className="text-xs text-[#667085]">
              Add payments inline and manage documents from one interaction row.
            </p>

            <div className="mt-3 flex flex-wrap items-center gap-2">
              <Input
                className="h-9 w-[130px]"
                type="number"
                step="0.01"
                value={paymentDraft.amount}
                onChange={(event) =>
                  setPaymentDrafts((current) => ({
                    ...current,
                    [item.id]: {
                      ...paymentDraft,
                      amount: event.target.value
                    }
                  }))
                }
                placeholder="Amount"
              />
              <Input
                className="h-9 min-w-[220px] flex-1"
                value={paymentDraft.note}
                onChange={(event) =>
                  setPaymentDrafts((current) => ({
                    ...current,
                    [item.id]: {
                      ...paymentDraft,
                      note: event.target.value
                    }
                  }))
                }
                placeholder="Payment note"
              />
              <Button type="button" className="h-9" disabled={saving} onClick={() => void onAddPayment(milestone, step, item)}>
                Add payment
              </Button>
              <Button
                type="button"
                variant="outline"
                className="h-9"
                disabled={saving}
                onClick={() => openAttachmentModal(milestone.id, step.id, item.id)}
              >
                Open attachments modal
              </Button>
            </div>

            <div className="mt-3 grid gap-2 md:grid-cols-2">
              <div className="rounded-lg border border-[#d7dde6] bg-white p-2">
                <p className="text-xs font-medium text-[#1f2937]">Recent payments</p>
                <div className="mt-2 space-y-1">
                  {item.payments.length === 0 ? (
                    <p className="text-xs text-[#667085]">No payments yet.</p>
                  ) : (
                    item.payments.map((payment) => (
                      <div key={payment.id} className="flex items-center justify-between gap-2 text-xs text-[#344054]">
                        <span>
                          {new Date(payment.paymentDate).toLocaleDateString()} - {formatCurrency(payment.amount, project!.currency)} {payment.note ? `(${payment.note})` : ""}
                        </span>
                        <button
                          type="button"
                          className="rounded border border-[#efc8d1] bg-[#fff5f7] px-2 py-0.5 text-[#b42342]"
                          onClick={() => void onDeletePayment(milestone, step, item, payment.id)}
                          disabled={saving}
                        >
                          Delete
                        </button>
                      </div>
                    ))
                  )}
                </div>
              </div>

              <div className="rounded-lg border border-[#d7dde6] bg-white p-2">
                <p className="text-xs font-medium text-[#1f2937]">Attachment summary</p>
                <div className="mt-2 space-y-1">
                  {item.attachments.length === 0 ? (
                    <p className="text-xs text-[#667085]">No active attachments.</p>
                  ) : (
                    item.attachments.slice(0, 4).map((attachment) => (
                      <p key={attachment.id} className="text-xs text-[#344054]">
                        {attachment.kind}: {attachment.originalName}
                      </p>
                    ))
                  )}
                </div>
              </div>
            </div>
          </div>
        </td>
      </tr>
    );

    return rows;
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="ui-text-strong text-2xl font-semibold tracking-[-0.02em] md:text-3xl">Projects</h1>
        <p className="ui-text-muted text-base">Plan major projects with milestones, steps, costs, payments, and key documents.</p>
      </header>

      {message ? (
        <p className="ui-text-muted text-sm md:text-base">
          {message.message}
          {message.details ? ` ${message.details}` : ""}
        </p>
      ) : null}

      <section className={`grid gap-4 ${isProjectNavCollapsed ? "xl:grid-cols-[74px_minmax(0,1fr)]" : "xl:grid-cols-[320px_minmax(0,1fr)]"}`}>
        <aside className={`space-y-3 rounded-2xl border border-[#ccd3dc] bg-white transition-all ${isProjectNavCollapsed ? "p-2" : "p-4"}`}>
          <div className="flex items-center justify-between gap-2">
            {!isProjectNavCollapsed ? <h2 className="text-base font-semibold text-[#101827]">Projects list</h2> : null}
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-8 px-2"
              onClick={() => setIsProjectNavCollapsed((current) => !current)}
            >
              {isProjectNavCollapsed ? "→" : "←"}
            </Button>
          </div>

          {isProjectNavCollapsed ? (
            <div className="space-y-2">
              {projects.map((entry, index) => (
                <button
                  key={`collapsed-${entry.id}`}
                  type="button"
                  onClick={() => setSelectedProjectId(entry.id)}
                  className={`grid h-10 w-full place-items-center rounded-lg border text-xs font-semibold ${selectedProjectId === entry.id ? "border-[#3b82f6] bg-[#eff6ff] text-[#1d4ed8]" : "border-[#d0d6df] bg-white text-[#344054]"}`}
                  title={entry.name}
                >
                  {entry.name.trim().charAt(0).toUpperCase() || String(index + 1)}
                </button>
              ))}
            </div>
          ) : (
            <>
              <div className="space-y-2">
                {loadingList ? <p className="text-sm text-[#667085]">Loading projects...</p> : null}
                {!loadingList && projects.length === 0 ? <p className="text-sm text-[#667085]">No projects yet.</p> : null}
                {projects.map((entry) => (
                  <button
                    key={entry.id}
                    type="button"
                    onClick={() => setSelectedProjectId(entry.id)}
                    className={`w-full rounded-xl border px-3 py-2 text-left ${selectedProjectId === entry.id ? "border-[#3b82f6] bg-[#eff6ff]" : "border-[#d0d6df] bg-white"}`}
                  >
                    <p className="text-sm font-medium text-[#111827]">{entry.name}</p>
                    <p className="text-xs text-[#667085]">
                      Actual: {formatCurrency(entry.totals.actual, entry.currency)} | Open items: {entry.completion.openItems}
                    </p>
                  </button>
                ))}
              </div>

              <div className="rounded-xl border border-[#d6dbe3] bg-[#f9fafb] p-3">
                <p className="text-xs font-semibold text-[#334155]">Create project</p>
                <div className="mt-2 space-y-2">
                  <Input value={projectName} onChange={(event) => setProjectName(event.target.value)} placeholder="Project name" />
                  <Input value={projectDescription} onChange={(event) => setProjectDescription(event.target.value)} placeholder="Description" />
                  <select
                    className="h-10 w-full rounded-md border border-[#d0d5dd] bg-white px-3 text-sm"
                    value={projectCurrency}
                    onChange={(event) => setProjectCurrency(event.target.value as CurrencyCode)}
                  >
                    {CURRENCIES.map((currency) => (
                      <option key={currency} value={currency}>
                        {currency}
                      </option>
                    ))}
                  </select>
                  <Button type="button" className="w-full" disabled={saving} onClick={() => void onCreateProject()}>
                    Create project
                  </Button>
                </div>
              </div>
            </>
          )}
        </aside>

        <section className="space-y-3 rounded-2xl border border-[#ccd3dc] bg-white p-4">
          {loadingDetail ? <p className="text-sm text-[#667085]">Loading project...</p> : null}

          {!loadingDetail && !project ? (
            <p className="text-sm text-[#667085]">Select a project from the list.</p>
          ) : null}

          {project ? (
            <>
              <div className="flex flex-wrap items-start justify-between gap-3 rounded-xl border border-[#d6dbe3] bg-[#f8fafc] p-3">
                <div>
                  <h2 className="text-xl font-semibold text-[#111827]">{project.name}</h2>
                  <p className="text-sm text-[#667085]">{project.description || "No description"}</p>
                  <p className="mt-1 text-xs text-[#667085]">
                    Planned: {formatCurrency(project.totals.planned, project.currency)} | Actual: {formatCurrency(project.totals.actual, project.currency)} | Variance: {formatCurrency(project.totals.variance, project.currency)}
                  </p>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  className="border-[#efc8d1] bg-[#fff5f7] text-[#b42342] hover:bg-[#ffeef3]"
                  disabled={saving}
                  onClick={() => void onArchiveProject(project.id)}
                >
                  Archive project
                </Button>
              </div>

              <div className="flex flex-wrap items-center gap-2 rounded-xl border border-[#d6dbe3] bg-white p-3">
                <Input
                  className="min-w-[220px] flex-1"
                  value={milestoneName}
                  onChange={(event) => setMilestoneName(event.target.value)}
                  placeholder="New milestone"
                />
                <Input
                  className="w-[100px]"
                  type="number"
                  value={milestoneSortOrder}
                  onChange={(event) => setMilestoneSortOrder(event.target.value)}
                  placeholder="Sort"
                />
                <Button type="button" disabled={saving} onClick={() => void onAddMilestone()}>
                  Add milestone
                </Button>
              </div>

              <div className="overflow-x-auto rounded-xl border border-[#d7dde6]">
                <table className="min-w-[1080px] w-full border-collapse">
                  <thead>
                    <tr className="bg-[#f2f4f7] text-left text-sm font-semibold text-[#1f2937]">
                      <th className="w-[37%] border-b border-[#d7dde6] px-4 py-3">Project</th>
                      <th className="w-[10%] border-b border-[#d7dde6] px-2 py-3 text-right">Planned</th>
                      <th className="w-[10%] border-b border-[#d7dde6] px-2 py-3 text-right">Actual</th>
                      <th className="w-[10%] border-b border-[#d7dde6] px-2 py-3 text-right">Variance</th>
                      <th className="w-[8%] border-b border-[#d7dde6] px-2 py-3 text-center">Open Items</th>
                      <th className="w-[8%] border-b border-[#d7dde6] px-2 py-3 text-center">Status</th>
                      <th className="w-[10%] border-b border-[#d7dde6] px-2 py-3">Payments & Attachments</th>
                      <th className="w-[7%] border-b border-[#d7dde6] px-2 py-3 text-right">Actions</th>
                    </tr>
                  </thead>

                  <tbody>
                    <tr className="border-b border-[#d7dde6] bg-white">
                      <td className="py-3 align-top">
                        {renderHierarchyLabel({
                          level: 0,
                          text: project.name,
                          subtitle: project.description || "No description"
                        })}
                      </td>
                      <td className="px-2 py-3 text-right text-sm text-[#344054]">{formatCurrency(project.totals.planned, project.currency)}</td>
                      <td className="px-2 py-3 text-right text-sm text-[#344054]">{formatCurrency(project.totals.actual, project.currency)}</td>
                      <td className={`px-2 py-3 text-right text-sm ${varianceClass(project.totals.variance)}`}>{formatCurrency(project.totals.variance, project.currency)}</td>
                      <td className="px-2 py-3 text-center text-sm text-[#475467]">{project.completion.openItems}</td>
                      <td className="px-2 py-3 text-center text-sm text-[#475467]">-</td>
                      <td className="px-2 py-3 text-sm text-[#667085]">
                        {project.completion.doneItems} / {project.completion.doneItems + project.completion.openItems} items done
                      </td>
                      <td className="px-2 py-3 text-right text-sm text-[#475467]">-</td>
                    </tr>

                    {activeMilestones.flatMap((milestone) => renderMilestoneRows(milestone))}

                    {completedMilestones.length > 0 ? (
                      <tr className="border-b border-[#d7dde6] bg-[#f9fafb]">
                        <td colSpan={8} className="py-2">
                          <button
                            type="button"
                            className="ml-4 text-sm font-medium text-[#475467] underline"
                            onClick={() => setShowCompletedMilestones((current) => !current)}
                          >
                            {showCompletedMilestones
                              ? "Hide completed milestones"
                              : `Show completed milestones (${completedMilestones.length})`}
                          </button>
                        </td>
                      </tr>
                    ) : null}

                    {showCompletedMilestones
                      ? completedMilestones.flatMap((milestone) => renderMilestoneRows(milestone))
                      : null}
                  </tbody>
                </table>
              </div>
            </>
          ) : null}
        </section>
      </section>

      {attachmentModal && modalContext ? (
        <div className="fixed inset-0 z-50 grid place-items-center bg-[#0b1220]/55 p-4">
          <div className="max-h-[86vh] w-full max-w-3xl overflow-auto rounded-2xl border border-[#d0d5dd] bg-white p-5 shadow-2xl">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h3 className="text-lg font-semibold text-[#101828]">Attachments</h3>
                <p className="text-sm text-[#667085]">
                  {modalContext.milestone.name} / {modalContext.step.name} / {modalContext.item.name}
                </p>
              </div>
              <button
                type="button"
                className="rounded-lg border border-[#d0d5dd] px-3 py-1.5 text-sm text-[#344054]"
                onClick={closeAttachmentModal}
              >
                Close
              </button>
            </div>

            <div className="mt-4 rounded-xl border border-[#d8dee9] bg-[#f8fafc] p-3">
              <p className="text-xs font-semibold text-[#344054]">Upload new attachment</p>
              <div className="mt-2 overflow-x-auto">
                <div className="flex min-w-[620px] items-center gap-2">
                  <select
                    className="h-9 w-[160px] rounded-md border border-[#d0d5dd] bg-white px-2 text-xs"
                    value={attachmentKindDrafts[modalContext.item.id] ?? "RECEIPT"}
                    onChange={(event) =>
                      setAttachmentKindDrafts((current) => ({
                        ...current,
                        [modalContext.item.id]: event.target.value as ProjectAttachmentKind
                      }))
                    }
                  >
                    {ATTACHMENT_KINDS.map((kind) => (
                      <option key={`${modalContext.item.id}-modal-${kind}`} value={kind}>
                        {kind}
                      </option>
                    ))}
                  </select>

                  <Input
                    className="h-9 flex-1"
                    type="file"
                    accept="application/pdf,image/jpeg,image/png,image/webp"
                    onChange={(event) => {
                      const nextFile = event.target.files?.[0] ?? null;
                      setAttachmentModalFile(nextFile);
                    }}
                  />

                  <Button
                    type="button"
                    className="h-9 shrink-0"
                    disabled={saving || !attachmentModalFile}
                    onClick={() => void onUploadFromModal()}
                  >
                    Upload
                  </Button>
                </div>
              </div>
            </div>

            <div className="mt-4 rounded-xl border border-[#d8dee9]">
              <table className="w-full border-collapse">
                <thead>
                  <tr className="bg-[#f8fafc] text-left text-xs font-semibold text-[#475467]">
                    <th className="border-b border-[#d8dee9] px-3 py-2">File</th>
                    <th className="border-b border-[#d8dee9] px-3 py-2">Kind</th>
                    <th className="border-b border-[#d8dee9] px-3 py-2">Created</th>
                    <th className="border-b border-[#d8dee9] px-3 py-2">Size</th>
                    <th className="border-b border-[#d8dee9] px-3 py-2 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {modalContext.item.attachments.length === 0 ? (
                    <tr>
                      <td colSpan={5} className="px-3 py-4 text-sm text-[#667085]">
                        No active attachments.
                      </td>
                    </tr>
                  ) : (
                    modalContext.item.attachments.map((attachment) => (
                      <tr key={attachment.id} className="border-b border-[#eef1f4] text-sm">
                        <td className="px-3 py-2 text-[#111827]">{attachment.originalName}</td>
                        <td className="px-3 py-2 text-[#475467]">{attachment.kind}</td>
                        <td className="px-3 py-2 text-[#475467]">{new Date(attachment.createdAt).toLocaleDateString()}</td>
                        <td className="px-3 py-2 text-[#475467]">{Math.max(1, Math.round(attachment.sizeBytes / 1024))} KB</td>
                        <td className="px-3 py-2">
                          <div className="flex justify-end gap-2">
                            <a
                              className="rounded border border-[#c8d0db] px-2 py-1 text-xs text-[#1d4ed8]"
                              href={resolveAttachmentDownloadUrl(project!.id, modalContext.milestone.id, modalContext.step.id, modalContext.item.id, attachment.id)}
                              target="_blank"
                              rel="noreferrer"
                            >
                              Download
                            </a>
                            <button
                              type="button"
                              className="rounded border border-[#efc8d1] bg-[#fff5f7] px-2 py-1 text-xs text-[#b42342]"
                              onClick={() =>
                                void onRemoveAttachment(modalContext.milestone, modalContext.step, modalContext.item, attachment.id)
                              }
                              disabled={saving}
                            >
                              Remove from app
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
