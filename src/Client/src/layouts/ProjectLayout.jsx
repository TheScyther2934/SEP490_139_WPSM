import { useEffect } from "react";
import { Link as RouterLink, Outlet, useLocation, useParams } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux";
import { Box, Breadcrumbs, Card, CardContent, Chip, Link, Stack, Tab, Tabs, Typography } from "@mui/material";
import DataState from "../components/DataState";
import StatusChip from "../components/StatusChip";
import { PATHS } from "../routes/paths";
import { loadProject, selectProjectState } from "../store/projectSlice";
import { countdownLabel, dateVi, slotLabel } from "../utils/format";

const M = PATHS.manager;
const TABS = [
  { label: "Tổng quan", to: M.project },
  { label: "Dịch vụ đã đặt", to: M.specification },
  { label: "Nhà cung cấp", to: M.suppliers },
  { label: "Hợp đồng phát sinh", to: M.addons },
  { label: "Phiếu điều phối", to: M.beo },
  { label: "Thanh toán", to: M.payments },
  { label: "Ngày cưới", to: M.dayOf },
  { label: "Khiếu nại", to: M.complaints },
];

// Child of ManagerLayout: reads :projectCode once, loads the event header and draws the event tabs,
// so the event pages only render their own content.
export default function ProjectLayout() {
  const { projectCode } = useParams();
  const { pathname } = useLocation();
  const dispatch = useDispatch();
  const { code, status, error, snapshot } = useSelector(selectProjectState);

  useEffect(() => { dispatch(loadProject(projectCode)); }, [projectCode, dispatch]);

  // Longest matching tab wins, so /beo/department-sheets keeps the coordination sheet tab selected.
  const current = TABS.map((t) => t.to(projectCode))
    .filter((to) => pathname === to || pathname.startsWith(`${to}/`))
    .sort((a, b) => b.length - a.length)[0] ?? false;

  const project = code === projectCode ? snapshot?.project : null;
  const loading = status === "loading" && !project;

  return (
    <>
      <Breadcrumbs sx={{ mb: 2 }}>
        <Link component={RouterLink} to={M.projects} underline="hover" color="inherit">Sự kiện</Link>
        <Typography color="text.primary">{projectCode}</Typography>
      </Breadcrumbs>
      <DataState loading={loading} error={!project && status === "error" ? error : null} onRetry={() => dispatch(loadProject(projectCode))}>
        {project && (
          <>
            <Card sx={{ mb: 2 }}>
              <CardContent>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ justifyContent: "space-between" }}>
                  <Box>
                    <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
                      <Typography variant="h5" color="primary">{project.id}</Typography>
                      <StatusChip kind="project" status={project.status} />
                    </Stack>
                    <Typography sx={{ mt: 0.5 }}>{project.coupleName}</Typography>
                    <Typography color="text.secondary" variant="body2">
                      {project.hall} · Ca {slotLabel(project.slot)} · {dateVi(project.eventDate)}
                    </Typography>
                  </Box>
                  <Chip label={countdownLabel(project.eventDate)} color="secondary" sx={{ alignSelf: { sm: "center" } }} />
                </Stack>
              </CardContent>
            </Card>
            <Tabs value={current} variant="scrollable" scrollButtons="auto" sx={{ mb: 3, borderBottom: 1, borderColor: "divider" }} aria-label="Các mục của sự kiện">
              {TABS.map((t) => {
                const to = t.to(projectCode);
                return <Tab key={to} label={t.label} value={to} component={RouterLink} to={to} />;
              })}
            </Tabs>
            <Outlet />
          </>
        )}
      </DataState>
    </>
  );
}
