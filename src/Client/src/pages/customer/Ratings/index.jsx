import { Link as RouterLink } from "react-router-dom";
import { Alert, Button } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import RatingForm from "./components/RatingForm";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { feedbackService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// C-13 (UC-14): ratings are saved once and never changed (GBR-10) → MSG26.
export default function Ratings() {
  const { project, loading: projectLoading, error: projectError } = useCustomerProject();
  const { data, loading, error, reload, setData } = useAsync(() => (project ? feedbackService.postEvent(project.id) : Promise.resolve(null)), [project?.id]);
  const { busy, run } = useAction();
  const submit = async (ratings) => {
    const updated = await run(() => feedbackService.rate(project.id, ratings), "Cảm ơn bạn đã đánh giá (MSG26).");
    if (updated) setData(updated);
  };
  const locked = data && !data.canRate;
  return (
    <>
      <PageHeader code="C-13" title="Đánh giá dịch vụ"
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.postEvent}>Sau sự kiện</Button>} />
      <DataState loading={projectLoading || loading} error={projectError || error} onRetry={reload} empty={!data} emptyText="Tài khoản chưa có dự án.">
        {data && (
          <>
            {locked && data.ratings.length === 0 && <Alert severity="info" sx={{ mb: 2 }}>Chỉ đánh giá được sau khi dự án đã quyết toán.</Alert>}
            <RatingForm existing={data.ratings} disabled={locked} busy={busy} onSubmit={submit} />
          </>
        )}
      </DataState>
    </>
  );
}
