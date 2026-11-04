import { Card, CardContent, Stack, Typography } from "@mui/material";
import { dateVi, slotLabel } from "../../../../utils/format";

export default function CountdownBanner({ dashboard }) {
  return (
    <Card sx={{ mb: 2, background: "linear-gradient(135deg, #6d1634 0%, #8b2e4a 100%)", color: "#fff", border: 0 }}>
      <CardContent sx={{ p: 3 }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
          <div>
            <Typography variant="h5">{dashboard.coupleName}</Typography>
            <Typography sx={{ opacity: 0.85 }}>{dashboard.hall} · Ca {slotLabel(dashboard.slot)} · {dateVi(dashboard.eventDate)}</Typography>
          </div>
          <div>
            <Typography variant="h4" id="countdown-label">{dashboard.countdownLabel}</Typography>
            <Typography sx={{ opacity: 0.85 }}>Ngày sự kiện: {dateVi(dashboard.eventDate)}</Typography>
          </div>
        </Stack>
      </CardContent>
    </Card>
  );
}
