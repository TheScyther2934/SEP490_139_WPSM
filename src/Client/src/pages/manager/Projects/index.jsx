import { useSearchParams, Link as RouterLink } from "react-router-dom";
import { Button } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ProjectFilters from "./components/ProjectFilters";
import ProjectTable from "./components/ProjectTable";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { PATHS } from "../../../routes/paths";

const PAGE_SIZE = 10;
const KEYS = ["q", "status", "hall", "month"];

// M-02: the Manager's own projects; filters and page live on the query string.
export default function Projects() {
  const [params, setParams] = useSearchParams();
  const filters = Object.fromEntries(KEYS.map((k) => [k, params.get(k) || ""]));
  const page = Math.max(0, Number(params.get("page") || 1) - 1);
  const { data, loading, error, reload } = useAsync(() => projectService.getProjects());

  const update = (next, nextPage = 0) =>
    setParams({ ...Object.fromEntries(Object.entries(next).filter(([, v]) => v)), ...(nextPage ? { page: String(nextPage + 1) } : {}) }, { replace: true });

  const projects = data ?? [];
  const q = filters.q.trim().toLowerCase();
  const filtered = projects.filter((p) => (!q || [p.id, p.coupleName, p.customerEmail].some((v) => (v || "").toLowerCase().includes(q)))
    && (!filters.status || p.status === filters.status)
    && (!filters.hall || p.hall === filters.hall)
    && (!filters.month || p.eventDate.startsWith(filters.month)))
    .sort((a, b) => a.eventDate.localeCompare(b.eventDate));
  const halls = [...new Set(projects.map((p) => p.hall))].sort();
  const months = [...new Set(projects.map((p) => p.eventDate.slice(0, 7)))].sort();
  const safePage = Math.min(page, Math.max(0, Math.ceil(filtered.length / PAGE_SIZE) - 1));

  return (
    <>
      <PageHeader code="M-02" title="Dự án đám cưới" subtitle={data ? `${projects.length} dự án được phân công` : "Danh sách đám cưới bạn đang phụ trách."}
        actions={<Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.manager.newProject}>Khởi tạo hợp đồng gốc</Button>} />
      <ProjectFilters filters={filters} halls={halls} months={months}
        onChange={(key, value) => update({ ...filters, [key]: value })} onReset={() => update({})} />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && <ProjectTable projects={filtered} page={safePage} rowsPerPage={PAGE_SIZE} onPageChange={(next) => update(filters, next)} />}
      </DataState>
    </>
  );
}
