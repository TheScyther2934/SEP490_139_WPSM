import { Link as RouterLink, useParams, useSearchParams } from "react-router-dom";
import { Alert, Box, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import CrSummaryCards from "./components/CrSummaryCards";
import CrFilters from "./components/CrFilters";
import CrTable from "./components/CrTable";
import { typeLabel } from "../../../components/changeRequest/crMeta";
import { useAsync } from "../../../hooks/useAsync";
import { changeRequestService } from "../../../services/changeRequestService";
import { PATHS } from "../../../routes/paths";

const PAGE_SIZE = 10;

// M-07 CR List (UC-22). Pipeline of every ticket on the project, including the ones the Manager logged
// on the Customer's behalf, with entry to M-08 and the create form.
export default function ChangeRequests() {
  const { projectCode } = useParams();
  const [params, setParams] = useSearchParams();
  const filters = {
    status: params.get("status") || "",
    q: params.get("q") || "",
    creator: params.get("creator") || "",
    type: params.get("type") || "",
  };
  const page = Math.max(0, Number(params.get("page") || 1) - 1);
  const { data, loading, error, reload } = useAsync(() => changeRequestService.getList("Manager", projectCode));

  const update = (next, nextPage = 0) => setParams({
    ...Object.fromEntries(Object.entries({ ...filters, ...next }).filter(([, value]) => value)),
    ...(nextPage ? { page: String(nextPage + 1) } : {}),
  }, { replace: true });

  const items = data?.items ?? [];
  const counts = {
    total: items.length,
    Draft: items.filter((cr) => cr.status === "Draft").length,
    Negotiating: items.filter((cr) => cr.status === "Negotiating").length,
    Approved: items.filter((cr) => cr.status === "Approved").length,
    Rejected: items.filter((cr) => cr.status === "Rejected").length,
  };

  const q = filters.q.trim().toLowerCase();
  const filtered = items.filter((cr) => (!filters.status || cr.status === filters.status)
    && (!filters.creator || cr.createdByRole === filters.creator)
    && (!filters.type || cr.type === filters.type)
    && (!q || [cr.code, cr.description, cr.type, typeLabel(cr.type), cr.createdByName].some((value) => (value || "").toLowerCase().includes(q))));
  const safePage = Math.min(page, Math.max(0, Math.ceil(filtered.length / PAGE_SIZE) - 1));
  const openCount = counts.Draft + counts.Negotiating;

  return (
    <>
      <PageHeader code="M-07" title="Quản lý Yêu cầu Thay đổi" subtitle={data ? `${data.coupleName} · ${data.hall} · ${data.summary.total} yêu cầu` : "Pipeline CR của dự án."}
        actions={<Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.manager.newChangeRequest(projectCode)}>Tạo CR thay mặt khách hàng</Button>} />

      {openCount > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Còn {openCount} yêu cầu chưa chốt. BEO không thể đóng băng khi còn CR đang xử lý (GBR-05).
        </Alert>
      )}

      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <Stack spacing={2}>
            <CrSummaryCards summary={data.summary} />
            <CrFilters filters={filters} counts={counts} onChange={update} onReset={() => setParams({}, { replace: true })} />
            {items.length === 0 ? (
              <Card>
                <CardContent sx={{ textAlign: "center", py: 6 }}>
                  <Typography variant="h6">Chưa có yêu cầu thay đổi</Typography>
                  <Typography color="text.secondary" sx={{ mt: 1, mb: 2 }}>
                    Khi khách hàng gửi yêu cầu hoặc quản lý ghi nhận đề nghị qua điện thoại, ticket CR sẽ xuất hiện tại đây.
                  </Typography>
                  <Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.manager.newChangeRequest(projectCode)}>
                    Tạo CR thay mặt khách hàng
                  </Button>
                </CardContent>
              </Card>
            ) : filtered.length === 0 ? (
              <Card>
                <CardContent sx={{ textAlign: "center", py: 6 }}>
                  <Typography variant="h6">Không tìm thấy yêu cầu phù hợp</Typography>
                  <Typography color="text.secondary" sx={{ mt: 1 }}>
                    Không có yêu cầu thay đổi nào khớp với bộ lọc hoặc từ khóa hiện tại.
                  </Typography>
                  <Box sx={{ mt: 2 }}>
                    <Button variant="outlined" onClick={() => setParams({}, { replace: true })}>Xóa bộ lọc</Button>
                  </Box>
                </CardContent>
              </Card>
            ) : (
              <CrTable items={filtered} projectCode={projectCode} page={safePage} rowsPerPage={PAGE_SIZE}
                onPageChange={(next) => update(filters, next)} />
            )}
          </Stack>
        )}
      </DataState>
    </>
  );
}
