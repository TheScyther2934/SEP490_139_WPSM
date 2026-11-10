import { Link as RouterLink, useNavigate } from "react-router-dom";
import { Alert, Button } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import CrCreateForm from "../../../components/changeRequests/CrCreateForm";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { PATHS } from "../../../routes/paths";

// C-06 (UC-07, UC-08): only while the baseline is frozen and the event has not taken place.
export default function CreateChangeRequest() {
  const { project, loading, error } = useCustomerProject();
  const navigate = useNavigate();
  return (
    <>
      <PageHeader code="C-06" title="Tạo yêu cầu thay đổi"
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.changeRequests}>Danh sách CR</Button>} />
      <DataState loading={loading} error={error}>
        {project?.status !== "Baseline Frozen" ? (
          <Alert severity="info">Chỉ tạo được yêu cầu thay đổi khi hợp đồng đã chốt và tiệc chưa diễn ra.</Alert>
        ) : (
          <CrCreateForm projectCode={project.id} onCreated={(cr) => navigate(PATHS.customer.changeRequest(cr.code))} />
        )}
      </DataState>
    </>
  );
}
