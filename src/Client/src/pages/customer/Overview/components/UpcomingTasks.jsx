import { Link as RouterLink } from "react-router-dom";
import { Button, Card, CardContent, Chip, List, ListItem, ListItemText, Stack, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateVi } from "../../../../utils/format";

export default function UpcomingTasks({ tasks }) {
  const pending = tasks.filter((t) => t.status !== "Done").length;
  return (
    <Card sx={{ height: "100%" }}>
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
          <Typography variant="h6">Việc sắp tới</Typography>
          {pending > 0 && <Chip size="small" color="warning" label={`${pending} việc cần làm`} />}
        </Stack>
        {tasks.length === 0 ? <Typography color="text.secondary">Chưa có nhiệm vụ nào.</Typography> : (
          <List dense disablePadding id="task-list">
            {tasks.map((t) => (
              <ListItem key={t.taskId} disableGutters secondaryAction={<StatusChip kind="task" status={t.status} />}>
                <ListItemText primary={`${t.milestone ? `${t.milestone} · ` : ""}${t.title}`} secondary={`Hạn: ${dateVi(t.dueDate)}`} sx={{ pr: 14 }} />
              </ListItem>
            ))}
          </List>
        )}
        <Button component={RouterLink} to={PATHS.customer.roadmap} sx={{ mt: 1 }}>Xem lộ trình</Button>
      </CardContent>
    </Card>
  );
}
