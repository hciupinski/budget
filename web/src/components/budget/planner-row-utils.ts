import type { ManagedSection, ManagedSectionKind } from "@/lib/section-settings";

export type ResolvedSectionMeta = {
  id: string;
  name: string;
  kind: ManagedSectionKind;
  order: number;
  sectionId: string;
};

export function fallbackLabelFromKind(kind: ManagedSectionKind): string {
  if (kind === "INCOME") {
    return "Income";
  }

  if (kind === "BUSINESS_EXPENSES") {
    return "Business Expenses";
  }

  if (kind === "PERSONAL_EXPENSES") {
    return "Personal Expenses";
  }

  if (kind === "SAVINGS") {
    return "Savings";
  }

  return "Investments";
}

export function resolveCustomSection(
  input: { sectionId: string; sectionKind: ManagedSectionKind },
  sections: ManagedSection[]
): ResolvedSectionMeta {
  const byId = sections.find((section) => section.id === input.sectionId);
  if (byId) {
    return {
      id: byId.id,
      name: byId.name,
      kind: byId.kind,
      order: byId.order,
      sectionId: byId.id
    };
  }

  const byKind = sections.find((section) => section.kind === input.sectionKind);
  if (byKind) {
    return {
      id: byKind.id,
      name: byKind.name,
      kind: byKind.kind,
      order: byKind.order,
      sectionId: byKind.id
    };
  }

  return {
    id: `fallback-${input.sectionKind}`,
    name: fallbackLabelFromKind(input.sectionKind),
    kind: input.sectionKind,
    order: 999,
    sectionId: ""
  };
}

export function groupRowsBySection<TRow extends { resolvedSection: ResolvedSectionMeta }>(
  rows: TRow[],
  sectionSettings: ManagedSection[]
): Array<{
  id: string;
  label: string;
  kind: ManagedSectionKind;
  order: number;
  sectionId: string;
  rows: TRow[];
}> {
  const groupedMap = new Map<
    string,
    {
      id: string;
      label: string;
      kind: ManagedSectionKind;
      order: number;
      sectionId: string;
      rows: TRow[];
    }
  >();

  for (const section of [...sectionSettings].sort((a, b) => a.order - b.order)) {
    groupedMap.set(section.id, {
      id: section.id,
      label: section.name,
      kind: section.kind,
      order: section.order,
      sectionId: section.id,
      rows: []
    });
  }

  for (const row of rows) {
    const existing = groupedMap.get(row.resolvedSection.id);

    if (existing) {
      existing.rows.push(row);
      continue;
    }

    groupedMap.set(row.resolvedSection.id, {
      id: row.resolvedSection.id,
      label: row.resolvedSection.name,
      kind: row.resolvedSection.kind,
      order: row.resolvedSection.order,
      sectionId: row.resolvedSection.sectionId,
      rows: [row]
    });
  }

  return Array.from(groupedMap.values()).sort((a, b) => a.order - b.order);
}
