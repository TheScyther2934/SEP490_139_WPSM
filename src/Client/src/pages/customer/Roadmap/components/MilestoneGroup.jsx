import { Accordion, AccordionDetails, AccordionSummary, Chip, List, ListItemButton, ListItemText, Stack, Typography } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import StatusChip from "../../../../components/StatusChip";
import { dateTimeVi, dateVi } from "../../../../utils/format";

// Completed milestones start collapsed; a task row opens the C-04 dialog.
export default function MilestoneGroup({ group, onOpenTask }) {
  const done = group.totalCount > 0 && group.doneCount === group.totalCount;
  return (
    <Accordion defaultExpanded={!done} disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
          <Typography variant="h6">{group.milestone}</Typography>
          {done && <Chip size="small" color="success" label="Đã xong" />}
          <Typography variant="body2" color="text.secondary">{dateVi(group.milestoneDate)} · {group.doneCount}/{group.totalCount} nhiệm vụ hoàn thành</Typography>
        </Stack>
      </AccordionSummary>
      <AccordionDetails sx={{ p: 0 }}>
        {group.tasks.length === 0 ? <Typography color="text.secondary" sx={{ p: 2 }}>Nhóm này chưa có nhiệm vụ nào.</Typography> : (
          <List disablePadding>
            {group.tasks.map((t) => (
              <ListItemButton key={t.taskId} onClick={() => onOpenTask(t)} data-task-id={t.taskId} sx={{ gap: 2 }}>
                <ListItemText primary={t.title}
                  secondary={`${dateVi(t.dueDate)} · ${t.assigneeRole === "Customer" ? "Khách hàng" : "Ban điều phối"}${t.completedAt ? ` · ✓ ${dateTimeVi(t.completedAt)}` : ""}`} />
                <StatusChip kind="task" status={t.status} />
              </ListItemButton>
            ))}
          </List>
        )}
      </AccordionDetails>
    </Accordion>
  );
}
