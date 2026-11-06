import { Button, Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import StatusChip from "../../../../components/StatusChip";
import PhotoCard from "../../../../components/PhotoCard";
import { timeVi } from "../../../../utils/format";

// One timeline card: deadline, status (Overdue is set by JOB-05, GBR-07), late marker, photos.
export default function TaskCard({ task, onComplete }) {
  return (
    <Card id={`task-${task.taskId}`} sx={{ borderLeft: 4, borderColor: task.status === "Overdue" ? "error.main" : task.status === "Done" ? "success.main" : "warning.main" }}>
      <CardContent>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
          <div>
            <Typography fontWeight={600}>{timeVi(task.deadline)} · {task.taskName}</Typography>
            <Stack direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: "wrap", gap: 1 }}>
              <StatusChip kind="task" status={task.status} />
              {task.photoRequired && <Chip size="small" icon={<PhotoCameraIcon />} label="Cần ảnh" variant="outlined" />}
              {task.isLate && <Chip size="small" color="error" variant="outlined" label={`Hoàn thành muộn lúc ${timeVi(task.completedAt)}`} />}
              {task.completedAt && !task.isLate && <Chip size="small" variant="outlined" label={`Xong lúc ${timeVi(task.completedAt)}`} />}
            </Stack>
          </div>
          {task.status !== "Done" && <Button variant="contained" onClick={() => onComplete(task)}>Hoàn thành</Button>}
        </Stack>
        {task.photos.length > 0 && (
          <Stack direction="row" spacing={1} sx={{ mt: 2, flexWrap: "wrap", gap: 1 }}>
            {task.photos.map((p) => (
              <div key={p.photoId} style={{ width: 140 }}><PhotoCard photo={p} height={90} title={task.taskName} subtitle={timeVi(p.uploadedAt)} /></div>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
