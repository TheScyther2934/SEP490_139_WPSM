import { Box, Card, CardContent, Chip, Stack, Step, StepContent, StepLabel, Stepper, Typography, useMediaQuery } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { dateVi } from "../utils/format";

const STATE = {
  Done: { label: "Đã xong", color: "success" },
  Current: { label: "Đang ở bước này", color: "primary" },
  Upcoming: { label: "Sắp tới", color: "default" },
  Cancelled: { label: "Đã hủy", color: "error" },
};

const range = (s) => (!s.startDate ? "" : s.startDate === s.endDate ? dateVi(s.startDate) : `${dateVi(s.startDate)} – ${dateVi(s.endDate)}`);

// The five stages of an event (sign & deposit, adjusting, locked, wedding day, settlement) with their dates.
// Horizontal on wide screens, vertical with the description on phones.
export default function EventStages({ stages = [], title = "Tiến độ sự kiện", compact = false }) {
  const theme = useTheme();
  const vertical = useMediaQuery(theme.breakpoints.down("md")) || compact;
  const active = Math.max(0, stages.findIndex((s) => s.state === "Current"));
  const allDone = stages.length > 0 && stages.every((s) => s.state === "Done");
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" sx={{ mb: 2 }}>{title}</Typography>
        <Stepper activeStep={allDone ? stages.length : active} orientation={vertical ? "vertical" : "horizontal"} alternativeLabel={!vertical}>
          {stages.map((s) => (
            <Step key={s.number} completed={s.state === "Done"}>
              <StepLabel error={s.state === "Cancelled"}
                optional={<Typography variant="caption" color="text.secondary">{range(s)}</Typography>}>
                <Box component="span" sx={{ fontWeight: s.state === "Current" ? 700 : 500 }}>{s.number}. {s.title}</Box>
              </StepLabel>
              {vertical && (
                <StepContent TransitionProps={{ in: true }}>
                  <Stack spacing={0.5}>
                    <Chip size="small" label={STATE[s.state]?.label ?? s.state} color={STATE[s.state]?.color ?? "default"} sx={{ alignSelf: "flex-start" }} />
                    <Typography variant="body2" color="text.secondary">{s.description}</Typography>
                  </Stack>
                </StepContent>
              )}
            </Step>
          ))}
        </Stepper>
        {!vertical && stages[active] && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 2, textAlign: "center" }}>{stages[active].description}</Typography>
        )}
      </CardContent>
    </Card>
  );
}
