import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import {
  Accordion, AccordionDetails, AccordionSummary, Alert, Box, Button, Card, CardContent, LinearProgress, Stack, Tab, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Tabs, ToggleButton, ToggleButtonGroup, Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import PlaylistAddIcon from "@mui/icons-material/PlaylistAdd";
import RefreshIcon from "@mui/icons-material/Refresh";
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useCoordinationTasks } from "../../../components/coordination/useCoordinationTasks";
import { DEPARTMENTS, DEPARTMENT_KEYS, TASK_FILTERS, taskStats } from "../../../components/coordination/departments";
import { projectService } from "../../../services/projectService";
import { PATHS } from "../../../routes/paths";
import { dateVi, slotLabel } from "../../../utils/format";

const saveBlob = (blob, name) => {
  const url = URL.createObjectURL(blob);
  Object.assign(document.createElement("a"), { href: url, download: name }).click();
  URL.revokeObjectURL(url);
};

function Progress({ stats }) {
  const tone = stats.overdue ? "error" : stats.missing ? "warning" : stats.done === stats.total ? "success" : "primary";
  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "baseline", mb: 0.75, flexWrap: "wrap", gap: 1 }}>
        <Typography fontWeight={600}>{stats.done}/{stats.total} nhiệm vụ đã xong</Typography>
        <Typography variant="body2" color="text.secondary">
          {[stats.overdue && `${stats.overdue} quá hạn`, stats.missing && `${stats.missing} thiếu minh chứng`, `${stats.proofs} minh chứng`].filter(Boolean).join(" · ")}
        </Typography>
      </Stack>
      <LinearProgress variant="determinate" value={stats.total ? (stats.done / stats.total) * 100 : 0} color={tone} sx={{ height: 8, borderRadius: 4 }} />
    </Box>
  );
}

