import { useMemo, useState } from "react";
import { useDispatch } from "react-redux";
import { useSearchParams } from "react-router-dom";
import { Alert, Button, Stack } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PrintIcon from "@mui/icons-material/Print";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import CalendarToolbar from "./components/CalendarToolbar";
import CalendarSummaryCards from "./components/CalendarSummaryCards";
import HallGrid from "./components/HallGrid";
import ResourceGrid from "./components/ResourceGrid";
import MonthCalendar from "./components/MonthCalendar";
import AllocationDialog from "./components/AllocationDialog";
import UsageDialog from "./components/UsageDialog";
import { rangeFor, shiftAnchor } from "./calendarRange";
import { useAsync } from "../../../hooks/useAsync";
import { resourceCalendarService } from "../../../services/resourceCalendarService";
import { notify } from "../../../store/notifySlice";
import { isoDate } from "../../../utils/format";

// M-21 Resource Calendar (UC-25, UC-26). Day/week/month grids of hall bookings and shared resources,
// with the allocation dialog and links to the M-22 conflict breakdown.
export default function ResourceCalendar() {
  const dispatch = useDispatch();
  const [params, setParams] = useSearchParams();
  const view = ["day", "week", "month"].includes(params.get("view")) ? params.get("view") : "week";
  const anchor = params.get("date") || isoDate(new Date());
  const filters = {
    hallId: params.get("hall") || "",
    type: params.get("type") || "",
    onlyConflicts: params.get("conflicts") === "1",
  };
  const range = useMemo(() => rangeFor(view, anchor), [view, anchor]);
  const { data, loading, error, reload } = useAsync(
    () => resourceCalendarService.getCalendar({ from: range.from, to: range.to, hallId: filters.hallId }),
    [range.from, range.to, filters.hallId]);

  const [dialog, setDialog] = useState(null);
  const [usage, setUsage] = useState(null);

  const update = (patch) => {
    const merged = {
      view,
      date: anchor,
      hall: filters.hallId,
      type: filters.type,
      conflicts: filters.onlyConflicts ? "1" : "",
      ...patch,
    };
    const clean = Object.fromEntries(Object.entries(merged)
      .filter(([key, value]) => value && !(key === "view" && value === "week")));
    setParams(clean, { replace: true });
  };

  const lanes = (data?.lanes ?? []).filter((lane) => !filters.type || lane.type === filters.type);

  const openAllocation = (initial) => {
    setUsage(null);
    setDialog({ initial: initial ?? {} });
  };

  const selectProject = (booking) => {
    const project = data?.projects.find((entry) => entry.projectId === booking.projectId);
    if (!project) {
      dispatch(notify({ message: "Dự án đã quyết toán hoặc đã đóng nên không thể phân bổ thêm tài nguyên.", severity: "warning" }));
      return;
    }
    openAllocation({ projectId: project.projectId });
  };

  const saved = async () => {
    setDialog(null);
    await reload({ silent: true });
  };

  return (
    <>
      <PageHeader code="M-21" title="Lịch tài nguyên và điều phối sảnh"
        subtitle={data ? `${data.halls.length} sảnh đang hoạt động · ${data.summary.events} tiệc trong kỳ đang xem` : "Theo dõi sảnh, ca tiệc và tài nguyên dùng chung theo ngày."}
        actions={(
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
            <Button startIcon={<PrintIcon />} onClick={() => window.print()}>Xuất run-sheet</Button>
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => openAllocation()} id="openAllocationBtn">Phân bổ tài nguyên</Button>
          </Stack>
        )} />

      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <Stack spacing={2}>
            <CalendarToolbar view={view} range={range} halls={data.halls} types={data.types} filters={filters}
              conflictCount={data.summary.conflicts}
              onChange={update} onStep={(delta) => update({ date: shiftAnchor(view, anchor, delta) })}
              onToday={() => update({ date: isoDate(new Date()) })} />

            <CalendarSummaryCards summary={data.summary} />

            {view === "month" ? (
              <MonthCalendar range={range} days={range.days} bookings={data.bookings} lanes={lanes} anchor={anchor}
                onSelectDay={(day) => update({ view: "day", date: day })} />
            ) : (
              <>
                <HallGrid halls={data.halls} bookings={data.bookings} days={range.days} view={view} onSelectProject={selectProject} />
                <ResourceGrid lanes={lanes} days={range.days} view={view} onlyConflicts={filters.onlyConflicts}
                  onOpenUsage={(lane, date) => setUsage({ lane, date })} />
              </>
            )}

            {data.types.length === 0 && (
              <Alert severity="info">
                Chưa khai báo tài nguyên dùng chung. Chạy <code>Database/Updates/20261109_ResourceCalendar.sql</code> hoặc
                bật seeder development để tạo danh mục thiết bị, nhân sự kỹ thuật và bếp.
              </Alert>
            )}
          </Stack>
        )}
      </DataState>

      <AllocationDialog open={!!dialog} calendar={data} initial={dialog?.initial}
        onClose={() => setDialog(null)} onSaved={saved} />

      <UsageDialog open={!!usage} lane={usage?.lane} date={usage?.date}
        onClose={() => setUsage(null)}
        onAllocate={() => openAllocation({ resourceId: usage?.lane?.resourceId })} />
    </>
  );
}
