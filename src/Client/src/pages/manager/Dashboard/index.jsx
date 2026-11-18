import { Link as RouterLink } from "react-router-dom";
import { Box, Button, Stack } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ManagerKpis from "./components/ManagerKpis";
import UpcomingEvents from "./components/UpcomingEvents";
import AlertPanel from "./components/AlertPanel";
import MyTasks from "./components/MyTasks";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { managerService } from "../../../services/managerService";
import { PATHS } from "../../../routes/paths";

// M-01 Manager Dashboard (FE-03, FE-09)
export default function ManagerDashboard() {
  const { data, loading, error, reload } = useAsync(() => managerService.getDashboard());
  usePolling(() => reload({ silent: true }), 30000);

  return (
    <>
      <PageHeader
        code="M-01"
        title="Tổng quan vận hành"
        subtitle="Theo dõi tiến độ các dự án tiệc cưới được phân công, công việc trong ngày và cảnh báo vận hành."
        actions={
          <Stack direction="row" spacing={1}>
            <Button
              variant="outlined"
              startIcon={<RefreshIcon />}
              onClick={() => reload()}
              disabled={loading}
            >
              Làm mới
            </Button>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              component={RouterLink}
              to={PATHS.manager.newProject}
            >
              Khởi tạo hợp đồng
            </Button>
          </Stack>
        }
      />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <>
            <ManagerKpis kpis={data.kpis} />

            <Box
              sx={{
                display: "grid",
                gap: 2.5,
                gridTemplateColumns: { xs: "1fr", lg: "7fr 5fr" },
                mb: 2.5,
              }}
            >
              <AlertPanel
                overdueTasks={data.overdueTasks}
                staleBeos={data.staleBeos}
              />
              <MyTasks tasks={data.myTasks} />
            </Box>

            <Box sx={{ mt: 2 }}>
              <UpcomingEvents milestones={data.upcomingMilestones} />
            </Box>
          </>
        )}
      </DataState>
    </>
  );
}
