import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DataState from "../../../components/DataState";
import CrTable from "../../../components/changeRequests/CrTable";
import CrCreateForm from "../../../components/changeRequests/CrCreateForm";
import { CR_STATUSES } from "../../../components/changeRequests/crLabels";
import { useAsync } from "../../../hooks/useAsync";
import { useProject } from "../../../hooks/useProject";
import { changeRequestService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// M-07 (UC-22): CR pipeline of the project; the Manager can record a CR on the customer's behalf.
export default function ChangeRequests() {
  const { project } = useProject();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const status = params.get("status") || "";
  const [creating, setCreating] = useState(false);
  const { data, loading, error, reload } = useAsync(() => changeRequestService.list(project.id, status), [project.id, status]);
  return (
    <>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2, justifyContent: "space-between", alignItems: { sm: "center" } }}>
        <Typography variant="h6">Yêu cầu thay đổi (M-07)</Typography>
        <Stack direction="row" spacing={1}>
          <TextField select size="small" label="Trạng thái" value={status} sx={{ minWidth: 180 }}
            onChange={(e) => setParams(e.target.value ? { status: e.target.value } : {}, { replace: true })}>
            {CR_STATUSES.map((s) => <MenuItem key={s.value} value={s.value}>{s.label}</MenuItem>)}
          </TextField>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreating(true)} disabled={creating || project.status !== "Baseline Frozen"}>
            Tạo CR thay khách
          </Button>
        </Stack>
      </Stack>
      {creating && (
        <Stack sx={{ mb: 3 }}>
          <CrCreateForm projectCode={project.id} onCancel={() => setCreating(false)}
            onCreated={(cr) => navigate(PATHS.manager.changeRequest(project.id, cr.code))} />
        </Stack>
      )}
      <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length} emptyText="Không có dữ liệu (MSG01).">
        <CrTable rows={data ?? []} linkTo={(cr) => PATHS.manager.changeRequest(project.id, cr.code)} />
      </DataState>
    </>
  );
}
