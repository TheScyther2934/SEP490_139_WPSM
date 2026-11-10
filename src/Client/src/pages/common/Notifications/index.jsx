import { useNavigate, useSearchParams } from "react-router-dom";
import { Button, Tab, Tabs } from "@mui/material";
import DoneAllIcon from "@mui/icons-material/DoneAll";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import NotificationList from "./components/NotificationList";
import { notificationTarget } from "./notificationLinks";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { notificationService } from "../../../services/operationsService";

// G-04 (UC-03): All / Unread tabs on the query string, open = mark read + navigate, mark all read.
export default function Notifications() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const unread = params.get("tab") === "unread";
  const { data, loading, error, reload, setData } = useAsync(() => notificationService.list(unread), [unread]);
  const { busy, run } = useAction();
  const open = async (n) => {
    if (!n.isRead) {
      setData((rows) => rows.map((r) => (r.id === n.id ? { ...r, isRead: true } : r)));
      notificationService.markRead(n.id).catch(() => {});
    }
    const target = notificationTarget(n.linkUrl);
    if (target) navigate(target);
  };
  const readAll = async () => {
    if (await run(() => notificationService.markAllRead(), "Đã đánh dấu tất cả là đã đọc.")) reload({ silent: true });
  };
  return (
    <>
      <PageHeader code="G-04" title="Thông báo"
        actions={<Button startIcon={<DoneAllIcon />} onClick={readAll} disabled={busy || !data?.some((n) => !n.isRead)}>Đánh dấu tất cả đã đọc</Button>} />
      <Tabs value={unread ? "unread" : "all"} onChange={(_, v) => setParams(v === "unread" ? { tab: "unread" } : {}, { replace: true })} sx={{ mb: 2 }}>
        <Tab value="all" label="Tất cả" />
        <Tab value="unread" label="Chưa đọc" />
      </Tabs>
      <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length} emptyText="Không có dữ liệu (MSG01).">
        <NotificationList items={data ?? []} onOpen={open} />
      </DataState>
    </>
  );
}
