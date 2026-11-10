import { useState } from "react";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { Alert, Button, Card, CardContent, MenuItem, Stack, TextField } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { feedbackService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// C-15 (UC-15): pick the disputed Frozen BEO version and describe the discrepancy (≤ 1000 characters) → MSG25.
export default function CreateComplaint() {
  const { project, loading: projectLoading, error: projectError } = useCustomerProject();
  const navigate = useNavigate();
  const { data, loading, error } = useAsync(() => (project ? feedbackService.postEvent(project.id) : Promise.resolve(null)), [project?.id]);
  const [beoVersion, setBeoVersion] = useState("");
  const [description, setDescription] = useState("");
  const [showErrors, setShowErrors] = useState(false);
  const { busy, run } = useAction();
  const submit = async (e) => {
    e.preventDefault();
    setShowErrors(true);
    if (!beoVersion || !description.trim() || description.length > 1000) return;
    const created = await run(() => feedbackService.fileComplaint(project.id, { beoVersion: Number(beoVersion), description: description.trim() }),
      "Đã gửi khiếu nại. Nhà hàng sẽ phản hồi sớm (MSG25).");
    if (created) navigate(PATHS.customer.complaint(created.code));
  };
  return (
    <>
      <PageHeader code="C-15" title="Gửi khiếu nại"
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.complaints}>Danh sách khiếu nại</Button>} />
      <DataState loading={projectLoading || loading} error={projectError || error}>
        {data && !data.canComplain ? <Alert severity="info">Chỉ gửi khiếu nại sau khi dự án đã quyết toán và có BEO đã đóng băng.</Alert> : data && (
          <Card component="form" noValidate onSubmit={submit} id="complaintForm">
            <CardContent>
              <Stack spacing={2}>
                <TextField id="complaintBeo" select label="Phiên bản BEO" required value={beoVersion} onChange={(e) => setBeoVersion(e.target.value)}
                  error={showErrors && !beoVersion} helperText={showErrors && !beoVersion ? "Trường Phiên bản BEO là bắt buộc." : " "}
                  slotProps={{ htmlInput: { "data-field": "beoVersion" } }}>
                  {data.frozenBeoVersions.map((v) => <MenuItem key={v} value={v}>BEO v{v}</MenuItem>)}
                </TextField>
                <TextField label="Mô tả sai khác" required multiline minRows={4} value={description} onChange={(e) => setDescription(e.target.value)}
                  error={showErrors && (!description.trim() || description.length > 1000)}
                  helperText={showErrors && !description.trim() ? "Trường Mô tả là bắt buộc." : description.length > 1000 ? "Vượt quá độ dài tối đa 1000 ký tự." : `${description.length}/1000`}
                  slotProps={{ htmlInput: { "data-field": "complaintDescription" } }} />
                <div><Button type="submit" variant="contained" disabled={busy}>Gửi khiếu nại</Button></div>
              </Stack>
            </CardContent>
          </Card>
        )}
      </DataState>
    </>
  );
}
