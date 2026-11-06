import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import TextDialog from "../../../components/TextDialog";
import CalendarFilters from "./components/CalendarFilters";
import { AllocationTable, BookingTable } from "./components/CalendarTables";
import AllocationDialog from "./components/AllocationDialog";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { resourceService } from "../../../services/operationsService";
import { projectService } from "../../../services/projectService";
import { PATHS } from "../../../routes/paths";
import { isoDate } from "../../../utils/format";

// M-21 (UC-25, UC-26): hall bookings and shared-resource allocations; conflicts are checked on save (GBR-09).
export default function ResourceCalendar() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const today = new Date();
  const filters = {
    from: params.get("from") || isoDate(today),
    to: params.get("to") || isoDate(new Date(today.getTime() + 13 * 86400000)),
    hallId: params.get("hallId") || "",
    type: params.get("type") || "",
  };
  const changeFilters = (patch) => {
    const next = { ...filters, ...patch };
    setParams(Object.fromEntries(Object.entries(next).filter(([, v]) => v)), { replace: true });
  };
  const calendar = useAsync(() => resourceService.calendar({ ...filters, hallId: filters.hallId || undefined, type: filters.type || undefined }),
    [filters.from, filters.to, filters.hallId, filters.type]);
  const lookups = useAsync(() => Promise.all([projectService.getHalls(), projectService.getProjects()]));
  const [halls, projects] = lookups.data ?? [[], []];
  const { busy, run } = useAction();
  const [editing, setEditing] = useState(null);
  const [conflict, setConflict] = useState(null);
  const [releasing, setReleasing] = useState(null);

  const open = (initial) => { setConflict(null); setEditing(initial ?? {}); };
  const save = async (body) => {
    const result = await run(() => (editing.allocationId ? resourceService.update(editing.allocationId, body) : resourceService.allocate(body)));
    if (!result) return;
    if (!result.allocation) { setConflict(result); return; }
    setEditing(null);
    calendar.reload();
  };
  const requestException = async (body) => {
    const result = await run(() => resourceService.requestException(body), "Đã gửi yêu cầu ngoại lệ tới Admin.");
    if (result?.allocation) navigate(PATHS.manager.conflict(result.allocation.allocationId));
  };
  const release = async () => {
    if (await run(() => resourceService.release(releasing.allocationId), "Đã giải phóng tài nguyên.")) { setReleasing(null); calendar.reload(); }
  };
  const activeProjects = projects.filter((p) => ["Active Baseline", "Baseline Frozen"].includes(p.status));

  return (
    <>
      <PageHeader code="M-21" title="Lịch tài nguyên"
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => open(null)} disabled={!calendar.data}>Phân bổ tài nguyên</Button>} />
      <CalendarFilters filters={filters} halls={halls} onChange={changeFilters} />
      <DataState loading={calendar.loading} error={calendar.error} onRetry={calendar.reload}>
        {calendar.data && (
          <>
            <BookingTable bookings={calendar.data.bookings} />
            <AllocationTable resources={calendar.data.resources} allocations={calendar.data.allocations}
              onEdit={(a) => open({ ...a })} onRelease={setReleasing} />
          </>
        )}
      </DataState>
      <AllocationDialog open={!!editing} resources={calendar.data?.resources ?? []} projects={activeProjects} initial={editing} conflict={conflict}
        busy={busy} onClose={() => setEditing(null)} onSave={save} onRequestException={requestException} />
      <TextDialog open={!!releasing} title="Giải phóng tài nguyên" busy={busy} confirmText="Giải phóng" color="error"
        description={releasing && `${releasing.resourceName} của ${releasing.projectCode} sẽ được trả lại.`} onClose={() => setReleasing(null)} onConfirm={release} />
    </>
  );
}
