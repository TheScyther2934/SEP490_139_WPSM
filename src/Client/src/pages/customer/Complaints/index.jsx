import { Link as RouterLink } from "react-router-dom";
import { Button } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ComplaintTable from "../../../components/complaints/ComplaintTable";
import { useAsync } from "../../../hooks/useAsync";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { feedbackService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// C-14 (UC-16): the customer's complaints with status.
export default function Complaints() {
  const { project, loading: projectLoading, error: projectError } = useCustomerProject();
  const { data, loading, error, reload } = useAsync(
    () => (project ? Promise.all([feedbackService.complaints(project.id), feedbackService.postEvent(project.id)]) : Promise.resolve([[], null])), [project?.id]);
  const [rows, postEvent] = data ?? [[], null];
  return (
    <>
      <PageHeader code="C-14" title="Khiếu nại"
        actions={<Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to={PATHS.customer.newComplaint} disabled={!postEvent?.canComplain}>Gửi khiếu nại</Button>} />
      <DataState loading={projectLoading || loading} error={projectError || error} onRetry={reload} empty={!rows.length} emptyText="Không có dữ liệu (MSG01).">
        <ComplaintTable rows={rows} linkTo={(c) => PATHS.customer.complaint(c.code)} />
      </DataState>
    </>
  );
}
