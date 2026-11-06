import { Link as RouterLink } from "react-router-dom";
import { Button, Card, CardContent, List, ListItem, ListItemText, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateVi, daysUntil } from "../../../../utils/format";

// "My tasks": open roadmap tasks owned by the Manager across active projects, earliest due first (M-01).
export default function MyTasks({ tasks }) {
  return (
    <Card id="myTasks">
      <CardContent>
        <Typography variant="h6" gutterBottom>Việc của tôi</Typography>
        {tasks.length === 0 ? <Typography color="text.secondary">Không có việc đang chờ.</Typography> : (
          <List dense>
            {tasks.slice(0, 10).map((t) => {
              const late = daysUntil(t.dueDate) < 0;
              return (
                <ListItem key={t.taskId} disableGutters
                  secondaryAction={<Button size="small" component={RouterLink} to={PATHS.manager.roadmap(t.projectCode)}>Mở</Button>}>
                  <ListItemText primary={`${t.milestone} · ${t.title}`}
                    secondary={<>{t.projectCode} · hạn {dateVi(t.dueDate)} {late && <StatusChip kind="task" status="Overdue" />}</>} />
                </ListItem>
              );
            })}
          </List>
        )}
      </CardContent>
    </Card>
  );
}
