import { Link as RouterLink, useParams } from "react-router-dom";
import { Button } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ComplaintProgress from "../../../components/complaints/ComplaintProgress";
import { useAsync } from "../../../hooks/useAsync";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { feedbackService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// C-16 (UC-16): progress Open → Escalated → Resolved and the response, read-only.
export default function ComplaintStatus() {
  const { complaintCode } = useParams();
  const { project } = useCustomerProject();
  const { data, loading, error, reload } = useAsync(
    () => (project ? feedbackService.complaint(project.id, complaintCode) : Promise.resolve(null)), [project?.id, complaintCode]);
  return (
    <>
      <PageHeader code="C-16" title={`Khiếu nại ${complaintCode}`}
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.complaints}>Danh sách khiếu nại</Button>} />
      <DataState loading={loading} error={error} onRetry={reload} empty={!data} emptyText="Không tìm thấy khiếu nại.">
        {data && <ComplaintProgress complaint={data} />}
      </DataState>
    </>
  );
}
