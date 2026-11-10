import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import { Alert, Button, Card, CardContent, List, ListItem, ListItemText, Stack, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import AllocationDialog from "../ResourceCalendar/components/AllocationDialog";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { resourceService } from "../../../services/operationsService";
import { projectService } from "../../../services/projectService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi } from "../../../utils/format";

// M-22 (UC-26): the clashing allocations of one allocation, its exception status, and reschedule / substitute.
export default function ConflictDetail() {
  const { allocationId } = useParams();
  const { data, loading, error, reload, setData } = useAsync(() => resourceService.conflict(allocationId), [allocationId]);
  const lookups = useAsync(() => Promise.all([resourceService.calendar({}), projectService.getProjects()]));
  const { busy, run } = useAction();
  const [editing, setEditing] = useState(false);
  const [conflict, setConflict] = useState(null);
  const save = async (body) => {
    const result = await run(() => resourceService.update(Number(allocationId), body));
    if (!result) return;
    if (!result.allocation) { setConflict(result); return; }
    setEditing(false);
    setData(await resourceService.conflict(allocationId));
  };
  const a = data?.allocation;
  return (
    <>
      <PageHeader code="M-22" title={`Chi tiết xung đột #${allocationId}`}
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.resourceCalendar}>Lịch tài nguyên</Button>} />
      <DataState loading={loading} error={error} onRetry={reload} empty={!a} emptyText="Không tìm thấy phân bổ.">
        {a && (
          <Stack spacing={2}>
            <Card>
              <CardContent>
                <Typography variant="subtitle1" fontWeight={600}>{a.resourceType} · {a.resourceName} ({data.resource?.totalQuantity} đơn vị)</Typography>
                <Typography>{a.projectCode} · {a.hall} · {dateTimeVi(a.startTime)} – {dateTimeVi(a.endTime)} · {a.quantity} đơn vị</Typography>
                <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                  <StatusChip kind="allocation" status={a.status} />
                  {a.exceptionStatus && <StatusChip kind="exception" status={a.exceptionStatus} />}
                </Stack>
              </CardContent>
            </Card>
            {data.conflicts.length === 0 ? <Alert severity="success">{data.message ?? "Không còn xung đột."}</Alert> : (
              <Alert severity="error" id="conflictList">
                Phân bổ này vượt sức chứa khi cộng với:
                <List dense>
                  {data.conflicts.map((c, i) => (
                    <ListItem key={i} disableGutters><ListItemText primary={`${c.projectCode}: ${c.quantity} đơn vị`} secondary={`${dateTimeVi(c.startTime)} – ${dateTimeVi(c.endTime)}`} /></ListItem>
                  ))}
                </List>
              </Alert>
            )}
            {a.exceptionStatus === "Pending" && <Alert severity="warning">Đang chờ Admin quyết định ngoại lệ (A-18).</Alert>}
            {a.exceptionStatus === "Rejected" && <Alert severity="info">Admin đã từ chối ngoại lệ; phân bổ không được thực hiện.</Alert>}
            {a.exceptionStatus !== "Pending" && a.status !== "Released" && (
              <div><Button variant="contained" onClick={() => { setConflict(null); setEditing(true); }}>Đổi giờ / đổi tài nguyên</Button></div>
            )}
          </Stack>
        )}
      </DataState>
      {a && (
        <AllocationDialog open={editing} resources={lookups.data?.[0]?.resources ?? []} projects={lookups.data?.[1] ?? []} initial={a} conflict={conflict}
          busy={busy} onClose={() => setEditing(false)} onSave={save} />
      )}
    </>
  );
}
