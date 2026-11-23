import { Alert, Avatar, Box, Card, CardContent, Chip, List, ListItem, ListItemAvatar, ListItemText, Step, StepLabel, Stepper, Typography } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import ProjectOperators from "../../../../components/ProjectOperators";
import { dateTimeVi, dateVi, initials, money } from "../../../../utils/format";

// Status history, money at a glance, the creator and everyone operating the event (Admin can add / remove co-managers).
export default function OverviewTab({ project, onChanged }) {
  const m = project.money;
  const activeStep = project.timeline.filter((s) => s.done).length;
  return (
    <Box sx={{ display: "grid", gap: 2 }}>
      {project.cancelReason && <Alert severity="error">Sự kiện đã hủy, không hoàn cọc. Lý do: {project.cancelReason}</Alert>}
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Tiến trình sự kiện</Typography>
          <Stepper activeStep={activeStep} alternativeLabel sx={{ overflowX: "auto", pb: 1 }}>
            {project.timeline.map((s) => (
              <Step key={s.key} completed={s.done}>
                <StepLabel error={s.key === "cancelled"} optional={
                  <Typography variant="caption" color="text.secondary">
                    {s.at ? (s.key === "signed" || s.key === "locked" || s.key === "cancelled" ? dateTimeVi(s.at) : dateVi(s.at)) : "Chưa diễn ra"}
                    {s.by ? ` · ${s.by}` : ""}
                  </Typography>
                }>{s.label}</StepLabel>
              </Step>
            ))}
          </Stepper>
        </CardContent>
      </Card>

      <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", lg: "repeat(5, 1fr)" } }}>
        <StatCard label="Giá trị lúc ký" value={money(m.signedValue)} hint={`Đặt cọc ${money(m.depositAmount)}`} />
        <StatCard label="Hợp đồng sau điều chỉnh" value={money(m.contractTotal)} hint={`Tối thiểu ${money(m.minimumTotal)}`} />
        <StatCard label="Phát sinh đã xác nhận" value={money(m.confirmedAddonTotal)} hint={`Tổng phải thu ${money(m.grandTotal)}`} />
        <StatCard label="Đã thu" value={money(m.paid)} tone="success.main" />
        <StatCard label="Còn phải thu" value={money(m.outstanding)} tone={m.outstanding > 0 ? "warning.main" : "success.main"} />
      </Box>

      <Card>
        <CardContent>
          <Typography variant="h6">Người tạo sự kiện</Typography>
          <List dense>
            {project.createdBy
              ? <Person person={project.createdBy} badge="Người tạo" detail={`Ký hợp đồng lúc ${dateTimeVi(project.createdAt)}`} />
              : <Typography variant="body2" color="text.secondary">Không có thông tin người tạo.</Typography>}
          </List>
        </CardContent>
      </Card>
      <ProjectOperators projectCode={project.projectCode} onChanged={onChanged} />
    </Box>
  );
}

function Person({ person, badge, detail }) {
  return (
    <ListItem disableGutters secondaryAction={<Chip size="small" label={badge} variant="outlined" />}>
      <ListItemAvatar><Avatar>{initials(person.name)}</Avatar></ListItemAvatar>
      <ListItemText primary={person.name} secondary={`${person.email}${detail ? ` · ${detail}` : ""}`} />
    </ListItem>
  );
}
