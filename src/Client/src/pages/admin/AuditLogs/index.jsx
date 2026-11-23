import { useSearchParams } from "react-router-dom";
import PageHeader from "../../../components/PageHeader";
import AuditLogTable from "./components/AuditLogTable";

const KEYS = ["search", "action", "impact", "role", "userId", "from", "to"];

// Admin audit log: who did what, on which object and project (append-only, values before → after). Filters stay on the URL.
export default function AuditLogs() {
  const [params, setParams] = useSearchParams();
  const initial = Object.fromEntries(KEYS.map((k) => [k, params.get(k) || ""]));
  return (
    <>
      <PageHeader title="Nhật ký hoạt động" subtitle="Ai đã thêm, sửa, xóa hoặc đăng nhập — và ảnh hưởng đến dịch vụ, sự kiện, tài khoản, thanh toán nào." />
      <AuditLogTable initial={initial}
        onFiltersChange={(next) => setParams(Object.fromEntries(Object.entries(next).filter(([, v]) => v)), { replace: true })} />
    </>
  );
}
