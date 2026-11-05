import { Link as RouterLink } from "react-router-dom";
import { Box, Button } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ManagerKpis, { summarize } from "./components/ManagerKpis";
import UpcomingEvents from "./components/UpcomingEvents";
import AlertPanel from "./components/AlertPanel";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { PATHS } from "../../../routes/paths";

// M-01, built from the project list. "My tasks" needs a manager task API and is not shown yet.
export default function ManagerDashboard() {
  const { data, loading, error, reload } = useAsync(() => projectService.getProjects());
  const summary = data ? summarize(data) : null;
  return (
    <>
      <PageHeader code="M-01" title="Tổng quan vận hành"
        actions={<Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.manager.newProject}>Khởi tạo hợp đồng</Button>} />
      <DataState loading={loading} error={error} onRetry={reload}>
        {summary && (
          <>
            <ManagerKpis summary={summary} />
            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "3fr 2fr" } }}>
              <UpcomingEvents projects={summary.active} />
              <AlertPanel stale={summary.stale} />
            </Box>
          </>
        )}
      </DataState>
    </>
  );
}
