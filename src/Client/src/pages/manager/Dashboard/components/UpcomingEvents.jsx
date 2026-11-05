import { Link as RouterLink } from "react-router-dom";
import { Card, CardContent, Chip, List, ListItemButton, ListItemText, Typography } from "@mui/material";
import { PATHS } from "../../../../routes/paths";
import { countdownLabel, dateVi, daysUntil, slotLabel } from "../../../../utils/format";

// Next events sorted by date; each row opens the Project Hub.
export default function UpcomingEvents({ projects }) {
  const upcoming = projects.filter((p) => daysUntil(p.eventDate) >= 0)
    .sort((a, b) => a.eventDate.localeCompare(b.eventDate)).slice(0, 8);
  return (
    <Card sx={{ height: "100%" }}>
      <CardContent>
        <Typography variant="h6" gutterBottom>Sự kiện sắp tới</Typography>
        {upcoming.length === 0 ? <Typography color="text.secondary">Không có sự kiện sắp tới.</Typography> : (
          <List dense disablePadding>
            {upcoming.map((p) => (
              <ListItemButton key={p.id} component={RouterLink} to={PATHS.manager.project(p.id)} sx={{ borderRadius: 2 }}>
                <ListItemText primary={`${p.id} · ${p.coupleName}`} secondary={`${p.hall} · Ca ${slotLabel(p.slot)} · ${dateVi(p.eventDate)}`} />
                <Chip size="small" label={countdownLabel(p.eventDate)} color={daysUntil(p.eventDate) <= 7 ? "warning" : "default"} />
              </ListItemButton>
            ))}
          </List>
        )}
      </CardContent>
    </Card>
  );
}
