import { BudgetShell } from "@/components/budget/budget-shell";
import { ProjectsView } from "@/components/budget/projects-view";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function ProjectsPage() {
  await requireOwnerSession();

  return (
    <BudgetShell>
      <ProjectsView />
    </BudgetShell>
  );
}
