import { Link as RouterLink, useSearchParams } from "react-router-dom";
import { Button, MenuItem, TextField } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import CrTable from "../../../components/changeRequests/CrTable";
import { CR_STATUSES } from "../../../components/changeRequests/crLabels";
import { useAsync } from "../../../hooks/useAsync";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { changeRequestService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// C-05 (UC-07): CRs of the customer's project; status filter on the query string.
export default function ChangeRequests() {
  const { project, loading: projectLoading, error: projectError } = useCustomerProject();
  const [params, setParams] = useSearchParams();
  const status = params.get("status") || "";
  const { data, loading, error, reload } = useAsync(
    () => (project ? changeRequestService.list(project.id, status) : Promise.resolve([])), [project?.id, status]);
  const canCreate = project?.status === "Baseline Frozen";
  return (
    <>
      <PageHeader code="C-05" title="Yêu cầu thay đổi" subtitle="Mọi thay đổi sau khi chốt hợp đồng đều qua yêu cầu thay đổi (CR)."
        actions={
          <>
            <TextField select size="small" label="Trạng thái" value={status} sx={{ minWidth: 180 }}
              onChange={(e) => setParams(e.target.value ? { status: e.target.value } : {}, { replace: true })}>
              {CR_STATUSES.map((s) => <MenuItem key={s.value} value={s.value}>{s.label}</MenuItem>)}
            </TextField>
            <Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.customer.newChangeRequest} disabled={!canCreate}>
              Tạo CR
            </Button>
          </>
        } />
      <DataState loading={projectLoading || loading} error={projectError || error} onRetry={reload} empty={!data?.length}
        emptyText={project ? "Không có dữ liệu (MSG01)." : "Tài khoản chưa có dự án."}>
        <CrTable rows={data ?? []} linkTo={(cr) => PATHS.customer.changeRequest(cr.code)} />
      </DataState>
    </>
  );
}
