import { Link as RouterLink } from "react-router-dom";
import { Alert, Box, Button, Stack } from "@mui/material";
import PrintIcon from "@mui/icons-material/Print";
import ModuleCards from "./components/ModuleCards";
import CustomerAccountCard from "./components/CustomerAccountCard";
import MilestoneTimeline from "./components/MilestoneTimeline";
import { useProject } from "../../../hooks/useProject";
import { PATHS } from "../../../routes/paths";

// M-04: the project header and tabs come from ProjectLayout; this is the overview tab.
export default function ProjectHub() {
  const { project, roadmap } = useProject();
  return (
    <Stack spacing={2}>
      {project.beoStatus === "Stale" && (
        <Alert severity="error" action={<Button color="inherit" component={RouterLink} to={PATHS.manager.beo(project.id)}>Mở BEO</Button>}>
          BEO đã lỗi thời do có thay đổi được phê duyệt. Vui lòng duyệt lại để gửi sang Bếp và Bar.
        </Alert>
      )}
      <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
        <Button startIcon={<PrintIcon />} onClick={() => window.print()}>In tổng hợp</Button>
      </Stack>
      <ModuleCards project={project} roadmap={roadmap} />
      <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "2fr 1fr" } }}>
        <MilestoneTimeline roadmap={roadmap} />
        <CustomerAccountCard project={project} />
      </Box>
    </Stack>
  );
}
