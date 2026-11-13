import { Card, CardContent, LinearProgress, Stack, Typography } from "@mui/material";
import { dateVi } from "../../../../utils/format";

export default function ProgressSidebar({ roadmap }) {
  const pct = roadmap.totalTasks > 0 ? Math.round((roadmap.doneTasks / roadmap.totalTasks) * 100) : 0;
  return (
    <Card sx={{ position: { lg: "sticky" }, top: { lg: 88 } }}>
      <CardContent>
        <Typography variant="h5" color="primary">{roadmap.countdownDays === 0 ? "Hôm nay là ngày cưới!" : `Còn ${roadmap.countdownDays} ngày`}</Typography>
        <Typography color="text.secondary">Ngày cưới: {dateVi(roadmap.eventDate)}</Typography>
        <Typography sx={{ mt: 2 }} fontWeight={600} id="prog-pct">{pct}%</Typography>
        <LinearProgress variant="determinate" value={pct} sx={{ height: 8, borderRadius: 4 }} aria-label="Tiến độ lộ trình" />
        <Typography variant="body2" color="text.secondary">{roadmap.doneTasks} / {roadmap.totalTasks} nhiệm vụ</Typography>
        <Stack spacing={1} sx={{ mt: 2 }}>
          {roadmap.milestoneGroups.map((g) => (
            <Stack key={g.milestone} direction="row" sx={{ justifyContent: "space-between" }}>
              <Typography variant="body2">{g.milestone}</Typography>
              <Typography variant="body2" color={g.totalCount && g.doneCount === g.totalCount ? "success.main" : "text.secondary"}>{g.doneCount}/{g.totalCount}</Typography>
            </Stack>
          ))}
        </Stack>
      </CardContent>
    </Card>
  );
}
