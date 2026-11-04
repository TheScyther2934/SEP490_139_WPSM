import { Box, Card, CardContent, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { dateVi } from "../../../../utils/format";

// Roadmap at a glance: T-90, T-30, T-7 and Event Day tasks in date order.
export default function MilestoneTimeline({ roadmap }) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Lộ trình cột mốc</Typography>
        {roadmap.length === 0 ? (
          <Typography color="text.secondary">Lộ trình sẽ được tạo sau khi baseline được đóng băng.</Typography>
        ) : (
          <Box id="hubTimeline" sx={{ display: "grid", gap: 1.5, gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))" }}>
            {roadmap.map((t, i) => (
              <Box key={`${t.milestone}-${i}`} sx={{ p: 1.5, border: 1, borderColor: "divider", borderRadius: 2 }}>
                <Typography fontWeight={700} color="primary">{t.milestone}</Typography>
                <Typography variant="caption" color="text.secondary">{dateVi(t.dueDate)}</Typography>
                <Typography variant="body2" sx={{ my: 0.5 }}>{t.title}</Typography>
                <StatusChip kind="task" status={t.status} />
              </Box>
            ))}
          </Box>
        )}
      </CardContent>
    </Card>
  );
}
