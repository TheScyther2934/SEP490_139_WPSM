import { useEffect, useMemo } from "react";
import { useDispatch } from "react-redux";
import { useSearchParams } from "react-router-dom";
import { Button, Card, CardContent, Stack, Typography } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ExceptionSummary from "./components/ExceptionSummary";
import ExceptionFilters from "./components/ExceptionFilters";
import ExceptionList from "./components/ExceptionList";
import ExceptionDetail from "./components/ExceptionDetail";
import { searchText } from "./exceptionMeta";
import { useAsync } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";
import { notify } from "../../../store/notifySlice";

// A-18 Special Exception Requests (UC-47). A queue of CR penalty waivers and resource conflicts:
// the Admin reviews the source-specific context and decides with a mandatory note.
export default function ExceptionRequests() {
  const dispatch = useDispatch();
  const [params, setParams] = useSearchParams();
  const status = params.get("status") || "Pending";
  const filters = {
    status,
    type: params.get("type") || "",
    q: params.get("q") || "",
  };

  const { data, loading, error, reload } = useAsync(() => adminService.getExceptionRequests());

  const update = (patch) => {
    const merged = { status, type: filters.type, q: filters.q, ...patch };
    const clean = Object.fromEntries(Object.entries(merged)
      .filter(([key, value]) => value && !(key === "status" && value === "Pending")));
    setParams(clean, { replace: true });
  };

  const items = data?.items ?? [];
  const summary = data?.summary ?? { pending: 0, approved: 0, rejected: 0, oldestPendingHours: null };

  const filtered = useMemo(() => {
    const query = filters.q.trim().toLowerCase();
    return items.filter((request) => request.status === filters.status
      && (!filters.type || request.sourceType === filters.type)
      && (!query || searchText(request).includes(query)));
  }, [items, filters.status, filters.type, filters.q]);

  const selectedId = params.get("selected") ? Number(params.get("selected")) : null;
  const selected = filtered.find((request) => request.id === selectedId) ?? filtered[0] ?? null;

  // Keep the URL in step with the visible selection so a refresh reopens the same request.
  useEffect(() => {
    if (selected && selected.id !== selectedId) update({ selected: String(selected.id) });
    if (!selected && selectedId) update({ selected: "" });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected?.id, selectedId]);

  const open = (request) => update({ selected: String(request.id) });

  const decide = async (approve, note) => {
    if (!selected) return;
    await adminService.decideException(selected.id, approve, note);
    dispatch(notify(`Đã ${approve ? "chấp thuận" : "từ chối"} yêu cầu ${selected.code}.`));
    // Show the decision that was just recorded: switch to its tab and keep the selection.
    update({ status: approve ? "Approved" : "Rejected", selected: String(selected.id) });
    await reload({ silent: true });
  };

  return (
    <>
      <PageHeader code="A-18" title="Yêu cầu ngoại lệ"
        subtitle="Ban Giám đốc quyết định các đề xuất miễn phí phạt CR hoặc giữ chỗ tài nguyên vượt công suất."
        actions={<Button startIcon={<RefreshIcon />} onClick={() => reload()}>Làm mới</Button>} />

      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <Stack spacing={2}>
            <ExceptionSummary summary={summary} />
            <ExceptionFilters filters={filters} summary={summary} total={filtered.length}
              onChange={update} onReset={() => setParams({}, { replace: true })} />

            {items.length === 0 ? (
              <Card>
                <CardContent sx={{ textAlign: "center", py: 6 }}>
                  <Typography variant="h6">Chưa có yêu cầu ngoại lệ</Typography>
                  <Typography color="text.secondary" sx={{ mt: 1 }}>
                    Khi Quản lý đề nghị miễn phí phạt cho CR hoặc xin giữ chỗ vượt công suất, yêu cầu sẽ xuất hiện tại đây.
                  </Typography>
                </CardContent>
              </Card>
            ) : filtered.length === 0 ? (
              <Card>
                <CardContent sx={{ textAlign: "center", py: 6 }}>
                  <Typography variant="h6">Không có yêu cầu phù hợp</Typography>
                  <Typography color="text.secondary" sx={{ mt: 1 }}>
                    Không có yêu cầu ngoại lệ nào thuộc nhóm này hoặc khớp với từ khóa tìm kiếm.
                  </Typography>
                </CardContent>
              </Card>
            ) : (
              <Stack direction={{ xs: "column", lg: "row" }} spacing={2} sx={{ alignItems: "flex-start" }}>
                <Stack sx={{ flex: { lg: "0 0 360px" }, width: { xs: "100%", lg: 360 } }}>
                  <ExceptionList requests={filtered} selectedId={selected?.id} onOpen={open} />
                </Stack>
                <Stack sx={{ flex: 1, minWidth: 0 }}>
                  <ExceptionDetail request={selected} onDecide={decide} />
                </Stack>
              </Stack>
            )}
          </Stack>
        )}
      </DataState>
    </>
  );
}