// Services the department prepares (from the coordination sheet), folded below its tasks.
function ItemsTable({ items }) {
  if (!items?.length) return <Typography variant="body2" color="text.secondary">Bộ phận không có dịch vụ riêng, thực hiện theo quy chuẩn vận hành.</Typography>;
  return (
    <TableContainer>
      <Table size="small">
        <TableHead><TableRow><TableCell>Dịch vụ</TableCell><TableCell align="right">Số lượng</TableCell><TableCell>Ghi chú</TableCell></TableRow></TableHead>
        <TableBody>
          {items.map((item) => (
            <TableRow key={item.specItemId}>
              <TableCell sx={{ fontWeight: 600 }}>{item.name}</TableCell>
              <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>{item.effectiveQuantity} {item.unit}</TableCell>
              <TableCell>{item.description}{item.changedByAddon ? " (phát sinh)" : ""}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

// Coordination sheet by department: each department's tasks (preparation from the lock, then the event day), completed with
// photos or documents as proof, and the services it prepares with the PDF sheet.
export default function DepartmentSheets() {
  const { projectCode } = useParams();
  const beo = useAsync(() => projectService.getBeo(projectCode), [projectCode]);
  const coord = useCoordinationTasks(projectCode);
  const { busy: exporting, run } = useAction();
  const [department, setDepartment] = useState("Decor");
  const [filter, setFilter] = useState("all");

  const data = coord.data;
  const tasks = coord.tasks;
  const deptTasks = tasks.filter((t) => t.department === department);
  const shown = deptTasks.filter(TASK_FILTERS[filter].test);
  const all = taskStats(tasks);
  const sheet = beo.data?.departmentSheets?.find((s) => s.department === department);
  const locked = data?.projectStatus === "Locked";

  const download = async () => {
    const blob = await run(() => projectService.downloadDepartmentPdf(projectCode, department));
    if (blob instanceof Blob) saveBlob(blob, `PhieuDieuPhoi_${projectCode}_v${beo.data?.version || 1}_${department}.pdf`);
  };
  const regenerateSheets = async () => {
    const updated = await run(() => projectService.generateDepartmentSheets(projectCode), "Đã cập nhật phiếu của 4 bộ phận từ phiếu điều phối hiện hành.");
    if (updated && updated !== true) beo.setData(updated);
  };

  return (
    <>
      <PageHeader title="Điều phối theo bộ phận"
        subtitle={beo.data ? `${beo.data.coupleName} · ${beo.data.hall} · ca ${slotLabel(beo.data.slot)} · ${dateVi(beo.data.eventDate)}` : `Sự kiện ${projectCode}`}
        actions={<>
          <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.beo(projectCode)}>Về phiếu điều phối</Button>
          <Button startIcon={<RefreshIcon />} onClick={() => coord.board.reload()} disabled={coord.board.loading}>Làm mới</Button>
          {data?.canAddPhotos && <Button variant="contained" startIcon={<AddIcon />} onClick={() => coord.addTask(department)}>Thêm nhiệm vụ</Button>}
        </>} />

      <DataState loading={beo.loading || coord.board.loading} error={beo.error || coord.board.error}
        onRetry={() => { beo.reload(); coord.board.reload(); }}>
        {beo.data && data && (
          <Stack spacing={2}>
            {beo.data.status === "Stale" && (
              <Alert severity="warning" action={<Button color="inherit" component={RouterLink} to={PATHS.manager.beo(projectCode)}>Duyệt lại</Button>}>
                Phiếu điều phối v{beo.data.version} đã cũ vì có hợp đồng phát sinh mới. Duyệt lại để thêm nhiệm vụ cho dịch vụ phát sinh.
              </Alert>
            )}
            {data.projectStatus === "Adjusting" && (
              <Alert severity="info">Sự kiện còn trong giai đoạn điều chỉnh. Nhiệm vụ điều phối được tạo và mở cập nhật ngay sau khi khóa dịch vụ.</Alert>
            )}
            {!data.canAddPhotos && data.projectStatus !== "Adjusting" && (
              <Alert severity="info">Sự kiện đã tất toán hoặc đã đóng. Nhiệm vụ và minh chứng chỉ còn để xem.</Alert>
            )}
            {all.missing > 0 && data.canAddPhotos && (
              <Alert severity="warning" action={<Button color="inherit" onClick={() => setFilter("missing")}>Xem</Button>}>
                Có {all.missing} nhiệm vụ đã đánh dấu xong nhưng chưa có ảnh hoặc tài liệu chứng minh. Hãy bổ sung trước khi tất toán với khách.
              </Alert>
            )}

            {tasks.length === 0 ? (
              <Card><CardContent>
                <Typography sx={{ mb: 2 }}>
                  {locked ? "Chưa có nhiệm vụ điều phối. Tạo nhiệm vụ cho 4 bộ phận từ các dịch vụ đã khóa (dựng sân khấu, nhận bàn ghế, tổng duyệt tiết mục...) và lịch trình ngày cưới."
                    : "Chưa có nhiệm vụ điều phối."}
                </Typography>
                {locked && <Button variant="contained" startIcon={<PlaylistAddIcon />} disabled={coord.busy} onClick={coord.generate}>Tạo nhiệm vụ điều phối</Button>}
              </CardContent></Card>
            ) : (
              <Card><CardContent sx={{ "&:last-child": { pb: 2 } }}><Progress stats={all} /></CardContent></Card>
            )}

            <Card>
              <Tabs value={department} onChange={(_, v) => setDepartment(v)} variant="scrollable" allowScrollButtonsMobile sx={{ borderBottom: 1, borderColor: "divider" }}>
                {DEPARTMENT_KEYS.map((key) => {
                  const s = taskStats(tasks.filter((t) => t.department === key));
                  return <Tab key={key} value={key} label={s.total ? `${DEPARTMENTS[key].label} ${s.done}/${s.total}` : DEPARTMENTS[key].label}
                    sx={s.overdue ? { color: "error.main" } : undefined} />;
                })}
              </Tabs>
              <CardContent>
                <Stack spacing={2}>
                  <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
                    <Typography variant="h6">{DEPARTMENTS[department].full}</Typography>
                    {deptTasks.length > 0 && (
                      <ToggleButtonGroup size="small" exclusive value={filter} onChange={(_, v) => v && setFilter(v)} sx={{ flexWrap: "wrap" }}>
                        {Object.entries(TASK_FILTERS).map(([key, f]) => (
                          <ToggleButton key={key} value={key}>{f.label} ({deptTasks.filter(f.test).length})</ToggleButton>
                        ))}
                      </ToggleButtonGroup>
                    )}
                  </Stack>
                  {deptTasks.length > 0 && <Progress stats={taskStats(deptTasks)} />}
                  <Stack spacing={1.5}>
                    {shown.map((t) => coord.renderTask(t, { showDate: true }))}
                    {deptTasks.length === 0 && tasks.length > 0 && <Typography color="text.secondary">Bộ phận này chưa có nhiệm vụ.</Typography>}
                    {deptTasks.length > 0 && shown.length === 0 && <Typography color="text.secondary">Không có nhiệm vụ nào trong mục này.</Typography>}
                  </Stack>
                  {data.canAddPhotos && (
                    <Button startIcon={<AddIcon />} onClick={() => coord.addTask(department)} sx={{ alignSelf: "flex-start" }}>
                      Thêm nhiệm vụ cho {DEPARTMENTS[department].label.toLowerCase()}
                    </Button>
                  )}

                  <Accordion variant="outlined" disableGutters>
                    <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                      <Typography fontWeight={600}>Dịch vụ bộ phận chuẩn bị ({sheet?.items?.length ?? 0})</Typography>
                    </AccordionSummary>
                    <AccordionDetails>
                      <Stack spacing={1.5}>
                        <Stack direction="row" sx={{ alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                          <Typography variant="body2" color="text.secondary">Theo phiếu điều phối v{beo.data.version}</Typography>
                          <StatusChip kind="beo" status={beo.data.status} />
                        </Stack>
                        <ItemsTable items={sheet?.items} />
                        <Stack direction="row" sx={{ gap: 1, flexWrap: "wrap" }}>
                          <Button size="small" variant="outlined" startIcon={<DownloadOutlinedIcon />} disabled={exporting} onClick={download}>Xuất PDF</Button>
                          <Button size="small" disabled={exporting} onClick={regenerateSheets}>Cập nhật phiếu 4 bộ phận</Button>
                        </Stack>
                      </Stack>
                    </AccordionDetails>
                  </Accordion>
                </Stack>
              </CardContent>
            </Card>
            {locked && tasks.length > 0 && (
              <Typography variant="body2" color="text.secondary">
                Thiếu nhiệm vụ cho dịch vụ mới?{" "}
                <Button size="small" onClick={coord.generate} disabled={coord.busy}>Bổ sung nhiệm vụ từ dịch vụ</Button>
              </Typography>
            )}
          </Stack>
        )}
      </DataState>
      {coord.dialogs}
    </>
  );
}
