import Link from "next/link";
import { BudgetShell } from "@/components/budget/budget-shell";
import { Button } from "@/components/ui/button";

export function BudgetOverview() {
  return (
    <BudgetShell
      title="Budget Engine"
      description="Epic 2: annual planning matrix, monthly execution workspace, and audit trail."
    >
      <div className="space-y-4">
        <p className="text-sm text-muted-foreground">
          Use the annual planner to define Jan-Dec targets, then generate monthly actions and execute them with
          PLANNED, DONE, PARTIAL, and SKIPPED statuses.
        </p>
        <div className="flex flex-wrap gap-2">
          <Link href="/annual">
            <Button type="button">Open Annual Planner</Button>
          </Link>
          <Link href="/monthly">
            <Button type="button" variant="secondary">
              Open Monthly Workspace
            </Button>
          </Link>
        </div>
      </div>
    </BudgetShell>
  );
}
