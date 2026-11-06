import { Card, CardContent, Step, StepLabel, Stepper, Typography } from "@mui/material";
import StatusChip from "../StatusChip";
import { dateTimeVi } from "../../utils/format";

// Open → (Escalated) → Resolved with the notes recorded at each step (C-16, M-19).
export default function ComplaintProgress({ complaint }) {
  const steps = [
    { label: "Đã gửi", at: complaint.createdAt },
    ...(complaint.escalatedAt ? [{ label: "Chuyển Admin", at: complaint.escalatedAt }] : []),
    { label: "Đã giải quyết", at: complaint.resolvedAt },
  ];
  const active = steps.filter((s) => s.at).length;
  return (
    <Card id="complaintProgress">
      <CardContent>
        <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>
          {complaint.code} · BEO v{complaint.beoVersion} <StatusChip kind="complaint" status={complaint.status} />
        </Typography>
        <Stepper activeStep={active - 1} alternativeLabel sx={{ my: 2 }}>
          {steps.map((s) => <Step key={s.label} completed={!!s.at}><StepLabel optional={s.at ? dateTimeVi(s.at) : null}>{s.label}</StepLabel></Step>)}
        </Stepper>
        <Typography variant="body2" color="text.secondary">Nội dung khiếu nại</Typography>
        <Typography sx={{ whiteSpace: "pre-wrap", mb: 2 }}>{complaint.description}</Typography>
        {complaint.escalationNote && (
          <>
            <Typography variant="body2" color="text.secondary">Ghi chú chuyển Admin</Typography>
            <Typography sx={{ whiteSpace: "pre-wrap", mb: 2 }}>{complaint.escalationNote}</Typography>
          </>
        )}
        {complaint.resolutionNote && (
          <>
            <Typography variant="body2" color="text.secondary">Phản hồi / kết quả</Typography>
            <Typography sx={{ whiteSpace: "pre-wrap" }} data-field="resolution">{complaint.resolutionNote}</Typography>
          </>
        )}
      </CardContent>
    </Card>
  );
}
