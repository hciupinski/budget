import { describe, expect, it } from "vitest";
import { groupRowsBySection, resolveCustomSection } from "@/components/budget/planner-row-utils";
import type { ManagedSection } from "@/lib/section-settings";

const sections: ManagedSection[] = [
  {
    id: "sec-income",
    name: "Income",
    kind: "INCOME",
    keywords: [],
    order: 0
  },
  {
    id: "sec-personal",
    name: "Personal",
    kind: "PERSONAL_EXPENSES",
    keywords: [],
    order: 1
  }
];

describe("planner-row-utils", () => {
  it("resolves custom section by id first", () => {
    const resolved = resolveCustomSection(
      {
        sectionId: "sec-personal",
        sectionKind: "SAVINGS"
      },
      sections
    );

    expect(resolved.id).toBe("sec-personal");
    expect(resolved.kind).toBe("PERSONAL_EXPENSES");
  });

  it("groups rows by section order", () => {
    const grouped = groupRowsBySection(
      [
        {
          resolvedSection: {
            id: "sec-personal",
            name: "Personal",
            kind: "PERSONAL_EXPENSES",
            order: 1,
            sectionId: "sec-personal"
          },
          rowId: "row-1"
        },
        {
          resolvedSection: {
            id: "sec-income",
            name: "Income",
            kind: "INCOME",
            order: 0,
            sectionId: "sec-income"
          },
          rowId: "row-2"
        }
      ],
      sections
    );

    expect(grouped.map((group) => group.id)).toEqual(["sec-income", "sec-personal"]);
    expect(grouped[0].rows).toHaveLength(1);
    expect(grouped[1].rows).toHaveLength(1);
  });
});
