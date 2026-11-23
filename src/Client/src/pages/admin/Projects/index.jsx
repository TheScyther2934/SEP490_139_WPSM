import { useSearchParams } from "react-router-dom";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ProjectFilters from "./components/ProjectFilters";
import ProjectTable from "./components/ProjectTable";
import { useAsync } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";

const KEYS = ["search", "status", "hallId", "managerId", "from", "to", "sort"];
const PAGE_SIZES = [10, 20, 50];

// Admin oversight: every project, searched and filtered on the server; all filters live on the query string.
export default function AdminProjects() {
  const [params, setParams] = useSearchParams();
  const filters = Object.fromEntries(KEYS.map((k) => [k, params.get(k) || ""]));
  const page = Math.max(1, Number(params.get("page") || 1));
  const pageSize = PAGE_SIZES.includes(Number(params.get("pageSize"))) ? Number(params.get("pageSize")) : 20;
  const invalidRange = filters.from && filters.to && filters.from > filters.to;

  const options = useAsync(() => adminService.getProjectFilters());
  const { data, loading, error, reload } = useAsync(
    () => (invalidRange ? Promise.resolve(null) : adminService.getProjects({
      ...Object.fromEntries(Object.entries(filters).filter(([, v]) => v)), page, pageSize,
    })),
    [...KEYS.map((k) => filters[k]), page, pageSize]
  );

  const update = (next, extra = {}) => {
    const merged = { ...filters, ...next, ...extra };
    setParams(Object.fromEntries(Object.entries(merged).filter(([, v]) => v !== "" && v != null).map(([k, v]) => [k, String(v)])), { replace: true });
  };

  return (
    <>
      <PageHeader title="Quản lý sự kiện" subtitle={data ? `${data.total} sự kiện khớp bộ lọc` : "Tất cả sự kiện cưới trong hệ thống."} />
      <ProjectFilters filters={filters} options={options.data} invalidRange={invalidRange}
        onChange={(key, value) => update(typeof key === "object" ? key : { [key]: value })} onReset={() => setParams({}, { replace: true })} />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <ProjectTable result={data} pageSizes={PAGE_SIZES}
            onPageChange={(next) => update({}, { page: next, pageSize })}
            onPageSizeChange={(size) => update({}, { pageSize: size })} />
        )}
      </DataState>
    </>
  );
}
