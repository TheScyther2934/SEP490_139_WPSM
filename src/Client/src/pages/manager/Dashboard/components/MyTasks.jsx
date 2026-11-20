import { Link as RouterLink } from "react-router-dom";
import {
  Card,
  CardContent,
  Chip,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  Typography,
} from "@mui/material";
import AssignmentIcon from "@mui/icons-material/Assignment";
import { PATHS } from "../../../../routes/paths";
import { dateVi } from "../../../../utils/format";

const statusColor = (status) => {
  switch (status) {
    case "Done":
      return "success";
    case "Overdue":
      return "error";
    default:
      return "warning";
  }
};

const statusLabel = (status) => {
  switch (status) {
    case "Done":
      return "Đã hoàn thành";
    case "Overdue":
      return "Quá hạn";
    default:
      return "Chờ xử lý";
  }
};

// Next actions derived from each event's stage: lock soon, supplier evidence missing, addon to confirm, settlement to collect.
const TASK_LINK = {
  Lock: PATHS.manager.specification,
  Supplier: PATHS.manager.suppliers,
  Addon: PATHS.manager.addons,
  Settlement: PATHS.manager.payments,
};
export default function MyTasks({ tasks = [] }) {
  return (
    <Card sx={{ height: "100%" }}>
      <CardContent>
        <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1.5 }}>
          <AssignmentIcon color="primary" fontSize="small" />
          <Typography variant="h6">Việc cần làm</Typography>
        </Stack>

        {tasks.length === 0 ? (
          <Typography color="text.secondary">Không có việc cần làm.</Typography>
        ) : (
          <List dense disablePadding>
            {tasks.map((t) => (
              <ListItemButton
                key={`${t.taskType}-${t.taskId}`}
                component={RouterLink}
                to={TASK_LINK[t.taskType]?.(t.projectCode) ?? PATHS.manager.project(t.projectCode)}
                sx={{ borderRadius: 1.5, mb: 0.5, border: "1px solid", borderColor: "divider" }}
              >
                <ListItemText
                  primary={
                    <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                      {t.title}
                    </Typography>
                  }
                  secondary={
                    <Typography variant="caption" color="text.secondary">
                      {t.projectCode} · {t.milestone} · Hạn: {dateVi(t.dueDate)}
                    </Typography>
                  }
                />
                <Chip
                  size="small"
                  label={statusLabel(t.status)}
                  color={statusColor(t.status)}
                  sx={{ ml: 1, fontWeight: "bold" }}
                />
              </ListItemButton>
            ))}
          </List>
        )}
      </CardContent>
    </Card>
  );
}
