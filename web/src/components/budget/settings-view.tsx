"use client";

import { useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import {
  createSectionDraft,
  readSectionSettings,
  resetSectionSettings,
  saveSectionSettings,
  type ManagedSection,
  type ManagedSectionKind
} from "@/lib/section-settings";
import { PlusIcon } from "@/components/budget/icons";

const KIND_OPTIONS: Array<{ value: ManagedSectionKind; label: string }> = [
  { value: "INCOME", label: "Income" },
  { value: "BUSINESS_EXPENSES", label: "Business Expenses" },
  { value: "PERSONAL_EXPENSES", label: "Personal Expenses" },
  { value: "SAVINGS", label: "Savings" },
  { value: "INVESTMENTS", label: "Investments" }
];

function sectionCardTone(kind: ManagedSectionKind): string {
  if (kind === "INCOME") {
    return "border-[#9bd7b2] bg-[#e8f4ee]";
  }

  if (kind === "BUSINESS_EXPENSES") {
    return "border-[#d9c2f4] bg-[#efebf7]";
  }

  if (kind === "PERSONAL_EXPENSES") {
    return "border-[#efcda8] bg-[#f5efe5]";
  }

  return "border-[#b8ccef] bg-[#e8eef7]";
}

export function SettingsView() {
  const [activeTab, setActiveTab] = useState<"SECTIONS" | "GENERAL">("SECTIONS");
  const [draftSections, setDraftSections] = useState<ManagedSection[]>([]);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    setDraftSections(readSectionSettings());
  }, []);

  function updateSection(sectionId: string, change: Partial<ManagedSection>) {
    setDraftSections((current) =>
      current.map((section) =>
        section.id === sectionId
          ? {
              ...section,
              ...change
            }
          : section
      )
    );
  }

  function removeSection(sectionId: string) {
    setDraftSections((current) => current.filter((section) => section.id !== sectionId));
  }

  function addSection() {
    setDraftSections((current) => [...current, createSectionDraft(current.length)]);
  }

  function moveSection(sectionId: string, direction: -1 | 1) {
    setDraftSections((current) => {
      const index = current.findIndex((section) => section.id === sectionId);
      if (index < 0) {
        return current;
      }

      const nextIndex = index + direction;
      if (nextIndex < 0 || nextIndex >= current.length) {
        return current;
      }

      const next = [...current];
      const [item] = next.splice(index, 1);
      next.splice(nextIndex, 0, item);

      return next.map((section, order) => ({
        ...section,
        order
      }));
    });
  }

  function onSave() {
    if (draftSections.length === 0) {
      setMessage("Add at least one section before saving.");
      return;
    }

    const saved = saveSectionSettings(draftSections);
    setDraftSections(saved);
    setMessage("Section settings saved. Annual and monthly views now use these sections.");
  }

  function onReset() {
    const defaults = resetSectionSettings();
    setDraftSections(defaults);
    setMessage("Section settings reset to defaults.");
  }

  const hasDuplicateNames = useMemo(() => {
    const names = draftSections.map((section) => section.name.trim().toLowerCase()).filter(Boolean);
    return new Set(names).size !== names.length;
  }, [draftSections]);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-3xl md:text-4xl font-semibold tracking-[-0.02em] text-[#0f1321]">Settings</h1>
        <p className="text-lg md:text-xl text-[#71768b]">Manage budget configuration and section rules</p>
      </header>

      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-4">
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => setActiveTab("SECTIONS")}
            className={`h-11 rounded-2xl px-5 text-sm md:text-base ${
              activeTab === "SECTIONS"
                ? "bg-[#040426] text-white"
                : "border border-[#d1d5dd] bg-[#f3f4f6] text-[#1d2230]"
            }`}
          >
            Sections
          </button>

          <button
            type="button"
            onClick={() => setActiveTab("GENERAL")}
            className={`h-11 rounded-2xl px-5 text-sm md:text-base ${
              activeTab === "GENERAL"
                ? "bg-[#040426] text-white"
                : "border border-[#d1d5dd] bg-[#f3f4f6] text-[#1d2230]"
            }`}
          >
            General
          </button>
        </div>
      </section>

      {activeTab === "SECTIONS" ? (
        <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-6 md:p-8">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h2 className="text-2xl md:text-3xl font-medium text-[#171a24]">Section Management</h2>
              <p className="mt-1 text-sm md:text-base text-[#73788d]">
                Add, edit, and remove sections. These rules are the source of truth for grouping in Annual and Monthly views.
              </p>
            </div>

            <button
              type="button"
              onClick={addSection}
              className="inline-flex h-11 items-center gap-2 rounded-2xl bg-[#040426] px-4 text-sm md:text-base text-white"
            >
              <PlusIcon size={18} />
              Add Section
            </button>
          </div>

          <div className="mt-6 space-y-4">
            {draftSections.map((section, index) => (
              <article key={section.id} className={`rounded-[18px] border p-4 ${sectionCardTone(section.kind)}`}>
                <div className="grid gap-3 lg:grid-cols-[1.2fr_0.9fr_1.4fr_auto] lg:items-end">
                  <div>
                    <label className="mb-1 block text-xs uppercase tracking-wide text-[#6f7489]">Section Name</label>
                    <Input
                      value={section.name}
                      onChange={(event) => updateSection(section.id, { name: event.target.value })}
                      className="h-11 rounded-xl border-[#cfd3da] bg-[#f8f9fb]"
                    />
                  </div>

                  <div>
                    <label className="mb-1 block text-xs uppercase tracking-wide text-[#6f7489]">Type</label>
                    <select
                      value={section.kind}
                      onChange={(event) => updateSection(section.id, { kind: event.target.value as ManagedSectionKind })}
                      className="h-11 w-full rounded-xl border border-[#cfd3da] bg-[#f8f9fb] px-3 text-sm text-[#1f2430]"
                    >
                      {KIND_OPTIONS.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="mb-1 block text-xs uppercase tracking-wide text-[#6f7489]">Category Keywords</label>
                    <Input
                      value={section.keywords.join(", ")}
                      onChange={(event) =>
                        updateSection(section.id, {
                          keywords: event.target.value
                            .split(",")
                            .map((part) => part.trim())
                            .filter(Boolean)
                        })
                      }
                      placeholder="e.g. software, office, consulting"
                      className="h-11 rounded-xl border-[#cfd3da] bg-[#f8f9fb]"
                    />
                  </div>

                  <div className="flex flex-wrap gap-2 lg:justify-end">
                    <button
                      type="button"
                      onClick={() => moveSection(section.id, -1)}
                      disabled={index === 0}
                      className="h-11 rounded-xl border border-[#c6cad2] bg-[#f8f9fb] px-3 text-sm text-[#1d2230] disabled:opacity-40"
                    >
                      Up
                    </button>
                    <button
                      type="button"
                      onClick={() => moveSection(section.id, 1)}
                      disabled={index === draftSections.length - 1}
                      className="h-11 rounded-xl border border-[#c6cad2] bg-[#f8f9fb] px-3 text-sm text-[#1d2230] disabled:opacity-40"
                    >
                      Down
                    </button>
                    <button
                      type="button"
                      onClick={() => removeSection(section.id)}
                      className="h-11 rounded-xl border border-[#f2a2b5] bg-[#fff1f4] px-3 text-sm text-[#be123c]"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              </article>
            ))}

            {draftSections.length === 0 ? (
              <p className="text-sm md:text-base text-[#6f7489]">No sections yet. Add your first section.</p>
            ) : null}
          </div>

          <div className="mt-6 flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={onSave}
              disabled={hasDuplicateNames}
              className="inline-flex h-11 items-center rounded-2xl bg-[#040426] px-5 text-sm md:text-base text-white disabled:opacity-60"
            >
              Save Sections
            </button>
            <button
              type="button"
              onClick={onReset}
              className="inline-flex h-11 items-center rounded-2xl border border-[#d1d5dd] bg-[#f3f4f6] px-5 text-sm md:text-base text-[#1d2230]"
            >
              Reset to Default
            </button>

            {hasDuplicateNames ? (
              <p className="text-sm text-[#b42318]">Section names must be unique.</p>
            ) : null}

            {message ? <p className="text-sm text-[#5f647a]">{message}</p> : null}
          </div>
        </section>
      ) : (
        <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-6 md:p-8">
          <h2 className="text-2xl md:text-3xl font-medium text-[#171a24]">General</h2>
          <p className="mt-2 text-sm md:text-base text-[#73788d]">More settings tabs can be added here later.</p>
        </section>
      )}
    </div>
  );
}
