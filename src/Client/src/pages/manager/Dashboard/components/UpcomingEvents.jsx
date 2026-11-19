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
import EventIcon from "@mui/icons-material/Event";
import { PATHS } from "../../../../routes/paths";
import { dateVi } from "../../../../utils/format";

export default function UpcomingEvents({ milestones = [] }) {
  return (
    <Card sx={{ height: "100%" }}>
      <CardContent>
        <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1.5 }}>
          <EventIcon color="primary" fontSize="small" />
          <Typography variant="h6">Sự kiện sắp tới</Typography>
        </Stack>

        {milestones.length === 0 ? (
          <Typography color="text.secondary">Không có sự kiện sắp tới.</Typography>
        ) : (
          <List dense disablePadding>
            {milestones.map((p) => {
              return (
                <ListItemButton
                  key={p.projectCode}
                  component={RouterLink}
                  to={PATHS.manager.project(p.projectCode)}
                  sx={{ borderRadius: 1.5, mb: 1, border: "1px solid", borderColor: "divider", flexDirection: "column", alignItems: "stretch" }}
                >
                  <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ width: "100%", mb: 0.5 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                      {p.projectCode} · {p.coupleName}
                    </Typography>
                    <Chip
                      size="small"
                      label={p.daysRemaining === 0 ? "Hôm nay" : `Còn ${p.daysRemaining} ngày`}
                      color={p.daysRemaining <= 7 ? "warning" : "default"}
                      sx={{ fontWeight: "bold" }}
                    />
                  </Stack>
                  <Typography variant="caption" color="text.secondary" sx={{ mb: 1 }}>
                    {p.hallName} · Ngày cưới: {dateVi(p.eventDate)} · Giai đoạn {p.stage}: <strong>{p.stageTitle}</strong> · Tiếp theo: {p.nextStep} ({dateVi(p.dueDate)})
                  </Typography>
                  <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 0.5 }}>
                    {p.missingSuppliers > 0 && <Chip size="small" color="warning" label={`Thiếu minh chứng ${p.missingSuppliers} dịch vụ`} />}
                    {p.pendingAddons > 0 && <Chip size="small" color="info" label={`${p.pendingAddons} phát sinh chờ khách`} />}
                  </Stack>
                </ListItemButton>
              );
            })}
          </List>
        )}
      </CardContent>
    </Card>
  );
}
