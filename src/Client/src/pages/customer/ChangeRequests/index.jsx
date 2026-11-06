import { Link as RouterLink, useSearchParams } from "react-router-dom";
import { Alert, Box, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import CrSummaryCards from "./components/CrSummaryCards";
import CrFilterBar from "./components/CrFilterBar";
import CrTable from "./components/CrTable";
import { typeLabel } from "../../../components/changeRequest/crMeta";
import { useAsync } from "../../../hooks/useAsync";
import { changeRequestService } from "../../../services/changeRequestService";
import { PATHS } from "../../../routes/paths";
import { dateVi, slotLabel } from "../../../utils/format";

const PAGE_SIZE = 8;

// C-05 CR List (UC-07). The API returns every ticket of the Customer's project; status, search and
// sort are applied here so the filter state can live on the URL and survive a refresh.
export default function ChangeRequests() {
  const [params, setParams] = useSearchParams();
  const filters = {
    status: params.get("status") || "",
    q: params.get("q") || "",
    sort: params.get("sort") || "newest",
  };
  const page = Math.max(0, Number(params.get("page") || 1) - 1);
  const { data, loading, error, reload } = useAsync(() => changeRequestService.getList("Customer"));

  const update = (next, nextPage = 0) => setParams({
    ...Object.fromEntries(Object.entries({ ...filters, ...next }).filter(([key, value]) => value && !(key === "sort" && value === "newest"))),
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
    && (!q || [cr.code, cr.description, cr.type, typeLabel(cr.type)].some((value) => (value || "").toLowerCase().includes(q))));
  const sorted = [...filtered].sort((a, b) => {
    if (filters.sort === "oldest") return a.createdAt.localeCompare(b.createdAt);
    if (filters.sort === "impact") return b.totalImpact - a.totalImpact;
    return b.createdAt.localeCompare(a.createdAt);
  });
  const safePage = Math.min(page, Math.max(0, Math.ceil(sorted.length / PAGE_SIZE) - 1));

  const subtitle = data
    ? `${data.coupleName} · ${data.hall} · Ca ${slotLabel(data.slot)} · ${dateVi(data.eventDate)}`
    : "Quản lý và đối soát các đề xuất điều chỉnh dịch vụ, trang trí và số lượng bàn tiệc.";

  return (
    <>
      <PageHeader code="C-05" title="Yêu cầu thay đổi" subtitle={subtitle}
        actions={data && <Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.customer.newChangeRequest}>Tạo yêu cầu</Button>} />

      <Alert severity="info" sx={{ mb: 2 }}>
        Mọi thay đổi sau khi ký hợp đồng được thực hiện qua yêu cầu thay đổi (CR) để chi phí và phụ lục hợp đồng luôn minh bạch.
        Yêu cầu gửi trong khung T-day có thể phát sinh phí phạt theo chính sách.
      </Alert>

      <DataState loading={loading} error={error} onRetry={reload}>
        {data === null ? (
          <Alert severity="info">Tài khoản chưa được gắn với dự án hôn lễ nào nên chưa thể tạo yêu cầu thay đổi.</Alert>
        ) : data && (
          <Stack spacing={2}>
            <CrSummaryCards summary={data.summary} />
            <CrFilterBar filters={filters} counts={counts} onChange={update} onReset={() => setParams({}, { replace: true })} />
            {items.length === 0 ? (
              <Card>
                <CardContent sx={{ textAlign: "center", py: 6 }}>
                  <Typography variant="h6">Chưa có yêu cầu thay đổi</Typography>
                  <Typography color="text.secondary" sx={{ mt: 1, mb: 2 }}>
                    Baseline đã chốt. Khi cần điều chỉnh thực đơn, trang trí hoặc số bàn, hãy tạo yêu cầu đầu tiên.
                  </Typography>
                  <Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.customer.newChangeRequest}>Tạo yêu cầu thay đổi</Button>
                </CardContent>
              </Card>
            ) : sorted.length === 0 ? (
              <Card>
                <CardContent sx={{ textAlign: "center", py: 6 }}>
                  <Typography variant="h6">Không tìm thấy dữ liệu</Typography>
                  <Typography color="text.secondary" sx={{ mt: 1 }}>
                    Chưa có yêu cầu thay đổi nào thuộc trạng thái này hoặc khớp với từ khóa tìm kiếm.
                  </Typography>
                  <Box sx={{ mt: 2 }}>
                    <Button variant="outlined" onClick={() => setParams({}, { replace: true })}>Xóa bộ lọc</Button>
                  </Box>
                </CardContent>
              </Card>
            ) : (
              <CrTable items={sorted} page={safePage} rowsPerPage={PAGE_SIZE}
                onPageChange={(next) => update(filters, next)} />
            )}
          </Stack>
        )}
      </DataState>
    </>
  );
}
