import { Link as RouterLink } from "react-router-dom";
import { Box, Button } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ManagerKpis, { summarize } from "./components/ManagerKpis";
import UpcomingEvents from "./components/UpcomingEvents";
import AlertPanel from "./components/AlertPanel";
import MyTasks from "./components/MyTasks";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { managerService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// M-01 (UC-21, UC-32): KPI strip from the project list, "My tasks" and the red alert panel from /manager/work.
export default function ManagerDashboard() {
  const { data, loading, error, reload } = useAsync(() => Promise.all([projectService.getProjects(), managerService.getWork()]));
  usePolling(() => reload({ silent: true }));
  const [projects, work] = data ?? [];
  const summary = projects ? summarize(projects) : null;
  return (
    <>
      <PageHeader code="M-01" title="Tổng quan vận hành"
        actions={<Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.manager.newProject}>Khởi tạo hợp đồng</Button>} />
      <DataState loading={loading} error={error} onRetry={reload}>
        {summary && work && (
          <>
            <ManagerKpis summary={{ ...summary, openCrs: work.openChangeRequests, tasksDueToday: work.tasksDueToday, staleCount: work.staleBeoCount }} />
            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "3fr 2fr" } }}>
              <Box sx={{ display: "grid", gap: 2 }}>
                <MyTasks tasks={work.myTasks} />
                <UpcomingEvents projects={summary.active} />
              </Box>
              <AlertPanel overdue={work.overdue} stale={work.staleBeos} />
            </Box>
          </>
        )}
      </DataState>
    </>
  );
}
