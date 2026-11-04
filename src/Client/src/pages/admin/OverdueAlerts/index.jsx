import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import OverdueTable from "./components/OverdueTable";
import { useAsync } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";

// A-15
export default function OverdueAlerts() {
  const { data, loading, error, reload } = useAsync(() => adminService.getOverdueAlerts());
  return (
    <>
      <PageHeader code="A-15" title="Cảnh báo quá hạn" subtitle="Nhiệm vụ ngày sự kiện đã quá hạn mà chưa có ảnh minh chứng." />
      <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length}>
        {data && <OverdueTable alerts={data} />}
      </DataState>
    </>
  );
}
