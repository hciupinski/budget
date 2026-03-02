import { describe, expect, it } from "vitest";
import { cn } from "@/lib/utils";

describe("cn", () => {
  it("merges class names and drops conflicting tailwind classes", () => {
    expect(cn("p-2", "text-sm", "p-4")).toBe("text-sm p-4");
  });
});
