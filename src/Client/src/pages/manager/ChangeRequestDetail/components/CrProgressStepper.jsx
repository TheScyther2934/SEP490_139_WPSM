import { Box, Step, StepLabel, Stepper, Typography } from "@mui/material";
import { dateTimeVi } from "../../../../utils/format";

// M-08 negotiation stages: proposal sent → Manager decision → Customer consent → addendum (GBR-04).
export default function CrProgressStepper({ ticket }) {
  const steps = [
    { label: "Gửi đề xuất", done: ticket.status !== "Draft", hint: dateTimeVi(ticket.createdAt) },
    { label: "Quản lý phê duyệt", done: !!ticket.managerApprovedAt, hint: ticket.managerApprovedAt ? dateTimeVi(ticket.managerApprovedAt) : "Đang chờ" },
    { label: "Khách hàng phê duyệt", done: !!ticket.customerApprovedAt, hint: ticket.customerApprovedAt ? dateTimeVi(ticket.customerApprovedAt) : "Đang chờ" },
    { label: "Phụ lục hợp đồng", done: !!ticket.addendumCode, hint: ticket.addendumCode ?? "Chưa tạo" },
  ];
  const rejected = ticket.status === "Rejected";
  const firstOpen = steps.findIndex((step) => !step.done);
  const activeStep = rejected ? -1 : firstOpen === -1 ? steps.length : firstOpen;

  return (
    <Box sx={{ mt: 2 }}>
      <Stepper activeStep={activeStep} alternativeLabel>
        {steps.map((step) => (
          <Step key={step.label} completed={step.done}>
            <StepLabel
              optional={<Typography variant="caption" color="text.secondary">{step.hint}</Typography>}
              error={rejected && step.label === "Quản lý phê duyệt"}>
              {step.label}
            </StepLabel>
          </Step>
        ))}
      </Stepper>
      {rejected && (
        <Typography variant="body2" color="error.main" sx={{ mt: 1, textAlign: "center" }}>
          Yêu cầu đã bị từ chối và được lưu cho mục đích đối soát (GBR-13).
        </Typography>
      )}
    </Box>
  );
}
